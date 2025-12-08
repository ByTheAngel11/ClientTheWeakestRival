using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using log4net;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.WildcardService;
using WPFTheWeakestRival.Controls;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Pages;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;
using WildcardFault = WPFTheWeakestRival.WildcardService.ServiceFault;

namespace WPFTheWeakestRival
{
    public partial class MatchWindow : Window
    {
        private const string DEFAULT_LOCALE = "es-MX";
        private const int MAX_QUESTIONS = 40;

        private const int QUESTION_TIME_SECONDS = 30;
        private const int TIMER_INTERVAL_SECONDS = 1;

        private const string TIMER_FORMAT = @"mm\:ss";
        private const string POINTS_FORMAT = "0.00";

        private const string GAME_MESSAGE_TITLE = "Juego";
        private const string WILDCARDS_MESSAGE_TITLE = "Wildcards";

        private const string DEFAULT_MATCH_CODE_TEXT = "Sin código";
        private const string DEFAULT_MATCH_CODE_PREFIX = "Código: ";

        private const string DEFAULT_CHAIN_INITIAL_VALUE = "0.00";
        private const string DEFAULT_BANKED_INITIAL_VALUE = "0.00";

        private const string DEFAULT_PLAYER_NAME = "Jugador";
        private const string DEFAULT_OTHER_PLAYER_NAME = "Otro jugador";

        private const string DEFAULT_WAITING_MATCH_TEXT = "Esperando inicio de partida...";
        private const string DEFAULT_WAITING_QUESTION_TEXT = "(esperando pregunta...)";
        private const string DEFAULT_WAITING_TURN_TEXT = "Esperando tu turno...";

        private const string TURN_MY_TURN_TEXT = "Tu turno";
        private const string TURN_OTHER_PLAYER_TEXT = "Turno de otro jugador";

        private const string DEFAULT_TIMER_TEXT = "--:--";

        private const string DEFAULT_NO_WILDCARD_NAME = "Sin comodín";
        private const string DEFAULT_WILDCARD_NAME = "Comodín";

        private const string DEFAULT_TIMEOUT_TEXT = "Tiempo agotado.";
        private const string DEFAULT_SELECT_ANSWER_TEXT = "Selecciona una respuesta.";
        private const string DEFAULT_CORRECT_TEXT = "¡Correcto!";
        private const string DEFAULT_INCORRECT_TEXT = "Respuesta incorrecta.";

        private const string DEFAULT_BANK_ERROR_MESSAGE = "No se pudieron cargar los comodines.";
        private const string DEFAULT_BANK_UNEXPECTED_ERROR_MESSAGE = "Ocurrió un error al cargar los comodines.";

        private const string COIN_FLIP_TITLE = "Volado";
        private const string COIN_FLIP_HEADS_MESSAGE = "¡Cara! Habrá duelo.";
        private const string COIN_FLIP_TAILS_MESSAGE = "Cruz. No habrá duelo.";

        private const byte DIFFICULTY_EASY = 1;
        private const byte DIFFICULTY_MEDIUM = 2;
        private const byte DIFFICULTY_HARD = 3;

        private const string MATCH_WINNER_MESSAGE_FORMAT = "El juego ha terminado. El ganador es: {0}.";

        private const string PHASE_TITLE = "Fase";
        private const string PHASE_ROUND_FORMAT = "Ronda {0}";
        private const string PHASE_DUEL_TEXT = "Duelo";
        private const string PHASE_SPECIAL_EVENT_TEXT = "Evento especial";
        private const string PHASE_FINISHED_TEXT = "Finalizada";

        private enum MatchPhase
        {
            NormalRound,
            Duel,
            SpecialEvent,
            Finished
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchWindow));

        private readonly MatchInfo match;
        private readonly string token;
        private readonly int myUserId;
        private readonly int matchDbId;
        private readonly LobbyWindow lobbyWindow;

        private readonly DispatcherTimer questionTimer;
        private PlayerWildcardDto myWildcard;
        private IReadOnlyList<PlayerWildcardDto> myWildcards = Array.Empty<PlayerWildcardDto>();
        private GameplayServiceProxy.GameplayServiceClient gameplayClient;
        private bool isMyTurn;
        private GameplayServiceProxy.QuestionWithAnswersDto currentQuestion;
        private int remainingSeconds;

        private readonly HashSet<int> eliminatedUserIds = new HashSet<int>();
        private readonly List<int> eliminationOrder = new List<int>();

