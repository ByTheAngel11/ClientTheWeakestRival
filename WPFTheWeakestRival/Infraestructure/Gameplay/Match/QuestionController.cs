using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class QuestionController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(QuestionController));

        private const string GENERIC_PLAYER_NAME_FORMAT = "Jugador {0}";

        private const string DARKNESS_UNKNOWN_NAME = "???";
        private const string DARKNESS_TURN_LABEL = "A oscuras";
        private const string LIGHTNING_IN_PROGRESS_TEXT = "Reto relámpago en curso";
        private const string SURPRISE_EXAM_MY_TURN_TEXT = "Examen sorpresa: responde";
        private const string OTHER_ANSWER_REGISTERED_TEXT = "Respuesta registrada.";

        private const string GENERIC_ACTION_FAILED_MESSAGE = "No se pudo realizar la acción. Intenta de nuevo.";

        private const string TURN_MY_TURN_BRUSH_KEY = "Brush.Turn.MyTurn";
        private const string TURN_OTHER_TURN_BRUSH_KEY = "Brush.Turn.OtherTurn";

        private const int MIN_SECONDS = 0;

        private const string PROP_USER_ID = "UserId";
        private const string PROP_DISPLAY_NAME = "DisplayName";

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;
        private readonly GameplayClientProxy gameplay;
        private readonly QuestionTimerController timer;

        private readonly object lobbyCacheSyncRoot = new object();
        private readonly Dictionary<int, PlayerSummary> lobbyPlayerCacheByUserId =
            new Dictionary<int, PlayerSummary>();

        private int remainingSeconds;
        private int currentTurnTimeLimitSeconds;

        private int? pendingNextTurnTimeLimitSeconds;
        private int? pendingNextTurnTimeLimitSourceUserId;
        private int? pendingNextTurnTimeLimitTargetUserId;

        public QuestionController(
            MatchWindowUiRefs ui,
            MatchSessionState state,
            GameplayClientProxy gameplay,
            QuestionTimerController timer)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            this.timer = timer ?? throw new ArgumentNullException(nameof(timer));

            this.timer.Tick += seconds =>
            {
                remainingSeconds = seconds;
                UpdateTimerText();
            };

            this.timer.Expired += async () => await OnTimerExpiredAsync();
        }

        public void InitializeEmptyUi()
        {
            currentTurnTimeLimitSeconds = 0;

            if (ui.TxtQuestion != null)
            {
                ui.TxtQuestion.Text = MatchConstants.DEFAULT_WAITING_QUESTION_TEXT;
            }

            if (ui.TxtAnswerFeedback != null)
            {
                ui.TxtAnswerFeedback.Text = string.Empty;
            }

            ResetAnswerButtons();
            SetTimerText(MatchConstants.DEFAULT_TIMER_TEXT);

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = false;
            }
        }

        public void ScheduleNextTurnTimeLimitOverride(int seconds, int? sourceTurnUserId, int? targetUserId)
        {
            seconds = NormalizeSeconds(seconds);

            if (pendingNextTurnTimeLimitSeconds.HasValue)
            {
                pendingNextTurnTimeLimitSeconds = Math.Min(pendingNextTurnTimeLimitSeconds.Value, seconds);
            }
            else
            {
                pendingNextTurnTimeLimitSeconds = seconds;
            }

            if (targetUserId.HasValue && targetUserId.Value > 0)
            {
                pendingNextTurnTimeLimitTargetUserId = targetUserId.Value;
                return;
            }

            if (!pendingNextTurnTimeLimitTargetUserId.HasValue &&
                sourceTurnUserId.HasValue &&
                sourceTurnUserId.Value > 0)
            {
                pendingNextTurnTimeLimitSourceUserId = sourceTurnUserId.Value;
            }
        }

        private static int NormalizeSeconds(int seconds)
        {
            return seconds < MIN_SECONDS ? MIN_SECONDS : seconds;
        }

        private int? TryConsumeNextTurnTimeLimitOverride(int targetUserId)
        {
            if (!pendingNextTurnTimeLimitSeconds.HasValue)
            {
                return null;
            }

            if (targetUserId <= 0)
            {
                return null;
            }

            if (!ShouldApplyPendingOverride(targetUserId))
            {
                return null;
            }

            int seconds = pendingNextTurnTimeLimitSeconds.Value;

            pendingNextTurnTimeLimitSeconds = null;
            pendingNextTurnTimeLimitSourceUserId = null;
            pendingNextTurnTimeLimitTargetUserId = null;

            return seconds;
        }

        private bool ShouldApplyPendingOverride(int targetUserId)
        {
            if (pendingNextTurnTimeLimitTargetUserId.HasValue)
            {
                return targetUserId == pendingNextTurnTimeLimitTargetUserId.Value;
            }

            if (pendingNextTurnTimeLimitSourceUserId.HasValue)
            {
                return targetUserId != pendingNextTurnTimeLimitSourceUserId.Value;
            }

            return true;
        }

        public void SetDarknessActive(bool isActive)
        {
            state.IsDarknessActive = isActive;

            if (ui.TurnAvatar != null)
            {
                ui.TurnAvatar.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
            }

            if (isActive)
            {
                ApplyDarknessTurnIdentity();
                return;
            }

            RestoreTurnIdentityFromLobbyOrUiList();
        }

        private void ApplyDarknessTurnIdentity()
        {
            if (ui.TxtTurnPlayerName != null)
            {
                ui.TxtTurnPlayerName.Text = DARKNESS_UNKNOWN_NAME;
            }

            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = DARKNESS_TURN_LABEL;
            }

            if (ui.TurnBannerBackground != null)
            {
                ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource("Brush.Turn.OtherTurn");
            }
        }

        private void RestoreTurnIdentityFromLobbyOrUiList()
        {
            int userId = state.CurrentTurnUserId;

            string nameFromUi = TryResolveDisplayNameFromUiPlayersList(userId);
            PlayerSummary lobbyPlayer = FindLobbyPlayer(userId);

            if (ui.TxtTurnPlayerName != null)
            {
                ui.TxtTurnPlayerName.Text =
                    !string.IsNullOrWhiteSpace(nameFromUi)
                        ? nameFromUi
                        : (lobbyPlayer != null && !string.IsNullOrWhiteSpace(lobbyPlayer.DisplayName)
                            ? lobbyPlayer.DisplayName
                            : BuildFallbackPlayerName(userId));
            }

            if (ui.TurnAvatar != null)
            {
                AvatarAppearance appearance = AvatarMapper.FromLobbyDto(lobbyPlayer != null ? lobbyPlayer.Avatar : null);
                ui.TurnAvatar.Appearance = appearance;
                ui.TurnAvatar.Visibility = Visibility.Visible;
            }
        }

        public void SetBombQuestionUi(bool isActive)
        {
            state.IsBombQuestionActive = isActive;

            if (ui.TxtAnswerFeedback != null)
            {
                if (isActive)
                {
                    ui.TxtAnswerFeedback.Text = MatchConstants.BOMB_UI_MESSAGE;
                    ui.TxtAnswerFeedback.Foreground = Brushes.Gold;
                }
                else
                {
                    if (string.Equals(ui.TxtAnswerFeedback.Text, MatchConstants.BOMB_UI_MESSAGE, StringComparison.Ordinal))
                    {
                        ui.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_SELECT_ANSWER_TEXT;
                        ui.TxtAnswerFeedback.Foreground = Brushes.LightGray;
                    }
                }
            }

            if (ui.TurnBannerBackground != null && state.IsMyTurn)
            {
                ui.TurnBannerBackground.Background = isActive
                    ? MatchConstants.BombTurnBrush
                    : (Brush)ui.Window.FindResource("Brush.Turn.MyTurn");
            }
        }

        public void OnNextQuestion(
            GameplayServiceProxy.PlayerSummary targetPlayer,
            GameplayServiceProxy.QuestionWithAnswersDto question,
            decimal currentChain,
            decimal banked,
            Action<int> highlightPlayer)
        {
            CacheLobbyPlayersSnapshot();

            int targetUserId = targetPlayer != null ? targetPlayer.UserId : 0;
            bool isTargetEliminated = targetUserId > 0 && state.IsEliminated(targetUserId);
            bool isTargetMe = targetUserId == state.MyUserId;

            state.IsMyTurn = isTargetMe && !isTargetEliminated;
            state.CurrentQuestion = question;
            state.CurrentTurnUserId = targetUserId;

            SetBombQuestionUi(false);

            int? timeLimitOverrideSeconds = TryConsumeNextTurnTimeLimitOverride(targetUserId);

            highlightPlayer?.Invoke(targetUserId);

            UpdateChainAndBankUi(currentChain, banked);
            ApplyTurnIdentityUi(targetPlayer);
            ApplyBankButtonState();

            if (state.IsMyTurn)
            {
                StartMyTurnTimer(timeLimitOverrideSeconds);
                ShowQuestionForMyTurn(question);
                return;
            }

            StartOtherTurnUi();
            ShowWaitingTurnUi();
        }


        private void UpdateChainAndBankUi(decimal currentChain, decimal banked)
        {
            if (ui.TxtChain != null)
            {
                ui.TxtChain.Text = currentChain.ToString(MatchConstants.POINTS_FORMAT);
            }

            if (ui.TxtBanked != null)
            {
                ui.TxtBanked.Text = banked.ToString(MatchConstants.POINTS_FORMAT);
            }
        }

        private void ApplyTurnIdentityUi(GameplayServiceProxy.PlayerSummary targetPlayer)
        {
            if (state.IsDarknessActive)
            {
                if (ui.TurnAvatar != null) ui.TurnAvatar.Visibility = Visibility.Collapsed;
                if (ui.TxtTurnPlayerName != null) ui.TxtTurnPlayerName.Text = DARKNESS_UNKNOWN_NAME;
                if (ui.TxtTurnLabel != null) ui.TxtTurnLabel.Text = DARKNESS_TURN_LABEL;
                return;
            }

            ApplyNormalTurnIdentity(targetPlayer);
        }

        private void ApplyNormalTurnIdentity(GameplayServiceProxy.PlayerSummary targetPlayer)
        {
            CacheLobbyPlayersSnapshot();

            int userId = targetPlayer != null && targetPlayer.UserId > 0
                ? targetPlayer.UserId
                : state.CurrentTurnUserId;

            string nameFromUi = TryResolveDisplayNameFromUiPlayersList(userId);
            PlayerSummary lobbyPlayer = FindLobbyPlayer(userId);

            string displayName = ResolveBestDisplayName(nameFromUi, targetPlayer, lobbyPlayer, userId);

            if (ui.TxtTurnPlayerName != null)
            {
                ui.TxtTurnPlayerName.Text = displayName;
            }

            if (ui.TurnAvatar != null)
            {
                AvatarAppearance appearance = null;

                if (targetPlayer != null && targetPlayer.Avatar != null)
                {
                    appearance = AvatarMapper.FromGameplayDto(targetPlayer.Avatar);
                }
                else
                {
                    appearance = AvatarMapper.FromLobbyDto(lobbyPlayer != null ? lobbyPlayer.Avatar : null);
                }

                ui.TurnAvatar.Appearance = appearance;
                ui.TurnAvatar.Visibility = Visibility.Visible;
            }
        }

        private static string ResolveBestDisplayName(
            string nameFromUi,
            GameplayServiceProxy.PlayerSummary gameplayPlayer,
            PlayerSummary lobbyPlayer,
            int userId)
        {
            if (!string.IsNullOrWhiteSpace(nameFromUi))
            {
                return nameFromUi;
            }

            string name = gameplayPlayer != null ? gameplayPlayer.DisplayName : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = lobbyPlayer != null ? lobbyPlayer.DisplayName : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return BuildFallbackPlayerName(userId);
        }

        private static string BuildFallbackPlayerName(int userId)
        {
            if (userId > 0)
            {
                return string.Format(CultureInfo.CurrentCulture, GENERIC_PLAYER_NAME_FORMAT, userId);
            }

            return MatchConstants.DEFAULT_PLAYER_NAME;
        }

        private void CacheLobbyPlayersSnapshot()
        {
            try
            {
                PlayerSummary[] players = state.Match != null ? state.Match.Players : null;
                if (players == null || players.Length == 0)
                {
                    return;
                }

                lock (lobbyCacheSyncRoot)
                {
                    foreach (PlayerSummary p in players)
                    {
                        if (p == null || p.UserId <= 0)
                        {
                            continue;
                        }

                        lobbyPlayerCacheByUserId[p.UserId] = p;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("CacheLobbyPlayersSnapshot failed.", ex);
            }
        }

        private PlayerSummary FindLobbyPlayer(int userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            lock (lobbyCacheSyncRoot)
            {
                if (lobbyPlayerCacheByUserId.TryGetValue(userId, out PlayerSummary cached))
                {
                    return cached;
                }
            }

            PlayerSummary[] players = state.Match != null ? state.Match.Players : null;
            if (players == null)
            {
                return null;
            }

            PlayerSummary found = players.FirstOrDefault(p => p != null && p.UserId == userId);
            if (found != null)
            {
                lock (lobbyCacheSyncRoot)
                {
                    lobbyPlayerCacheByUserId[userId] = found;
                }
            }

            return found;
        }

        private string TryResolveDisplayNameFromUiPlayersList(int userId)
        {
            try
            {
                if (userId <= 0 || ui.LstPlayers == null)
                {
                    return null;
                }

                IEnumerable source = ui.LstPlayers.ItemsSource as IEnumerable;
                if (source == null)
                {
                    source = ui.LstPlayers.Items;
                }

                foreach (object item in source)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    int? itemUserId = TryReadInt(item, PROP_USER_ID);
                    if (!itemUserId.HasValue)
                    {
                        continue;
                    }

                    if (itemUserId.Value != userId)
                    {
                        continue;
                    }

                    string name = TryReadString(item, PROP_DISPLAY_NAME);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("TryResolveDisplayNameFromUiPlayersList failed.", ex);
            }

            return null;
        }

        private static int? TryReadInt(object target, string propertyName)
        {
            try
            {
                if (target == null || string.IsNullOrWhiteSpace(propertyName))
                {
                    return null;
                }

                PropertyInfo prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (prop == null || prop.PropertyType != typeof(int))
                {
                    return null;
                }

                object value = prop.GetValue(target, null);
                if (value is int intValue)
                {
                    return intValue;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private void ApplyTurnBannerOnly()
        {
            if (ui.TurnBannerBackground == null)
            {
                return;
            }

            string key = state.IsMyTurn ? TURN_MY_TURN_BRUSH_KEY : TURN_OTHER_TURN_BRUSH_KEY;

            ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource(key);
        }


        private static string TryReadString(object target, string propertyName)
        {
            try
            {
                if (target == null || string.IsNullOrWhiteSpace(propertyName))
                {
                    return null;
                }

                PropertyInfo prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (prop == null || prop.PropertyType != typeof(string))
                {
                    return null;
                }

                return prop.GetValue(target, null) as string;
            }
            catch (Exception)
            {
                // Ignorar: solo fallback.
            }

            return null;
        }

        private void ApplyBankButtonState()
        {
            if (ui.BtnBank == null)
            {
                return;
            }

            ui.BtnBank.IsEnabled = state.IsMyTurn && !state.IsSurpriseExamActive;
        }

        private void StartMyTurnTimer(int? timeLimitOverrideSeconds)
        {
            if (state.IsDarknessActive)
            {
                ApplyTurnBannerOnly();
            }
            else
            {
                ApplyMyTurnLabelAndBanner();
            }

            int limitSeconds = ResolveTimeLimitSeconds(timeLimitOverrideSeconds);

            currentTurnTimeLimitSeconds = limitSeconds;
            remainingSeconds = limitSeconds;

            UpdateTimerText();

            timer.Stop();
            timer.Start(limitSeconds);
        }


        private void ApplyMyTurnLabelAndBanner()
        {
            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = state.IsSurpriseExamActive
                    ? SURPRISE_EXAM_MY_TURN_TEXT
                    : MatchConstants.TURN_MY_TURN_TEXT;
            }

            if (ui.TurnBannerBackground != null)
            {
                ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource("Brush.Turn.MyTurn");
            }
        }

        private int ResolveTimeLimitSeconds(int? timeLimitOverrideSeconds)
        {
            if (state.IsSurpriseExamActive)
            {
                return MatchConstants.SURPRISE_EXAM_TIME_SECONDS;
            }

            int limitSeconds = MatchConstants.QUESTION_TIME_SECONDS;

            if (timeLimitOverrideSeconds.HasValue)
            {
                limitSeconds = timeLimitOverrideSeconds.Value;
            }

            return limitSeconds;
        }

        private void StartOtherTurnUi()
        {
            currentTurnTimeLimitSeconds = 0;

            if (state.IsDarknessActive)
            {
                ApplyTurnBannerOnly();
            }
            else
            {
                ApplyOtherTurnLabelAndBanner();
            }

            timer.Stop();
            SetTimerText(MatchConstants.DEFAULT_TIMER_TEXT);
        }


        private void ApplyOtherTurnLabelAndBanner()
        {
            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = MatchConstants.TURN_OTHER_PLAYER_TEXT;
            }

            if (ui.TurnBannerBackground != null)
            {
                ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource("Brush.Turn.OtherTurn");
            }
        }

        private void ShowWaitingTurnUi()
        {
            InitializeEmptyUi();

            if (ui.TxtQuestion != null)
            {
                ui.TxtQuestion.Text = MatchConstants.DEFAULT_WAITING_TURN_TEXT;
            }
        }

        private void ShowQuestionForMyTurn(GameplayServiceProxy.QuestionWithAnswersDto question)
        {
            if (question == null)
            {
                Logger.Warn("OnNextQuestion: question is null for my turn.");
                InitializeEmptyUi();
                return;
            }

            if (ui.TxtQuestion != null)
            {
                ui.TxtQuestion.Text = question.Body;
            }

            SetAnswerButtonContent(ui.BtnAnswer1, question.Answers, 0);
            SetAnswerButtonContent(ui.BtnAnswer2, question.Answers, 1);
            SetAnswerButtonContent(ui.BtnAnswer3, question.Answers, 2);
            SetAnswerButtonContent(ui.BtnAnswer4, question.Answers, 3);

            if (ui.TxtAnswerFeedback != null)
            {
                ui.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_SELECT_ANSWER_TEXT;
                ui.TxtAnswerFeedback.Foreground = Brushes.LightGray;
            }

            EnableAnswerButtons(true);
        }

        public void OnLightningQuestion(GameplayServiceProxy.QuestionWithAnswersDto question, int timeLimitSeconds)
        {
            CacheLobbyPlayersSnapshot();

            SetBombQuestionUi(false);

            state.CurrentQuestion = question;

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = false;
            }

            if (!state.IsMyTurn)
            {
                timer.Stop();
                currentTurnTimeLimitSeconds = 0;

                InitializeEmptyUi();

                if (ui.TxtQuestion != null)
                {
                    ui.TxtQuestion.Text = LIGHTNING_IN_PROGRESS_TEXT;
                }

                return;
            }

            if (question == null)
            {
                InitializeEmptyUi();
                return;
            }

            if (ui.TxtQuestion != null)
            {
                ui.TxtQuestion.Text = question.Body;
            }

            SetAnswerButtonContent(ui.BtnAnswer1, question.Answers, 0);
            SetAnswerButtonContent(ui.BtnAnswer2, question.Answers, 1);
            SetAnswerButtonContent(ui.BtnAnswer3, question.Answers, 2);
            SetAnswerButtonContent(ui.BtnAnswer4, question.Answers, 3);

            if (ui.TxtAnswerFeedback != null)
            {
                ui.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_SELECT_ANSWER_TEXT;
                ui.TxtAnswerFeedback.Foreground = Brushes.LightGray;
            }

            EnableAnswerButtons(true);

            int limitSeconds = timeLimitSeconds > 0
                ? timeLimitSeconds
                : MatchConstants.QUESTION_TIME_SECONDS;

            currentTurnTimeLimitSeconds = limitSeconds;
            remainingSeconds = limitSeconds;

            UpdateTimerText();
            timer.Start(limitSeconds);
        }

        public void OnAnswerEvaluated(GameplayServiceProxy.PlayerSummary player, GameplayServiceProxy.AnswerResult result)
        {
            if (ui.TxtAnswerFeedback == null)
            {
                return;
            }

            if (IsMyPlayer(player))
            {
                UpdateMyAnswerStats(result);
                ShowMyAnswerFeedback(result);
                return;
            }

            if (state.IsDarknessActive)
            {
                ShowOtherAnswerRegistered();
                return;
            }

            ShowOtherPlayerAnswerFeedback(player, result);
        }

        private bool IsMyPlayer(GameplayServiceProxy.PlayerSummary player)
        {
            return player != null && player.UserId == state.MyUserId;
        }

        private void UpdateMyAnswerStats(GameplayServiceProxy.AnswerResult result)
        {
            state.MyTotalAnswers++;

            if (result != null && result.IsCorrect)
            {
                state.MyCorrectAnswers++;
            }
        }

        private void ShowMyAnswerFeedback(GameplayServiceProxy.AnswerResult result)
        {
            bool isCorrect = result != null && result.IsCorrect;

            ui.TxtAnswerFeedback.Text = isCorrect
                ? MatchConstants.DEFAULT_CORRECT_TEXT
                : MatchConstants.DEFAULT_INCORRECT_TEXT;

            ui.TxtAnswerFeedback.Foreground = isCorrect
                ? Brushes.LawnGreen
                : Brushes.OrangeRed;
        }

        private void ShowOtherAnswerRegistered()
        {
            ui.TxtAnswerFeedback.Text = OTHER_ANSWER_REGISTERED_TEXT;
            ui.TxtAnswerFeedback.Foreground = Brushes.LightGray;
        }

        private void ShowOtherPlayerAnswerFeedback(GameplayServiceProxy.PlayerSummary player, GameplayServiceProxy.AnswerResult result)
        {
            string name = ResolveOtherPlayerName(player);
            bool otherIsCorrect = result != null && result.IsCorrect;

            ui.TxtAnswerFeedback.Text = otherIsCorrect
                ? string.Format(CultureInfo.CurrentCulture, "{0} respondió correcto.", name)
                : string.Format(CultureInfo.CurrentCulture, "{0} respondió incorrecto.", name);

            ui.TxtAnswerFeedback.Foreground = Brushes.LightGray;
        }

        private string ResolveOtherPlayerName(GameplayServiceProxy.PlayerSummary player)
        {
            string displayName = player != null ? player.DisplayName : null;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            int userId = player != null ? player.UserId : 0;

            string fromUi = TryResolveDisplayNameFromUiPlayersList(userId);
            if (!string.IsNullOrWhiteSpace(fromUi))
            {
                return fromUi;
            }

            PlayerSummary lobby = FindLobbyPlayer(userId);

            displayName = lobby != null ? lobby.DisplayName : null;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return MatchConstants.DEFAULT_OTHER_PLAYER_NAME;
        }

        public void OnBankUpdated(GameplayServiceProxy.BankState bank)
        {
            if (bank == null)
            {
                return;
            }

            if (ui.TxtChain != null)
            {
                ui.TxtChain.Text = bank.CurrentChain.ToString(MatchConstants.POINTS_FORMAT);
            }

            if (ui.TxtBanked != null)
            {
                ui.TxtBanked.Text = bank.BankedPoints.ToString(MatchConstants.POINTS_FORMAT);
            }
        }

        public async Task SubmitAnswerFromButtonAsync(Button button)
        {
            if (!state.IsMyTurn || button == null)
            {
                return;
            }

            GameplayServiceProxy.AnswerDto answer = button.Tag as GameplayServiceProxy.AnswerDto;
            if (answer == null)
            {
                return;
            }

            DisableInteractionsForTurn();

            try
            {
                int questionId = state.CurrentQuestion != null ? state.CurrentQuestion.QuestionId : 0;

                var request = new GameplayServiceProxy.SubmitAnswerRequest
                {
                    Token = state.Token,
                    MatchId = state.Match.MatchId,
                    QuestionId = questionId,
                    AnswerText = answer.Text,
                    ResponseTime = TimeSpan.Zero
                };

                await gameplay.SubmitAnswerAsync(request);
            }
            catch (Exception ex)
            {
                Logger.Warn("SubmitAnswerFromButtonAsync error.", ex);
            }
        }

        public async Task BankAsync()
        {
            if (!state.IsMyTurn)
            {
                return;
            }

            if (state.IsSurpriseExamActive)
            {
                MessageBox.Show(
                    MatchConstants.SURPRISE_EXAM_BANK_BLOCKED_MESSAGE,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = false;
            }

            timer.Stop();

            try
            {
                var request = new GameplayServiceProxy.BankRequest
                {
                    Token = state.Token,
                    MatchId = state.Match.MatchId
                };

                GameplayServiceProxy.BankResponse response = await gameplay.BankAsync(request);
                if (response != null)
                {
                    OnBankUpdated(response.Bank);
                }
            }
            catch (FaultException ex)
            {
                Logger.Warn("BankAsync service fault.", ex);

                string message = ResolveServiceFaultMessage(ex);

                MessageBox.Show(
                    message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (state.IsMyTurn && remainingSeconds > 0)
                {
                    timer.Start(remainingSeconds);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("BankAsync error.", ex);
            }
            finally
            {
                if (ui.BtnBank != null)
                {
                    ui.BtnBank.IsEnabled = state.IsMyTurn && !state.IsSurpriseExamActive;
                }
            }
        }

        private static string ResolveServiceFaultMessage(FaultException ex)
        {
            if (ex == null)
            {
                return GENERIC_ACTION_FAILED_MESSAGE;
            }

            try
            {
                var detailProperty = ex.GetType().GetProperty("Detail");
                if (detailProperty != null)
                {
                    object detail = detailProperty.GetValue(ex, null);
                    if (detail != null)
                    {
                        var messageProperty = detail.GetType().GetProperty("Message");
                        if (messageProperty != null)
                        {
                            string detailMessage = messageProperty.GetValue(detail, null) as string;
                            if (!string.IsNullOrWhiteSpace(detailMessage))
                            {
                                return detailMessage;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback abajo.
            }

            if (!string.IsNullOrWhiteSpace(ex.Message))
            {
                return ex.Message;
            }

            return GENERIC_ACTION_FAILED_MESSAGE;
        }

        public void OnEliminated(bool isMe)
        {
            if (!isMe)
            {
                return;
            }

            MessageBox.Show(
                "Has sido eliminado de la ronda.\nSeguirás viendo la partida como espectador.",
                MatchConstants.GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            state.IsMyTurn = false;
            timer.Stop();
            DisableInteractionsForTurn();

            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = "Eliminado (espectador)";
            }
        }

        public void SetSurpriseExamActive(bool isActive)
        {
            state.IsSurpriseExamActive = isActive;

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = state.IsMyTurn && !state.IsSurpriseExamActive;
            }
        }

        private async Task OnTimerExpiredAsync()
        {
            if (!state.IsMyTurn)
            {
                return;
            }

            DisableInteractionsForTurn();

            if (ui.TxtAnswerFeedback != null)
            {
                ui.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_TIMEOUT_TEXT;
                ui.TxtAnswerFeedback.Foreground = Brushes.OrangeRed;
            }

            await SendTimeoutAnswerAsync();
        }

        private async Task SendTimeoutAnswerAsync()
        {
            if (!state.IsMyTurn)
            {
                return;
            }

            try
            {
                int questionId = state.CurrentQuestion != null ? state.CurrentQuestion.QuestionId : 0;

                int limitSeconds = currentTurnTimeLimitSeconds > 0
                    ? currentTurnTimeLimitSeconds
                    : (state.IsSurpriseExamActive
                        ? MatchConstants.SURPRISE_EXAM_TIME_SECONDS
                        : MatchConstants.QUESTION_TIME_SECONDS);

                var request = new GameplayServiceProxy.SubmitAnswerRequest
                {
                    Token = state.Token,
                    MatchId = state.Match.MatchId,
                    QuestionId = questionId,
                    AnswerText = string.Empty,
                    ResponseTime = TimeSpan.FromSeconds(limitSeconds)
                };

                await gameplay.SubmitAnswerAsync(request);
            }
            catch (Exception ex)
            {
                Logger.Warn("SendTimeoutAnswerAsync error.", ex);
            }
        }

        private void DisableInteractionsForTurn()
        {
            EnableAnswerButtons(false);

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = false;
            }

            timer.Stop();
        }

        private void EnableAnswerButtons(bool isEnabled)
        {
            if (ui.BtnAnswer1 != null) ui.BtnAnswer1.IsEnabled = isEnabled;
            if (ui.BtnAnswer2 != null) ui.BtnAnswer2.IsEnabled = isEnabled;
            if (ui.BtnAnswer3 != null) ui.BtnAnswer3.IsEnabled = isEnabled;
            if (ui.BtnAnswer4 != null) ui.BtnAnswer4.IsEnabled = isEnabled;
        }

        private void ResetAnswerButtons()
        {
            ResetAnswerButton(ui.BtnAnswer1);
            ResetAnswerButton(ui.BtnAnswer2);
            ResetAnswerButton(ui.BtnAnswer3);
            ResetAnswerButton(ui.BtnAnswer4);
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

        private static void SetAnswerButtonContent(Button button, GameplayServiceProxy.AnswerDto[] answers, int index)
        {
            if (button == null)
            {
                return;
            }

            if (answers != null && index < answers.Length)
            {
                GameplayServiceProxy.AnswerDto answer = answers[index];

                button.Content = answer.Text;
                button.Tag = answer;
                button.Visibility = Visibility.Visible;
                button.IsEnabled = true;
                button.Background = Brushes.Transparent;
                return;
            }

            button.Content = string.Empty;
            button.Tag = null;
            button.Visibility = Visibility.Collapsed;
        }

        private void UpdateTimerText()
        {
            if (ui.TxtTimer == null)
            {
                return;
            }

            if (remainingSeconds < 0)
            {
                ui.TxtTimer.Text = MatchConstants.DEFAULT_TIMER_TEXT;
                return;
            }

            ui.TxtTimer.Text = TimeSpan.FromSeconds(remainingSeconds).ToString(MatchConstants.TIMER_FORMAT);
        }

        private void SetTimerText(string text)
        {
            if (ui.TxtTimer != null)
            {
                ui.TxtTimer.Text = text;
            }
        }
    }
}
