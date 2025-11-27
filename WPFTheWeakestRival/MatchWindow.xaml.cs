using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using log4net;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.WildcardService;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;
using WildcardFault = WPFTheWeakestRival.WildcardService.ServiceFault;
using GameplayFault = WPFTheWeakestRival.GameplayService.ServiceFault;

namespace WPFTheWeakestRival
{
    public partial class MatchWindow : Window
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchWindow));

        private const string DEFAULT_LOCALE = "es-MX";
        private const int MAX_QUESTIONS = 40;

        private readonly MatchInfo match;
        private readonly string token;
        private readonly int myUserId;
        private readonly bool isHost;
        private readonly int matchDbId;
        private readonly LobbyWindow lobbyWindow;

        private PlayerWildcardDto myWildcard;

        private readonly List<GameplayServiceProxy.QuestionWithAnswersDto> questionBuffer =
            new List<GameplayServiceProxy.QuestionWithAnswersDto>();

        private int currentQuestionIndex = -1;

        public MatchWindow(MatchInfo match, string token, int myUserId, bool isHost, LobbyWindow lobbyWindow)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            this.token = token;
            this.myUserId = myUserId;
            this.isHost = isHost;
            this.lobbyWindow = lobbyWindow;
            matchDbId = match.MatchDbId;

            InitializeComponent();

            Closed += MatchWindowClosed;
            Loaded += MatchWindowLoaded;

            InitializeUi();
        }

        #region Inicialización básica

        private void InitializeUi()
        {
            if (txtMatchTitle != null)
            {
                txtMatchTitle.Text = "Partida";
            }

            if (txtMatchCodeSmall != null)
            {
                var code = string.IsNullOrWhiteSpace(match.MatchCode)
                    ? "Sin código"
                    : match.MatchCode;

                txtMatchCodeSmall.Text = $"Código: {code}";
            }

            if (lstPlayers != null)
            {
                // Players viene como PlayerSummary[]
                var players = match.Players ?? Array.Empty<PlayerSummary>();
                lstPlayers.ItemsSource = players;

                if (txtPlayersSummary != null)
                {
                    txtPlayersSummary.Text = $"({players.Length})";
                }
            }

            UpdateWildcardUi();
            InitializeQuestionUiEmpty();
        }

        private void MatchWindowClosed(object sender, EventArgs e)
        {
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

        #endregion

        #region Loaded: comodín + preguntas

        private async void MatchWindowLoaded(object sender, RoutedEventArgs e)
        {
            await LoadWildcardAsync();
            await LoadQuestionsAsync();
            ShowCurrentQuestion();
        }

        #endregion

        #region Comodín

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

        #endregion

        #region Preguntas: carga desde GameplayService

        private async Task LoadQuestionsAsync()
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var difficultyByte = MapDifficultyToByte(match.Config?.DifficultyCode);

            try
            {
                var callback = new InstanceContext(new GameplayCallbackStub());
                using (var client = new GameplayServiceProxy.GameplayServiceClient(
                           callback,
                           "WSDualHttpBinding_IGameplayService"))
                {
                    var request = new GameplayServiceProxy.GetQuestionsRequest
                    {
                        Token = token,
                        Difficulty = difficultyByte,
                        LocaleCode = DEFAULT_LOCALE,
                        MaxQuestions = MAX_QUESTIONS,
                        CategoryId = null
                    };

                    var response = await Task.Run(() => client.GetQuestions(request));

                    questionBuffer.Clear();
                    if (response?.Questions != null)
                    {
                        questionBuffer.AddRange(response.Questions);
                    }
                }

                Logger.InfoFormat(
                    "Preguntas cargadas para la partida {0}. Dificultad={1}, Count={2}",
                    match.MatchId,
                    difficultyByte,
                    questionBuffer.Count);

                currentQuestionIndex = questionBuffer.Count > 0 ? 0 : -1;
            }
            catch (FaultException<GameplayFault> ex)
            {
                Logger.Warn("Fault al obtener preguntas.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    "Preguntas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al obtener preguntas.", ex);
                MessageBox.Show(
                    "No se pudieron cargar las preguntas." + Environment.NewLine + ex.Message,
                    "Preguntas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al obtener preguntas.", ex);
                MessageBox.Show(
                    "Ocurrió un error al cargar las preguntas." + Environment.NewLine + ex.Message,
                    "Preguntas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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

        #endregion

        #region Preguntas: UI

        private GameplayServiceProxy.QuestionWithAnswersDto CurrentQuestion
        {
            get
            {
                if (currentQuestionIndex < 0 || currentQuestionIndex >= questionBuffer.Count)
                {
                    return null;
                }

                return questionBuffer[currentQuestionIndex];
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

        private void ShowCurrentQuestion()
        {
            var question = CurrentQuestion;

            if (question == null)
            {
                if (txtQuestion != null)
                {
                    txtQuestion.Text = "No hay preguntas disponibles para esta dificultad.";
                }

                ResetAnswerButtons();
                return;
            }

            if (txtQuestion != null)
            {
                txtQuestion.Text = question.Body;
            }

            // Answers viene como AnswerDto[]
            var answers = question.Answers ?? Array.Empty<GameplayServiceProxy.AnswerDto>();

            SetAnswerButtonContent(btnAnswer1, answers, 0);
            SetAnswerButtonContent(btnAnswer2, answers, 1);
            SetAnswerButtonContent(btnAnswer3, answers, 2);
            SetAnswerButtonContent(btnAnswer4, answers, 3);

            if (txtAnswerFeedback != null)
            {
                txtAnswerFeedback.Text = "Selecciona una respuesta.";
                txtAnswerFeedback.Foreground = Brushes.LightGray;
            }
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

        private void AnswerButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            if (!(button.Tag is GameplayServiceProxy.AnswerDto answer))
            {
                return;
            }

            var question = CurrentQuestion;
            if (question == null)
            {
                return;
            }

            btnAnswer1.IsEnabled = false;
            btnAnswer2.IsEnabled = false;
            btnAnswer3.IsEnabled = false;
            btnAnswer4.IsEnabled = false;

            var isCorrect = answer.IsCorrect;

            button.Background = isCorrect ? Brushes.DarkSeaGreen : Brushes.IndianRed;

            if (txtAnswerFeedback != null)
            {
                txtAnswerFeedback.Text = isCorrect ? "¡Correcto!" : "Respuesta incorrecta.";
                txtAnswerFeedback.Foreground = isCorrect ? Brushes.LawnGreen : Brushes.OrangeRed;
            }

            // Aquí luego mandaremos SubmitAnswer al GameplayService.
        }

        #endregion

        #region Intro video (stub)

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

        #endregion

        #region Callback stub para GameplayService

        private sealed class GameplayCallbackStub : GameplayServiceProxy.IGameplayServiceCallback
        {
            public void OnNextQuestion(
                Guid matchId,
                GameplayServiceProxy.PlayerSummary targetPlayer,
                GameplayServiceProxy.QuestionDto question,
                decimal currentChain,
                decimal banked)
            {
            }

            public void OnAnswerEvaluated(
                Guid matchId,
                GameplayServiceProxy.PlayerSummary player,
                GameplayServiceProxy.AnswerResult result)
            {
            }

            public void OnBankUpdated(Guid matchId, GameplayServiceProxy.BankState bank)
            {
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

        #endregion
    }
}