        private GameplayServiceProxy.CoinFlipResolvedDto lastCoinFlip;

        // DUELO
        private IReadOnlyCollection<DuelCandidateItem> currentDuelCandidates;
        private int currentWeakestRivalUserId;

        // Reto relámpago
        private int lightningTargetUserId;

        // Estadísticas para pantalla final
        private int myCorrectAnswers;
        private int myTotalAnswers;
        private bool isMatchFinished;
        private GameplayServiceProxy.PlayerSummary finalWinner;

        // Fase / ronda
        private int currentRoundNumber = 1;
        private MatchPhase currentPhase = MatchPhase.NormalRound;

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
            this.lobbyWindow = lobbyWindow;
            matchDbId = match.MatchDbId;

            InitializeComponent();

            questionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(TIMER_INTERVAL_SECONDS)
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
                    ? DEFAULT_MATCH_CODE_TEXT
                    : match.MatchCode;

                txtMatchCodeSmall.Text = DEFAULT_MATCH_CODE_PREFIX + code;
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

            txtChain.Text = DEFAULT_CHAIN_INITIAL_VALUE;
            txtBanked.Text = DEFAULT_BANKED_INITIAL_VALUE;

            txtTurnPlayerName.Text = DEFAULT_PLAYER_NAME;
            txtTurnLabel.Text = DEFAULT_WAITING_MATCH_TEXT;

            if (txtTimer != null)
            {
                txtTimer.Text = DEFAULT_TIMER_TEXT;
            }

