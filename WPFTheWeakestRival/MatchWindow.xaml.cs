using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using log4net;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.WildcardService;
using WPFTheWeakestRival.Controls;
using WPFTheWeakestRival.Helpers;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;
using WildcardFault = WPFTheWeakestRival.WildcardService.ServiceFault;

namespace WPFTheWeakestRival
{
    public partial class MatchWindow : Window
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchWindow));

        private const string DEFAULT_LOCALE = "es-MX";
        private const int MAX_QUESTIONS = 40;

        private const int QUESTION_TIME_SECONDS = 30;
        private const string TIMER_FORMAT = @"mm\:ss";

        private readonly MatchInfo match;
        private readonly string token;
        private readonly int myUserId;
        private readonly bool isHost;
        private readonly int matchDbId;
        private readonly LobbyWindow lobbyWindow;

        private PlayerWildcardDto myWildcard;

        private GameplayServiceProxy.GameplayServiceClient gameplayClient;

        private bool isMyTurn;
        private GameplayServiceProxy.QuestionWithAnswersDto currentQuestion;

        private readonly DispatcherTimer questionTimer;
        private int remainingSeconds;

        public MatchWindow(
            MatchInfo match,
            string token,
            int myUserId,
            bool isHost,
            LobbyWindow lobbyWindow)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            this.token = token;
            this.myUserId = myUserId;
            this.isHost = isHost;
            this.lobbyWindow = lobbyWindow;
            matchDbId = match.MatchDbId;

            InitializeComponent();

            questionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            questionTimer.Tick += QuestionTimerTick;

            Closed += MatchWindowClosed;
            Loaded += MatchWindowLoaded;

            InitializeUi();
        }

        private void InitializeUi()
        {
            if (txtMatchCodeSmall != null)
            {
                var code = string.IsNullOrWhiteSpace(match.MatchCode)
                    ? "Sin código"
                    : match.MatchCode;

                txtMatchCodeSmall.Text = $"Código: {code}";
            }

            if (lstPlayers != null)
            {
                var players = match.Players ?? Array.Empty<PlayerSummary>();
                lstPlayers.ItemsSource = players;

                if (txtPlayersSummary != null)
                {
                    txtPlayersSummary.Text = $"({players.Length})";
                }
            }

            txtChain.Text = "0.00";
            txtBanked.Text = "0.00";

            txtTurnPlayerName.Text = "Jugador";
            txtTurnLabel.Text = "Esperando inicio de partida...";

            if (txtTimer != null)
            {
                txtTimer.Text = "--:--";
            }

            UpdateWildcardUi();
            InitializeQuestionUiEmpty();
        }

        private void MatchWindowClosed(object sender, EventArgs e)
        {
            try
            {
                if (questionTimer != null && questionTimer.IsEnabled)
                {
                    questionTimer.Stop();
                }

                if (gameplayClient != null)
                {
                    if (gameplayClient.State == CommunicationState.Faulted)
                    {
                        gameplayClient.Abort();
                    }
                    else
                    {
                        gameplayClient.Close();
                    }
                }
            }
            catch
            {
            }

            if (lobbyWindow != null)
            {
                lobbyWindow.Show();
                lobbyWindow.Activate();
            }
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void MatchWindowLoaded(object sender, RoutedEventArgs e)
        {
            await LoadWildcardAsync();
            await InitializeGameplayAsync();
            await JoinMatchAsync();
            await StartMatchAsync();
        }

        private async Task InitializeGameplayAsync()
        {
            var callback = new GameplayCallbackStub(this);
            var instanceContext = new InstanceContext(callback);
            gameplayClient = new GameplayServiceProxy.GameplayServiceClient(
                instanceContext,
                "WSDualHttpBinding_IGameplayService");

            await Task.CompletedTask;
        }

        private async Task JoinMatchAsync()
        {
            if (gameplayClient == null)
            {
                return;
            }

            try
            {
                var request = new GameplayServiceProxy.GameplayJoinMatchRequest
                {
                    Token = token,
                    MatchId = match.MatchId
                };

                await Task.Run(() => gameplayClient.JoinMatch(request));
            }
            catch (FaultException<WildcardFault> ex)
            {
                Logger.Warn("Fault al unirse a la partida en MatchWindow.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al unirse a la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al unirse a la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task StartMatchAsync()
        {
            if (gameplayClient == null)
            {
                return;
            }

            try
            {
                var request = new GameplayServiceProxy.GameplayStartMatchRequest
                {
                    Token = token,
                    MatchId = match.MatchId,
                    Difficulty = MapDifficultyToByte(match.Config?.DifficultyCode),
                    LocaleCode = DEFAULT_LOCALE,
                    MaxQuestions = MAX_QUESTIONS
                };

                await Task.Run(() => gameplayClient.StartMatch(request));
            }
            catch (FaultException<WildcardFault> ex)
            {
                Logger.Warn("Fault al iniciar la partida en MatchWindow.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al iniciar la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al iniciar la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadWildcardAsync()
        {
            if (string.IsNullOrWhiteSpace(token) || matchDbId <= 0)
            {
                return;
            }

            try
            {
                using (var client = new WildcardServiceClient("WSHttpBinding_IWildcardService"))
                {
                    var request = new GetPlayerWildcardsRequest
                    {
                        Token = token,
                        MatchId = matchDbId
                    };

                    var response = await Task.Run(() => client.GetPlayerWildcards(request));

                    var wildcard = response?.Wildcards != null && response.Wildcards.Length > 0
                        ? response.Wildcards[0]
                        : null;

                    myWildcard = wildcard;

                    Dispatcher.Invoke(UpdateWildcardUi);
                }
            }
            catch (FaultException<WildcardFault> ex)
            {
                Logger.Warn("Fault al obtener comodines del jugador en MatchWindow.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    "Wildcards",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al obtener comodines en MatchWindow.", ex);
                MessageBox.Show(
                    "No se pudieron cargar los comodines." + Environment.NewLine + ex.Message,
                    "Wildcards",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al obtener comodines en MatchWindow.", ex);
                MessageBox.Show(
                    "Ocurrió un error al cargar los comodines." + Environment.NewLine + ex.Message,
                    "Wildcards",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateWildcardUi()
        {
            if (txtWildcardName == null || txtWildcardDescription == null)
            {
                return;
            }

            if (myWildcard == null)
            {
                txtWildcardName.Text = "Sin comodín";
                txtWildcardDescription.Text = string.Empty;

                if (imgWildcardIcon != null)
                {
                    imgWildcardIcon.Visibility = Visibility.Collapsed;
                    imgWildcardIcon.Source = null;
                }

                return;
            }

            var name = string.IsNullOrWhiteSpace(myWildcard.Name)
                ? myWildcard.Code
                : myWildcard.Name;

            txtWildcardName.Text = name ?? "Comodín";

            txtWildcardDescription.Text =
                string.IsNullOrWhiteSpace(myWildcard.Description)
                    ? myWildcard.Code
                    : myWildcard.Description;

            UpdateWildcardIcon();
        }

        private void UpdateWildcardIcon()
        {
            if (imgWildcardIcon == null)
            {
                return;
            }

            if (myWildcard == null || string.IsNullOrWhiteSpace(myWildcard.Code))
            {
                imgWildcardIcon.Visibility = Visibility.Collapsed;
                imgWildcardIcon.Source = null;
                return;
            }

            try
            {
                var code = myWildcard.Code.Trim();
                var uriString = $"pack://application:,,,/Assets/Wildcards/{code}.png";

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(uriString, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                imgWildcardIcon.Source = bitmap;
                imgWildcardIcon.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.Warn($"No se pudo cargar la imagen del comodín '{myWildcard.Code}'.", ex);
                imgWildcardIcon.Visibility = Visibility.Collapsed;
                imgWildcardIcon.Source = null;
            }
        }

        private void InitializeQuestionUiEmpty()
        {
            if (txtQuestion != null)
            {
                txtQuestion.Text = "(esperando pregunta...)";
            }

            if (txtAnswerFeedback != null)
            {
                txtAnswerFeedback.Text = string.Empty;
            }

            ResetAnswerButtons();
        }

        private void ResetAnswerButtons()
        {
            ResetAnswerButton(btnAnswer1);
            ResetAnswerButton(btnAnswer2);
            ResetAnswerButton(btnAnswer3);
            ResetAnswerButton(btnAnswer4);
        }

        private static void ResetAnswerButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.Content = string.Empty;
            button.Tag = null;
            button.IsEnabled = false;
            button.Visibility = Visibility.Visible;
            button.Background = Brushes.Transparent;
        }

        private static void SetAnswerButtonContent(
            Button button,
            GameplayServiceProxy.AnswerDto[] answers,
            int index)
        {
            if (button == null)
            {
                return;
            }

            if (answers != null && index < answers.Length)
            {
                var answer = answers[index];
                button.Content = answer.Text;
                button.Tag = answer;
                button.Visibility = Visibility.Visible;
                button.IsEnabled = true;
                button.Background = Brushes.Transparent;
            }
            else
            {
                button.Content = string.Empty;
                button.Tag = null;
                button.Visibility = Visibility.Collapsed;
            }
        }

        private async void AnswerButtonClick(object sender, RoutedEventArgs e)
        {
            if (!isMyTurn)
            {
                return;
            }

            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            if (!(button.Tag is GameplayServiceProxy.AnswerDto answer))
            {
                return;
            }

            if (gameplayClient == null)
            {
                return;
            }

            btnAnswer1.IsEnabled = false;
            btnAnswer2.IsEnabled = false;
            btnAnswer3.IsEnabled = false;
            btnAnswer4.IsEnabled = false;

            questionTimer.Stop();

            try
            {
                var request = new GameplayServiceProxy.SubmitAnswerRequest
                {
                    Token = token,
                    MatchId = match.MatchId,
                    QuestionId = Guid.Empty,
                    AnswerText = answer.Text,
                    ResponseTime = TimeSpan.Zero
                };

                await Task.Run(() => gameplayClient.SubmitAnswer(request));
            }
            catch (FaultException ex)
            {
                Logger.Warn("Fault al enviar respuesta en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al enviar respuesta en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al enviar respuesta en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnBankClick(object sender, RoutedEventArgs e)
        {
            if (!isMyTurn || gameplayClient == null)
            {
                return;
            }

            btnBank.IsEnabled = false;
            questionTimer.Stop();

            try
            {
                var request = new GameplayServiceProxy.BankRequest
                {
                    Token = token,
                    MatchId = match.MatchId
                };

                var response = await Task.Run(() => gameplayClient.Bank(request));

                if (response != null)
                {
                    OnServerBankUpdated(response.Bank);
                }
            }
            catch (FaultException ex)
            {
                Logger.Warn("Fault al bancar en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al bancar en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al bancar en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    "Juego",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                btnBank.IsEnabled = true;
            }
        }

        internal void OnServerNextQuestion(
            GameplayServiceProxy.PlayerSummary targetPlayer,
            GameplayServiceProxy.QuestionWithAnswersDto question,
            decimal currentChain,
            decimal banked)
        {
            isMyTurn = targetPlayer != null && targetPlayer.UserId == myUserId;
            currentQuestion = question;

            txtChain.Text = currentChain.ToString("0.00");
            txtBanked.Text = banked.ToString("0.00");

            var appearance = AvatarMapper.FromGameplayDto(targetPlayer?.Avatar);
            TurnAvatar.Appearance = appearance;

            txtTurnPlayerName.Text = !string.IsNullOrWhiteSpace(targetPlayer?.DisplayName)
                ? targetPlayer.DisplayName
                : "Jugador";

            if (isMyTurn)
            {
                txtTurnLabel.Text = "Tu turno";
                TurnBannerBackground.Background =
                    (Brush)FindResource("Brush.Turn.MyTurn");

                remainingSeconds = QUESTION_TIME_SECONDS;
                UpdateTimerText();
                questionTimer.Start();
            }
            else
            {
                txtTurnLabel.Text = "Turno de otro jugador";
                TurnBannerBackground.Background =
                    (Brush)FindResource("Brush.Turn.OtherTurn");

                questionTimer.Stop();
                if (txtTimer != null)
                {
                    txtTimer.Text = "--:--";
                }
            }

            if (!isMyTurn)
            {
                InitializeQuestionUiEmpty();
                if (txtQuestion != null)
                {
                    txtQuestion.Text = "Esperando tu turno...";
                }

                return;
            }

            if (txtQuestion != null)
            {
                txtQuestion.Text = question.Body;
            }

            SetAnswerButtonContent(btnAnswer1, question.Answers, 0);
            SetAnswerButtonContent(btnAnswer2, question.Answers, 1);
            SetAnswerButtonContent(btnAnswer3, question.Answers, 2);
            SetAnswerButtonContent(btnAnswer4, question.Answers, 3);

            if (txtAnswerFeedback != null)
            {
                txtAnswerFeedback.Text = "Selecciona una respuesta.";
                txtAnswerFeedback.Foreground = Brushes.LightGray;
            }

            btnAnswer1.IsEnabled = true;
            btnAnswer2.IsEnabled = true;
            btnAnswer3.IsEnabled = true;
            btnAnswer4.IsEnabled = true;
        }

        internal void OnServerAnswerEvaluated(
            GameplayServiceProxy.PlayerSummary player,
            GameplayServiceProxy.AnswerResult result)
        {
            if (txtAnswerFeedback == null)
            {
                return;
            }

            bool isMyPlayer = player != null && player.UserId == myUserId;

            if (isMyPlayer)
            {
                txtAnswerFeedback.Text = result.IsCorrect ? "¡Correcto!" : "Respuesta incorrecta.";
                txtAnswerFeedback.Foreground = result.IsCorrect ? Brushes.LawnGreen : Brushes.OrangeRed;
            }
            else
            {
                var name = string.IsNullOrWhiteSpace(player?.DisplayName) ? "Otro jugador" : player.DisplayName;
                txtAnswerFeedback.Text = result.IsCorrect
                    ? $"{name} respondió correcto."
                    : $"{name} respondió incorrecto.";
                txtAnswerFeedback.Foreground = Brushes.LightGray;
            }
        }

        internal void OnServerBankUpdated(GameplayServiceProxy.BankState bank)
        {
            if (bank == null)
            {
                return;
            }

            txtChain.Text = bank.CurrentChain.ToString("0.00");
            txtBanked.Text = bank.BankedPoints.ToString("0.00");
        }

        private async void QuestionTimerTick(object sender, EventArgs e)
        {
            if (!isMyTurn)
            {
                questionTimer.Stop();
                return;
            }

            if (remainingSeconds > 0)
            {
                remainingSeconds--;
                UpdateTimerText();
            }

            if (remainingSeconds <= 0)
            {
                questionTimer.Stop();

                btnAnswer1.IsEnabled = false;
                btnAnswer2.IsEnabled = false;
                btnAnswer3.IsEnabled = false;
                btnAnswer4.IsEnabled = false;
                btnBank.IsEnabled = false;

                if (txtAnswerFeedback != null)
                {
                    txtAnswerFeedback.Text = "Tiempo agotado.";
                    txtAnswerFeedback.Foreground = Brushes.OrangeRed;
                }

                await SendTimeoutAnswerAsync();
            }
        }

        private async Task SendTimeoutAnswerAsync()
        {
            if (!isMyTurn || gameplayClient == null)
            {
                return;
            }

            try
            {
                var request = new GameplayServiceProxy.SubmitAnswerRequest
                {
                    Token = token,
                    MatchId = match.MatchId,
                    QuestionId = Guid.Empty,
                    AnswerText = string.Empty,
                    ResponseTime = TimeSpan.FromSeconds(QUESTION_TIME_SECONDS)
                };

                await Task.Run(() => gameplayClient.SubmitAnswer(request));
            }
            catch (FaultException ex)
            {
                Logger.Warn("Fault al procesar timeout en MatchWindow.", ex);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al procesar timeout en MatchWindow.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al procesar timeout en MatchWindow.", ex);
            }
        }

        private void UpdateTimerText()
        {
            if (txtTimer == null)
            {
                return;
            }

            if (remainingSeconds < 0)
            {
                txtTimer.Text = "--:--";
                return;
            }

            var time = TimeSpan.FromSeconds(remainingSeconds);
            txtTimer.Text = time.ToString(TIMER_FORMAT);
        }

        private static byte MapDifficultyToByte(string difficultyCode)
        {
            if (string.IsNullOrWhiteSpace(difficultyCode))
            {
                return 1;
            }

            var code = difficultyCode.Trim().ToUpperInvariant();

            switch (code)
            {
                case "EASY":
                case "E":
                    return 1;
                case "NORMAL":
                case "MEDIUM":
                case "M":
                    return 2;
                case "HARD":
                case "H":
                    return 3;
                default:
                    return 1;
            }
        }

        private void IntroVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            IntroOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnSkipIntroClick(object sender, RoutedEventArgs e)
        {
            IntroOverlay.Visibility = Visibility.Collapsed;
            try
            {
                introVideo.Stop();
            }
            catch
            {
            }
        }

        private sealed class GameplayCallbackStub : GameplayServiceProxy.IGameplayServiceCallback
        {
            private readonly MatchWindow matchWindow;

            public GameplayCallbackStub(MatchWindow matchWindow)
            {
                this.matchWindow = matchWindow ?? throw new ArgumentNullException(nameof(matchWindow));
            }

            public void OnNextQuestion(
                Guid matchId,
                GameplayServiceProxy.PlayerSummary targetPlayer,
                GameplayServiceProxy.QuestionWithAnswersDto question,
                decimal currentChain,
                decimal banked)
            {
                matchWindow.Dispatcher.Invoke(
                    () => matchWindow.OnServerNextQuestion(targetPlayer, question, currentChain, banked));
            }

            public void OnAnswerEvaluated(
                Guid matchId,
                GameplayServiceProxy.PlayerSummary player,
                GameplayServiceProxy.AnswerResult result)
            {
                matchWindow.Dispatcher.Invoke(
                    () => matchWindow.OnServerAnswerEvaluated(player, result));
            }

            public void OnBankUpdated(Guid matchId, GameplayServiceProxy.BankState bank)
            {
                matchWindow.Dispatcher.Invoke(
                    () => matchWindow.OnServerBankUpdated(bank));
            }

            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit)
            {
            }

            public void OnElimination(
                Guid matchId,
                GameplayServiceProxy.PlayerSummary eliminatedPlayer)
            {
            }

            public void OnSpecialEvent(Guid matchId, string eventName, string description)
            {
            }
        }
    }
}