            UpdateWildcardUi();
            InitializeQuestionUiEmpty();
            UpdatePhaseLabel();
        }

        private void UpdatePhaseLabel()
        {
            if (txtPhase == null)
            {
                return;
            }

            string phaseDetail;

            switch (currentPhase)
            {
                case MatchPhase.NormalRound:
                    phaseDetail = string.Format(
                        CultureInfo.CurrentCulture,
                        PHASE_ROUND_FORMAT,
                        currentRoundNumber);
                    break;

                case MatchPhase.Duel:
                    phaseDetail = PHASE_DUEL_TEXT;
                    break;

                case MatchPhase.SpecialEvent:
                    phaseDetail = PHASE_SPECIAL_EVENT_TEXT;
                    break;

                case MatchPhase.Finished:
                    phaseDetail = PHASE_FINISHED_TEXT;
                    break;

                default:
                    phaseDetail = string.Empty;
                    break;
            }

            txtPhase.Text = $"{PHASE_TITLE}: {phaseDetail}";
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
            catch (Exception ex)
            {
                Logger.Warn("Error al cerrar MatchWindow.", ex);
            }

            if (lobbyWindow != null)
            {
                lobbyWindow.Show();
                lobbyWindow.Activate();
            }
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            if (!isMatchFinished)
            {
                var result = MessageBox.Show(
                    "La partida aún no ha terminado. ¿Deseas salir al lobby?",
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                Close();
                return;
            }

            ShowMatchResultAndClose();
        }

        private async void MatchWindowLoaded(object sender, EventArgs e)
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
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al unirse a la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al unirse a la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
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
                    MaxQuestions = MAX_QUESTIONS,
                    MatchDbId = matchDbId
                };

                await Task.Run(() => gameplayClient.StartMatch(request));
            }
            catch (FaultException<WildcardFault> ex)
            {
                Logger.Warn("Fault al iniciar la partida en MatchWindow.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al iniciar la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al iniciar la partida en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
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

                    var wildcards = response?.Wildcards ?? Array.Empty<PlayerWildcardDto>();

                    myWildcards = wildcards.ToList();
                    myWildcard = myWildcards.LastOrDefault();

                    // DEBUG: ver qué está llegando realmente
                    string codesSummary = myWildcards.Count == 0
                        ? "(sin comodines)"
                        : string.Join(", ", myWildcards.Select(w => w.Code));

                    Logger.InfoFormat(
                        "LoadWildcardAsync: totalWildcards={0}, codes=[{1}], selectedCode={2}",
                        myWildcards.Count,
                        codesSummary,
                        myWildcard?.Code);

                    MessageBox.Show(
                        $"Comodines recibidos: {myWildcards.Count}\nCódigos: {codesSummary}",
                        "DEBUG Wildcards",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Dispatcher.Invoke(UpdateWildcardUi);
                }
            }
            catch (FaultException<WildcardFault> ex)
            {
                Logger.Warn("Fault al obtener comodines del jugador en MatchWindow.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    WILDCARDS_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al obtener comodines en MatchWindow.", ex);
                MessageBox.Show(
                    DEFAULT_BANK_ERROR_MESSAGE + Environment.NewLine + ex.Message,
                    WILDCARDS_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al obtener comodines en MatchWindow.", ex);
                MessageBox.Show(
                    DEFAULT_BANK_UNEXPECTED_ERROR_MESSAGE + Environment.NewLine + ex.Message,
                    WILDCARDS_MESSAGE_TITLE,
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

            if (myWildcards == null || myWildcards.Count == 0 || myWildcard == null)
            {
                txtWildcardName.Text = DEFAULT_NO_WILDCARD_NAME;
                txtWildcardDescription.Text = string.Empty;

                if (imgWildcardIcon != null)
                {
                    imgWildcardIcon.Visibility = Visibility.Collapsed;
                    imgWildcardIcon.Source = null;
                }

                return;
            }

            // ¿Cuántos comodines del MISMO código tengo?
            int sameCodeCount = myWildcards
                .Count(w => string.Equals(w.Code, myWildcard.Code, StringComparison.OrdinalIgnoreCase));

            var baseName = string.IsNullOrWhiteSpace(myWildcard.Name)
                ? myWildcard.Code
                : myWildcard.Name;

            string displayName = baseName ?? DEFAULT_WILDCARD_NAME;

            if (sameCodeCount > 1)
            {
                displayName = $"{displayName} (x{sameCodeCount})";
            }

            txtWildcardName.Text = displayName;

            var baseDescription =
                string.IsNullOrWhiteSpace(myWildcard.Description)
                    ? myWildcard.Code
                    : myWildcard.Description;

            if (sameCodeCount > 1)
            {
                txtWildcardDescription.Text =
                    baseDescription + Environment.NewLine +
                    $"Tienes {sameCodeCount} comodines de este tipo en esta partida.";
            }
            else
            {
                txtWildcardDescription.Text = baseDescription;
            }

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
                txtQuestion.Text = DEFAULT_WAITING_QUESTION_TEXT;
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
                    QuestionId = 0, // int en el proxy
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
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al enviar respuesta en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al enviar respuesta en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
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
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al bancar en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al bancar en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
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
            currentPhase = MatchPhase.NormalRound;
            UpdatePhaseLabel();

            int? targetUserId = targetPlayer != null ? (int?)targetPlayer.UserId : null;
            bool isTargetEliminated = targetUserId.HasValue && eliminatedUserIds.Contains(targetUserId.Value);
            bool isTargetMe = targetUserId.HasValue && targetUserId.Value == myUserId;

            isMyTurn = isTargetMe && !isTargetEliminated;

            currentQuestion = question;

            txtChain.Text = currentChain.ToString(POINTS_FORMAT);
            txtBanked.Text = banked.ToString(POINTS_FORMAT);

            var appearance = AvatarMapper.FromGameplayDto(targetPlayer?.Avatar);
            TurnAvatar.Appearance = appearance;

            txtTurnPlayerName.Text = !string.IsNullOrWhiteSpace(targetPlayer?.DisplayName)
                ? targetPlayer.DisplayName
                : DEFAULT_PLAYER_NAME;

            if (isMyTurn)
            {
                txtTurnLabel.Text = TURN_MY_TURN_TEXT;
                TurnBannerBackground.Background =
                    (Brush)FindResource("Brush.Turn.MyTurn");

                remainingSeconds = QUESTION_TIME_SECONDS;
                UpdateTimerText();
                questionTimer.Start();
            }
            else
            {
                txtTurnLabel.Text = TURN_OTHER_PLAYER_TEXT;
                TurnBannerBackground.Background =
                    (Brush)FindResource("Brush.Turn.OtherTurn");

                questionTimer.Stop();
                if (txtTimer != null)
                {
                    txtTimer.Text = DEFAULT_TIMER_TEXT;
                }
            }

            if (!isMyTurn)
            {
                InitializeQuestionUiEmpty();
                if (txtQuestion != null)
                {
                    txtQuestion.Text = DEFAULT_WAITING_TURN_TEXT;
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
                txtAnswerFeedback.Text = DEFAULT_SELECT_ANSWER_TEXT;
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

            var isMyPlayer = player != null && player.UserId == myUserId;

            if (isMyPlayer)
            {
                myTotalAnswers++;
                if (result.IsCorrect)
                {
                    myCorrectAnswers++;
                }

                txtAnswerFeedback.Text = result.IsCorrect ? DEFAULT_CORRECT_TEXT : DEFAULT_INCORRECT_TEXT;
                txtAnswerFeedback.Foreground = result.IsCorrect ? Brushes.LawnGreen : Brushes.OrangeRed;
            }
            else
            {
                var name = string.IsNullOrWhiteSpace(player?.DisplayName)
                    ? DEFAULT_OTHER_PLAYER_NAME
                    : player.DisplayName;

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

            txtChain.Text = bank.CurrentChain.ToString(POINTS_FORMAT);
            txtBanked.Text = bank.BankedPoints.ToString(POINTS_FORMAT);
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
                    txtAnswerFeedback.Text = DEFAULT_TIMEOUT_TEXT;
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
                    QuestionId = 0, // int en el proxy
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
                txtTimer.Text = DEFAULT_TIMER_TEXT;
                return;
            }

            var time = TimeSpan.FromSeconds(remainingSeconds);
            txtTimer.Text = time.ToString(TIMER_FORMAT);
        }

        private static byte MapDifficultyToByte(string difficultyCode)
        {
            if (string.IsNullOrWhiteSpace(difficultyCode))
            {
                return DIFFICULTY_EASY;
            }

            var code = difficultyCode.Trim().ToUpperInvariant();

            switch (code)
            {
                case "EASY":
                case "E":
                    return DIFFICULTY_EASY;

                case "NORMAL":
                case "MEDIUM":
                case "M":
                    return DIFFICULTY_MEDIUM;

                case "HARD":
                case "H":
                    return DIFFICULTY_HARD;

                default:
                    return DIFFICULTY_EASY;
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
            catch (Exception ex)
            {
                Logger.Warn("Error al detener introVideo en MatchWindow.", ex);
            }
        }

        internal void OnServerVotePhaseStarted(Guid matchId, TimeSpan timeLimit)
        {
            Logger.InfoFormat(
                "OnServerVotePhaseStarted. MatchId={0}, TimeLimitSeconds={1}",
                matchId,
                timeLimit.TotalSeconds);

            ShowVotePage();
        }

        private void ShowVotePage()
        {
            if (eliminatedUserIds.Contains(myUserId))
            {
                Logger.Info("ShowVotePage: current user is eliminated, skipping vote.");

                VotePageOnVoteCompleted(
                    this,
                    new VoteCompletedEventArgs(
                        matchDbId,
                        myUserId,
                        null));

                return;
            }

            var playersForVote = new List<PlayerVoteItem>(BuildVotePlayers());

            var votePage = new VotePage(
                matchDbId,
                myUserId,
                playersForVote);

            votePage.VoteCompleted += VotePageOnVoteCompleted;

            var hostWindow = new Window
            {
                Title = "Votación",
                Content = votePage,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };

            hostWindow.ShowDialog();
        }

        private IEnumerable<PlayerVoteItem> BuildVotePlayers()
        {
            var lobbyPlayers = match.Players ?? Array.Empty<LobbyService.PlayerSummary>();

            foreach (var p in lobbyPlayers)
            {
                if (p == null)
                {
                    continue;
                }

                if (p.UserId == myUserId || eliminatedUserIds.Contains(p.UserId))
                {
                    continue;
                }

                yield return new PlayerVoteItem
                {
                    UserId = p.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(p.DisplayName)
                        ? DEFAULT_PLAYER_NAME
                        : p.DisplayName,
                    BankedPoints = 0m,
                    CorrectAnswers = 0,
                    WrongAnswers = 0
                };
            }
        }

        private async void VotePageOnVoteCompleted(object sender, VoteCompletedEventArgs e)
        {
            if (gameplayClient == null)
            {
                return;
            }

            if (eliminatedUserIds.Contains(myUserId))
            {
                Logger.Info("VotePageOnVoteCompleted: eliminated user, ignoring vote.");
                return;
            }

            try
            {
                var request = new GameplayServiceProxy.CastVoteRequest
                {
                    Token = token,
                    MatchId = match.MatchId,
                    TargetUserId = e.TargetUserId
                };

                Logger.InfoFormat(
                    "Sending CastVote. MatchId={0}, VoterUserId={1}, TargetUserId={2}",
                    match.MatchId,
                    myUserId,
                    e.TargetUserId.HasValue ? e.TargetUserId.Value : 0);

                await Task.Run(() => gameplayClient.CastVote(request));
            }
            catch (FaultException ex)
            {
                Logger.Warn("Fault al enviar voto en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al enviar voto en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al enviar voto en MatchWindow.", ex);
                MessageBox.Show(
                    ex.Message,
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        internal void OnServerElimination(GameplayServiceProxy.PlayerSummary eliminatedPlayer)
        {
            if (eliminatedPlayer == null)
            {
                return;
            }

            eliminatedUserIds.Add(eliminatedPlayer.UserId);
            if (!eliminationOrder.Contains(eliminatedPlayer.UserId))
            {
                eliminationOrder.Add(eliminatedPlayer.UserId);
            }

            var isMe = eliminatedPlayer.UserId == myUserId;
            var name = string.IsNullOrWhiteSpace(eliminatedPlayer.DisplayName)
                ? DEFAULT_PLAYER_NAME
                : eliminatedPlayer.DisplayName;

            if (isMe)
            {
                MessageBox.Show(
                    "Has sido eliminado de la ronda.\nSeguirás viendo la partida como espectador.",
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                isMyTurn = false;
                questionTimer.Stop();

                btnAnswer1.IsEnabled = false;
                btnAnswer2.IsEnabled = false;
                btnAnswer3.IsEnabled = false;
                btnAnswer4.IsEnabled = false;
                btnBank.IsEnabled = false;

                if (txtTurnLabel != null)
                {
                    txtTurnLabel.Text = "Eliminado (espectador)";
                }
            }
            else
            {
                MessageBox.Show(
                    $"{name} ha sido eliminado de la ronda.",
                    GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        internal void OnServerCoinFlipResolved(GameplayServiceProxy.CoinFlipResolvedDto coinFlip)
        {
            if (coinFlip == null)
            {
                return;
            }

            lastCoinFlip = coinFlip;

            Logger.InfoFormat(
                "OnServerCoinFlipResolved. MatchId={0}, RoundId={1}, WeakestRivalUserId={2}, Result={3}, ShouldEnableDuel={4}",
                match.MatchId,
                coinFlip.RoundId,
                coinFlip.WeakestRivalPlayerId,
                coinFlip.Result,
                coinFlip.ShouldEnableDuel);

            currentRoundNumber++;
            currentPhase = coinFlip.ShouldEnableDuel
                ? MatchPhase.Duel
                : MatchPhase.NormalRound;
            UpdatePhaseLabel();

            ShowCoinFlipOverlay(coinFlip);
        }

        private void ShowCoinFlipOverlay(GameplayServiceProxy.CoinFlipResolvedDto coinFlip)
        {
            if (CoinFlipOverlay == null || CoinFlipResultText == null)
            {
                string fallbackMessage = coinFlip.Result == GameplayServiceProxy.CoinFlipResultType.Heads
                    ? COIN_FLIP_HEADS_MESSAGE
                    : COIN_FLIP_TAILS_MESSAGE;

                MessageBox.Show(
                    fallbackMessage,
                    COIN_FLIP_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string message = coinFlip.Result == GameplayServiceProxy.CoinFlipResultType.Heads
                ? COIN_FLIP_HEADS_MESSAGE
                : COIN_FLIP_TAILS_MESSAGE;

            CoinFlipResultText.Text = message;
            CoinFlipOverlay.Visibility = Visibility.Visible;

            var storyboard = TryFindResource("CoinFlipStoryboard") as Storyboard;
            if (storyboard != null)
            {
                storyboard.Completed -= CoinFlipStoryboardCompleted;
                storyboard.Completed += CoinFlipStoryboardCompleted;
                storyboard.Begin();
            }
        }

        private void CoinFlipStoryboardCompleted(object sender, EventArgs e)
        {
            if (CoinFlipOverlay != null)
            {
                CoinFlipOverlay.Visibility = Visibility.Collapsed;
            }

            if (lastCoinFlip == null)
            {
                return;
            }

            if (lastCoinFlip.ShouldEnableDuel)
            {
                MessageBox.Show(
                    "Habrá duelo.",
                    COIN_FLIP_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        internal void OnServerSpecialEvent(Guid matchId, string eventName, string description)
        {
            currentPhase = MatchPhase.SpecialEvent;
            UpdatePhaseLabel();
        }

        // --- Reto relámpago ---

        internal void OnServerLightningChallengeStarted(
            Guid matchId,
            Guid roundId,
            GameplayServiceProxy.PlayerSummary targetPlayer,
            int totalQuestions,
            int totalTimeSeconds)
        {
            currentPhase = MatchPhase.SpecialEvent;
            UpdatePhaseLabel();

            lightningTargetUserId = targetPlayer != null ? targetPlayer.UserId : 0;
            bool isTargetMe = lightningTargetUserId == myUserId && !eliminatedUserIds.Contains(myUserId);

            isMyTurn = isTargetMe;

            var appearance = AvatarMapper.FromGameplayDto(targetPlayer?.Avatar);
            TurnAvatar.Appearance = appearance;

            txtTurnPlayerName.Text = !string.IsNullOrWhiteSpace(targetPlayer?.DisplayName)
                ? targetPlayer.DisplayName
                : DEFAULT_PLAYER_NAME;

            if (isMyTurn)
            {
                txtTurnLabel.Text = "Reto relámpago: tu turno";
                TurnBannerBackground.Background =
                    (Brush)FindResource("Brush.Turn.MyTurn");

                remainingSeconds = QUESTION_TIME_SECONDS;
                UpdateTimerText();
                questionTimer.Start();
            }
            else
            {
                txtTurnLabel.Text = "Reto relámpago en curso";
                TurnBannerBackground.Background =
                    (Brush)FindResource("Brush.Turn.OtherTurn");

                questionTimer.Stop();
                if (txtTimer != null)
                {
                    txtTimer.Text = DEFAULT_TIMER_TEXT;
                }
            }

            InitializeQuestionUiEmpty();
            if (!isMyTurn && txtQuestion != null)
            {
                txtQuestion.Text = "Reto relámpago en curso...";
            }
        }

        internal void OnServerLightningChallengeQuestion(
            Guid matchId,
            Guid roundId,
            int questionIndex,
            GameplayServiceProxy.QuestionWithAnswersDto question)
        {
            currentPhase = MatchPhase.SpecialEvent;
            UpdatePhaseLabel();

            currentQuestion = question;

            if (!isMyTurn)
            {
                InitializeQuestionUiEmpty();
                if (txtQuestion != null)
                {
                    txtQuestion.Text = "Reto relámpago en curso...";
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
                txtAnswerFeedback.Text = DEFAULT_SELECT_ANSWER_TEXT;
                txtAnswerFeedback.Foreground = Brushes.LightGray;
            }

            btnAnswer1.IsEnabled = true;
            btnAnswer2.IsEnabled = true;
            btnAnswer3.IsEnabled = true;
            btnAnswer4.IsEnabled = true;
        }

        internal async void OnServerLightningChallengeFinished(
            Guid matchId,
            Guid roundId,
            int correctAnswers,
            bool isSuccess)
        {
            try
            {
                questionTimer.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error al detener questionTimer en OnServerLightningChallengeFinished.", ex);
            }

            isMyTurn = false;
            lightningTargetUserId = 0;

            string message = isSuccess
                ? $"¡Has completado el reto relámpago! Respuestas correctas: {correctAnswers}."
                : $"Reto relámpago finalizado. Respuestas correctas: {correctAnswers}.";

            MessageBox.Show(
                message,
                GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (isSuccess)
            {
                await LoadWildcardAsync();
            }

            currentPhase = MatchPhase.NormalRound;
            UpdatePhaseLabel();

            if (txtTurnLabel != null)
            {
                txtTurnLabel.Text = DEFAULT_WAITING_TURN_TEXT;
            }

            if (txtTimer != null)
            {
                txtTimer.Text = DEFAULT_TIMER_TEXT;
            }
        }

        // --- DUELO ---

        internal void OnServerDuelCandidates(
            Guid matchId,
            GameplayServiceProxy.DuelCandidatesDto duelCandidates)
        {
            if (duelCandidates == null ||
                duelCandidates.Candidates == null ||
                duelCandidates.Candidates.Length == 0)
            {
                Logger.Warn("OnServerDuelCandidates: sin candidatos.");
                currentDuelCandidates = null;
                currentWeakestRivalUserId = 0;

                return;
            }

            currentWeakestRivalUserId = duelCandidates.WeakestRivalUserId;

            var items = new List<DuelCandidateItem>();

            foreach (var candidate in duelCandidates.Candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                items.Add(
                    new DuelCandidateItem
                    {
                        UserId = candidate.UserId,
                        DisplayName = string.IsNullOrWhiteSpace(candidate.DisplayName)
                            ? DEFAULT_PLAYER_NAME
                            : candidate.DisplayName
                    });
            }

            currentDuelCandidates = items;

            if (myUserId != currentWeakestRivalUserId)
            {
                Logger.InfoFormat(
                    "OnServerDuelCandidates: usuario {0} no es weakest rival ({1}), no muestra diálogo.",
                    myUserId,
                    currentWeakestRivalUserId);

                return;
            }

            currentPhase = MatchPhase.Duel;
            UpdatePhaseLabel();

            ShowDuelSelectionDialog();
        }

        private void ShowDuelSelectionDialog()
        {
            if (currentDuelCandidates == null ||
                currentDuelCandidates.Count == 0)
            {
                Logger.Warn("ShowDuelSelectionDialog: no hay candidatos.");
                return;
            }

            if (gameplayClient == null)
            {
                Logger.Warn("ShowDuelSelectionDialog: gameplayClient es null.");
                return;
            }

            int? selectedUserId = null;

            var duelPage = new DuelSelectionPage(
                matchDbId,
                currentWeakestRivalUserId,
                currentDuelCandidates);

            var hostWindow = new Window
            {
                Title = "Selecciona a quién retar",
                Content = duelPage,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };

            duelPage.DuelSelectionCompleted += async (sender, e) =>
            {
                selectedUserId = e.TargetUserId;
                hostWindow.DialogResult = true;
                hostWindow.Close();

                if (!selectedUserId.HasValue)
                {
                    Logger.Info("ShowDuelSelectionDialog: usuario cerró sin elegir oponente.");
                    return;
                }

                try
                {
                    var request = new GameplayServiceProxy.ChooseDuelOpponentRequest
                    {
                        Token = token,
                        TargetUserId = selectedUserId.Value
                    };

                    Logger.InfoFormat(
                        "Enviando ChooseDuelOpponent. MatchDbId={0}, WeakestRivalUserId={1}, TargetUserId={2}",
                        matchDbId,
                        currentWeakestRivalUserId,
                        selectedUserId.Value);

                    await Task.Run(() => gameplayClient.ChooseDuelOpponent(request));
                }
                catch (FaultException ex)
                {
                    Logger.Warn("Fault al elegir oponente de duelo en MatchWindow.", ex);
                    MessageBox.Show(
                        ex.Message,
                        GAME_MESSAGE_TITLE,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (CommunicationException ex)
                {
                    Logger.Error("Error de comunicación al elegir oponente de duelo en MatchWindow.", ex);
                    MessageBox.Show(
                        ex.Message,
                        GAME_MESSAGE_TITLE,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error inesperado al elegir oponente de duelo en MatchWindow.", ex);
                    MessageBox.Show(
                        ex.Message,
                        GAME_MESSAGE_TITLE,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            hostWindow.ShowDialog();
        }

        internal void OnServerMatchFinished(
            Guid matchId,
            GameplayServiceProxy.PlayerSummary winner)
        {
            isMatchFinished = true;
            finalWinner = winner;

            try
            {
                questionTimer.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error al detener questionTimer en OnServerMatchFinished.", ex);
            }

            isMyTurn = false;

            if (btnAnswer1 != null) btnAnswer1.IsEnabled = false;
            if (btnAnswer2 != null) btnAnswer2.IsEnabled = false;
            if (btnAnswer3 != null) btnAnswer3.IsEnabled = false;
            if (btnAnswer4 != null) btnAnswer4.IsEnabled = false;
            if (btnBank != null) btnBank.IsEnabled = false;

            string winnerName = winner != null && !string.IsNullOrWhiteSpace(winner.DisplayName)
                ? winner.DisplayName
                : DEFAULT_PLAYER_NAME;

            string message = string.Format(
                MATCH_WINNER_MESSAGE_FORMAT,
                winnerName);

            MessageBox.Show(
                message + "\nPuedes cerrar la ventana cuando quieras para ver tu resultado.",
                GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (txtTurnLabel != null)
            {
                txtTurnLabel.Text = "Partida finalizada";
            }

            currentPhase = MatchPhase.Finished;
            UpdatePhaseLabel();
        }

        private void ShowMatchResultAndClose()
        {
            try
            {
                var players = match.Players ?? Array.Empty<PlayerSummary>();
                int totalPlayers = players.Length;

                string mainResultText;
                bool iAmWinner = finalWinner != null && finalWinner.UserId == myUserId;

                if (iAmWinner)
                {
                    mainResultText = "¡GANASTE LA PARTIDA!";
                }
                else
                {
                    mainResultText = "Resultado de la partida";
                }

                AvatarAppearance localAvatar = null;

                var myLobbyPlayer = players
                    .FirstOrDefault(p => p != null && p.UserId == myUserId);

                if (myLobbyPlayer != null)
                {
                    localAvatar = AvatarMapper.FromLobbyDto(myLobbyPlayer.Avatar);
                }

                int winnerUserId = finalWinner != null ? finalWinner.UserId : 0;

                var resultWindow = new MatchResultWindow(
                    mainResultText,
                    myUserId,
                    localAvatar,
                    myCorrectAnswers,
                    myTotalAnswers,
                    players.ToList(),
                    winnerUserId);

                resultWindow.Owner = this;
                resultWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error al mostrar MatchResultWindow.", ex);
            }
            finally
            {
                Close();
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
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerNextQuestion(targetPlayer, question, currentChain, banked);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerNextQuestion(targetPlayer, question, currentChain, banked));
                }
            }

            public void OnAnswerEvaluated(
                Guid matchId,
                GameplayServiceProxy.PlayerSummary player,
                GameplayServiceProxy.AnswerResult result)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerAnswerEvaluated(player, result);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerAnswerEvaluated(player, result));
                }
            }

            public void OnBankUpdated(Guid matchId, GameplayServiceProxy.BankState bank)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerBankUpdated(bank);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerBankUpdated(bank));
                }
            }

            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerVotePhaseStarted(matchId, timeLimit);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerVotePhaseStarted(matchId, timeLimit));
                }
            }

            public void OnElimination(Guid matchId, GameplayServiceProxy.PlayerSummary eliminatedPlayer)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerElimination(eliminatedPlayer);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerElimination(eliminatedPlayer));
                }
            }

            public void OnSpecialEvent(Guid matchId, string eventName, string description)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerSpecialEvent(matchId, eventName, description);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerSpecialEvent(matchId, eventName, description));
                }
            }

            public void OnCoinFlipResolved(
                Guid matchId,
                GameplayServiceProxy.CoinFlipResolvedDto coinFlip)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerCoinFlipResolved(coinFlip);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerCoinFlipResolved(coinFlip));
                }
            }

            public void OnDuelCandidates(
                Guid matchId,
                GameplayServiceProxy.DuelCandidatesDto duelCandidates)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerDuelCandidates(matchId, duelCandidates);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerDuelCandidates(matchId, duelCandidates));
                }
            }

            public void OnMatchFinished(
                Guid matchId,
                GameplayServiceProxy.PlayerSummary winner)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerMatchFinished(matchId, winner);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerMatchFinished(matchId, winner));
                }
            }

            public void OnLightningChallengeStarted(
                Guid matchId,
                Guid roundId,
                GameplayServiceProxy.PlayerSummary targetPlayer,
                int totalQuestions,
                int totalTimeSeconds)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerLightningChallengeStarted(
                        matchId,
                        roundId,
                        targetPlayer,
                        totalQuestions,
                        totalTimeSeconds);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerLightningChallengeStarted(
                            matchId,
                            roundId,
                            targetPlayer,
                            totalQuestions,
                            totalTimeSeconds));
                }
            }

            public void OnLightningChallengeQuestion(
                Guid matchId,
                Guid roundId,
                int questionIndex,
                GameplayServiceProxy.QuestionWithAnswersDto question)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerLightningChallengeQuestion(
                        matchId,
                        roundId,
                        questionIndex,
                        question);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerLightningChallengeQuestion(
                            matchId,
                            roundId,
                            questionIndex,
                            question));
                }
            }

            public void OnLightningChallengeFinished(
                Guid matchId,
                Guid roundId,
                int correctAnswers,
                bool isSuccess)
            {
                if (matchWindow.Dispatcher.CheckAccess())
                {
                    matchWindow.OnServerLightningChallengeFinished(
                        matchId,
                        roundId,
                        correctAnswers,
                        isSuccess);
                }
                else
                {
                    matchWindow.Dispatcher.Invoke(
                        () => matchWindow.OnServerLightningChallengeFinished(
                            matchId,
                            roundId,
                            correctAnswers,
                            isSuccess));
                }
            }
        }
    }
}
