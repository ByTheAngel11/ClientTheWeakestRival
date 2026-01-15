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
using WPFTheWeakestRival.Properties.Langs;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class QuestionController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(QuestionController));

        private const string LOG_CACHE_LOBBY_SNAPSHOT_FAILED = "CacheLobbyPlayersSnapshot failed.";
        private const string LOG_TRY_RESOLVE_UI_NAME_FAILED = "TryResolveDisplayNameFromUiPlayersList failed.";
        private const string LOG_TRY_READ_INT_FAILED = "TryReadInt failed.";
        private const string LOG_TRY_READ_STRING_FAILED = "TryReadString failed.";
        private const string LOG_ON_NEXT_QUESTION_NULL_FOR_MY_TURN = "OnNextQuestion: question is null for my turn.";
        private const string LOG_SUBMIT_ANSWER_FAILED = "SubmitAnswerFromButtonAsync error.";
        private const string LOG_BANK_SERVICE_FAULT = "BankAsync service fault.";
        private const string LOG_BANK_FAILED = "BankAsync error.";
        private const string LOG_RESOLVE_SERVICE_FAULT_FAILED = "ResolveServiceFaultMessage failed.";
        private const string LOG_SEND_TIMEOUT_FAILED = "SendTimeoutAnswerAsync error.";

        private const string TURN_MY_TURN_BRUSH_KEY = "Brush.Turn.MyTurn";
        private const string TURN_OTHER_TURN_BRUSH_KEY = "Brush.Turn.OtherTurn";

        private const int MIN_SECONDS = 0;

        private const string PROP_USER_ID = "UserId";
        private const string PROP_DISPLAY_NAME = "DisplayName";

        private const int ANSWER_INDEX_FIRST = 0;
        private const int ANSWER_INDEX_SECOND = 1;
        private const int ANSWER_INDEX_THIRD = 2;
        private const int ANSWER_INDEX_FOURTH = 3;

        private readonly MatchWindowUiRefs uiMatchWindow;
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
            this.uiMatchWindow = ui ?? throw new ArgumentNullException(nameof(ui));
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

            if (uiMatchWindow.TxtQuestion != null)
            {
                uiMatchWindow.TxtQuestion.Text = MatchConstants.DEFAULT_WAITING_QUESTION_TEXT;
            }

            if (uiMatchWindow.TxtAnswerFeedback != null)
            {
                uiMatchWindow.TxtAnswerFeedback.Text = string.Empty;
            }

            ResetAnswerButtons();
            SetTimerText(MatchConstants.DEFAULT_TIMER_TEXT);

            if (uiMatchWindow.BtnBank != null)
            {
                uiMatchWindow.BtnBank.IsEnabled = false;
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

            if (uiMatchWindow.TurnAvatar != null)
            {
                uiMatchWindow.TurnAvatar.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
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
            if (uiMatchWindow.TxtTurnPlayerName != null)
            {
                uiMatchWindow.TxtTurnPlayerName.Text = Lang.darknessUnknownPlayerName;
            }

            if (uiMatchWindow.TxtTurnLabel != null)
            {
                uiMatchWindow.TxtTurnLabel.Text = Lang.msgSpecialEventDarknessTitle;
            }

            if (uiMatchWindow.TurnBannerBackground != null)
            {
                uiMatchWindow.TurnBannerBackground.Background = (Brush)uiMatchWindow.Window.FindResource(TURN_OTHER_TURN_BRUSH_KEY);
            }
        }

        private void RestoreTurnIdentityFromLobbyOrUiList()
        {
            int userId = state.CurrentTurnUserId;

            string nameFromUi = TryResolveDisplayNameFromUiPlayersList(userId);
            PlayerSummary lobbyPlayer = FindLobbyPlayer(userId);

            if (uiMatchWindow.TxtTurnPlayerName != null)
            {
                uiMatchWindow.TxtTurnPlayerName.Text =
                    !string.IsNullOrWhiteSpace(nameFromUi)
                        ? nameFromUi
                        : (lobbyPlayer != null && !string.IsNullOrWhiteSpace(lobbyPlayer.DisplayName)
                            ? lobbyPlayer.DisplayName
                            : BuildFallbackPlayerName(userId));
            }

            if (uiMatchWindow.TurnAvatar != null)
            {
                AvatarAppearance appearance = AvatarMapper.FromLobbyDto(lobbyPlayer != null ? lobbyPlayer.Avatar : null);
                uiMatchWindow.TurnAvatar.Appearance = appearance;
                uiMatchWindow.TurnAvatar.Visibility = Visibility.Visible;
            }
        }

        public void SetBombQuestionUi(bool isActive)
        {
            state.IsBombQuestionActive = isActive;

            if (uiMatchWindow.TxtAnswerFeedback != null)
            {
                if (isActive)
                {
                    uiMatchWindow.TxtAnswerFeedback.Text = MatchConstants.BOMB_UI_MESSAGE;
                    uiMatchWindow.TxtAnswerFeedback.Foreground = Brushes.Gold;
                }
                else
                {
                    if (string.Equals(uiMatchWindow.TxtAnswerFeedback.Text, MatchConstants.BOMB_UI_MESSAGE, StringComparison.Ordinal))
                    {
                        uiMatchWindow.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_SELECT_ANSWER_TEXT;
                        uiMatchWindow.TxtAnswerFeedback.Foreground = Brushes.LightGray;
                    }
                }
            }

            if (uiMatchWindow.TurnBannerBackground != null && state.IsMyTurn)
            {
                uiMatchWindow.TurnBannerBackground.Background = isActive
                    ? MatchConstants.BombTurnBrush
                    : (Brush)uiMatchWindow.Window.FindResource(TURN_MY_TURN_BRUSH_KEY);
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
            if (uiMatchWindow.TxtChain != null)
            {
                uiMatchWindow.TxtChain.Text = currentChain.ToString(MatchConstants.POINTS_FORMAT);
            }

            if (uiMatchWindow.TxtBanked != null)
            {
                uiMatchWindow.TxtBanked.Text = banked.ToString(MatchConstants.POINTS_FORMAT);
            }
        }

        private void ApplyTurnIdentityUi(GameplayServiceProxy.PlayerSummary targetPlayer)
        {
            if (state.IsDarknessActive)
            {
                if (uiMatchWindow.TurnAvatar != null) uiMatchWindow.TurnAvatar.Visibility = Visibility.Collapsed;
                if (uiMatchWindow.TxtTurnPlayerName != null) uiMatchWindow.TxtTurnPlayerName.Text = Lang.darknessUnknownPlayerName;
                if (uiMatchWindow.TxtTurnLabel != null) uiMatchWindow.TxtTurnLabel.Text = Lang.msgSpecialEventDarknessTitle;
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

            if (uiMatchWindow.TxtTurnPlayerName != null)
            {
                uiMatchWindow.TxtTurnPlayerName.Text = displayName;
            }

            if (uiMatchWindow.TurnAvatar != null)
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

                uiMatchWindow.TurnAvatar.Appearance = appearance;
                uiMatchWindow.TurnAvatar.Visibility = Visibility.Visible;
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
                return string.Format(CultureInfo.CurrentCulture, Lang.playerWithIdFormat, userId);
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
                Logger.Warn(LOG_CACHE_LOBBY_SNAPSHOT_FAILED, ex);
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
                if (userId <= 0 || uiMatchWindow.LstPlayers == null)
                {
                    return null;
                }

                IEnumerable source = uiMatchWindow.LstPlayers.ItemsSource as IEnumerable;
                if (source == null)
                {
                    source = uiMatchWindow.LstPlayers.Items;
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
                Logger.Warn(LOG_TRY_RESOLVE_UI_NAME_FAILED, ex);
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
            catch (Exception ex)
            {
                Logger.Warn(LOG_TRY_READ_INT_FAILED, ex);
            }

            return null;
        }

        private void ApplyTurnBannerOnly()
        {
            if (uiMatchWindow.TurnBannerBackground == null)
            {
                return;
            }

            string key = state.IsMyTurn ? TURN_MY_TURN_BRUSH_KEY : TURN_OTHER_TURN_BRUSH_KEY;

            uiMatchWindow.TurnBannerBackground.Background = (Brush)uiMatchWindow.Window.FindResource(key);
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
            catch (Exception ex)
            {
                Logger.Warn(LOG_TRY_READ_STRING_FAILED, ex);
            }

            return null;
        }

        private void ApplyBankButtonState()
        {
            if (uiMatchWindow.BtnBank == null)
            {
                return;
            }

            uiMatchWindow.BtnBank.IsEnabled = state.IsMyTurn && !state.IsSurpriseExamActive;
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
            if (uiMatchWindow.TxtTurnLabel != null)
            {
                uiMatchWindow.TxtTurnLabel.Text = state.IsSurpriseExamActive
                    ? Lang.surpriseExamMyTurnText
                    : MatchConstants.TURN_MY_TURN_TEXT;
            }

            if (uiMatchWindow.TurnBannerBackground != null)
            {
                uiMatchWindow.TurnBannerBackground.Background = (Brush)uiMatchWindow.Window.FindResource(TURN_MY_TURN_BRUSH_KEY);
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
            if (uiMatchWindow.TxtTurnLabel != null)
            {
                uiMatchWindow.TxtTurnLabel.Text = MatchConstants.TURN_OTHER_PLAYER_TEXT;
            }

            if (uiMatchWindow.TurnBannerBackground != null)
            {
                uiMatchWindow.TurnBannerBackground.Background = (Brush)uiMatchWindow.Window.FindResource(TURN_OTHER_TURN_BRUSH_KEY);
            }
        }

        private void ShowWaitingTurnUi()
        {
            InitializeEmptyUi();

            if (uiMatchWindow.TxtQuestion != null)
            {
                uiMatchWindow.TxtQuestion.Text = MatchConstants.DEFAULT_WAITING_TURN_TEXT;
            }
        }

        private void ShowQuestionForMyTurn(GameplayServiceProxy.QuestionWithAnswersDto question)
        {
            if (question == null)
            {
                Logger.Warn(LOG_ON_NEXT_QUESTION_NULL_FOR_MY_TURN);
                InitializeEmptyUi();
                return;
            }

            if (uiMatchWindow.TxtQuestion != null)
            {
                uiMatchWindow.TxtQuestion.Text = question.Body;
            }

            SetAnswerButtonContent(uiMatchWindow.BtnAnswer1, question.Answers, ANSWER_INDEX_FIRST);
            SetAnswerButtonContent(uiMatchWindow.BtnAnswer2, question.Answers, ANSWER_INDEX_SECOND);
            SetAnswerButtonContent(uiMatchWindow.BtnAnswer3, question.Answers, ANSWER_INDEX_THIRD);
            SetAnswerButtonContent(uiMatchWindow.BtnAnswer4, question.Answers, ANSWER_INDEX_FOURTH);

            if (uiMatchWindow.TxtAnswerFeedback != null)
            {
                uiMatchWindow.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_SELECT_ANSWER_TEXT;
                uiMatchWindow.TxtAnswerFeedback.Foreground = Brushes.LightGray;
            }

            EnableAnswerButtons(true);
        }

        public void OnLightningQuestion(GameplayServiceProxy.QuestionWithAnswersDto question, int timeLimitSeconds)
        {
            CacheLobbyPlayersSnapshot();

            SetBombQuestionUi(false);

            state.CurrentQuestion = question;

            if (uiMatchWindow.BtnBank != null)
            {
                uiMatchWindow.BtnBank.IsEnabled = false;
            }

            if (!state.IsMyTurn)
            {
                timer.Stop();
                currentTurnTimeLimitSeconds = 0;

                InitializeEmptyUi();

                if (uiMatchWindow.TxtQuestion != null)
                {
                    uiMatchWindow.TxtQuestion.Text = Lang.lightningInProgressLabel;
                }

                return;
            }

            if (question == null)
            {
                InitializeEmptyUi();
                return;
            }

            if (uiMatchWindow.TxtQuestion != null)
            {
                uiMatchWindow.TxtQuestion.Text = question.Body;
            }

            SetAnswerButtonContent(uiMatchWindow.BtnAnswer1, question.Answers, ANSWER_INDEX_FIRST);
            SetAnswerButtonContent(uiMatchWindow.BtnAnswer2, question.Answers, ANSWER_INDEX_SECOND);
            SetAnswerButtonContent(uiMatchWindow.BtnAnswer3, question.Answers, ANSWER_INDEX_THIRD);
            SetAnswerButtonContent(uiMatchWindow.BtnAnswer4, question.Answers, ANSWER_INDEX_FOURTH);

            if (uiMatchWindow.TxtAnswerFeedback != null)
            {
                uiMatchWindow.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_SELECT_ANSWER_TEXT;
                uiMatchWindow.TxtAnswerFeedback.Foreground = Brushes.LightGray;
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
            if (uiMatchWindow.TxtAnswerFeedback == null)
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

            uiMatchWindow.TxtAnswerFeedback.Text = isCorrect
                ? MatchConstants.DEFAULT_CORRECT_TEXT
                : MatchConstants.DEFAULT_INCORRECT_TEXT;

            uiMatchWindow.TxtAnswerFeedback.Foreground = isCorrect
                ? Brushes.LawnGreen
                : Brushes.OrangeRed;
        }

        private void ShowOtherAnswerRegistered()
        {
            uiMatchWindow.TxtAnswerFeedback.Text = Lang.otherAnswerRegisteredText;
            uiMatchWindow.TxtAnswerFeedback.Foreground = Brushes.LightGray;
        }

        private void ShowOtherPlayerAnswerFeedback(GameplayServiceProxy.PlayerSummary player, GameplayServiceProxy.AnswerResult result)
        {
            string name = ResolveOtherPlayerName(player);
            bool otherIsCorrect = result != null && result.IsCorrect;

            uiMatchWindow.TxtAnswerFeedback.Text = otherIsCorrect
                ? string.Format(CultureInfo.CurrentCulture, Lang.otherPlayerAnsweredCorrectFormat, name)
                : string.Format(CultureInfo.CurrentCulture, Lang.otherPlayerAnsweredIncorrectFormat, name);

            uiMatchWindow.TxtAnswerFeedback.Foreground = Brushes.LightGray;
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

            if (uiMatchWindow.TxtChain != null)
            {
                uiMatchWindow.TxtChain.Text = bank.CurrentChain.ToString(MatchConstants.POINTS_FORMAT);
            }

            if (uiMatchWindow.TxtBanked != null)
            {
                uiMatchWindow.TxtBanked.Text = bank.BankedPoints.ToString(MatchConstants.POINTS_FORMAT);
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
                Logger.Warn(LOG_SUBMIT_ANSWER_FAILED, ex);
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
                    Lang.surpriseExamBankBlockedMessage,
                    Lang.gameMessageTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (uiMatchWindow.BtnBank != null)
            {
                uiMatchWindow.BtnBank.IsEnabled = false;
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
                Logger.Warn(LOG_BANK_SERVICE_FAULT, ex);

                string message = ResolveServiceFaultMessage(ex);

                MessageBox.Show(
                    message,
                    Lang.gameMessageTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (state.IsMyTurn && remainingSeconds > 0)
                {
                    timer.Start(remainingSeconds);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_BANK_FAILED, ex);
            }
            finally
            {
                if (uiMatchWindow.BtnBank != null)
                {
                    uiMatchWindow.BtnBank.IsEnabled = state.IsMyTurn && !state.IsSurpriseExamActive;
                }
            }
        }

        private static string ResolveServiceFaultMessage(FaultException ex)
        {
            if (ex == null)
            {
                return Lang.genericActionFailedMessage;
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
            catch (Exception innerEx)
            {
                Logger.Warn(LOG_RESOLVE_SERVICE_FAULT_FAILED, innerEx);
            }

            return Lang.genericActionFailedMessage;
        }

        public void OnEliminated(bool isMe)
        {
            if (!isMe)
            {
                return;
            }

            MessageBox.Show(
                Lang.eliminatedSpectatorMessage,
                Lang.gameMessageTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            state.IsMyTurn = false;
            timer.Stop();
            DisableInteractionsForTurn();

            if (uiMatchWindow.TxtTurnLabel != null)
            {
                uiMatchWindow.TxtTurnLabel.Text = Lang.eliminatedSpectatorLabel;
            }
        }

        public void SetSurpriseExamActive(bool isActive)
        {
            state.IsSurpriseExamActive = isActive;

            if (uiMatchWindow.BtnBank != null)
            {
                uiMatchWindow.BtnBank.IsEnabled = state.IsMyTurn && !state.IsSurpriseExamActive;
            }
        }

        private async Task OnTimerExpiredAsync()
        {
            if (!state.IsMyTurn)
            {
                return;
            }

            DisableInteractionsForTurn();

            if (uiMatchWindow.TxtAnswerFeedback != null)
            {
                uiMatchWindow.TxtAnswerFeedback.Text = MatchConstants.DEFAULT_TIMEOUT_TEXT;
                uiMatchWindow.TxtAnswerFeedback.Foreground = Brushes.OrangeRed;
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
                Logger.Warn(LOG_SEND_TIMEOUT_FAILED, ex);
            }
        }

        private void DisableInteractionsForTurn()
        {
            EnableAnswerButtons(false);

            if (uiMatchWindow.BtnBank != null)
            {
                uiMatchWindow.BtnBank.IsEnabled = false;
            }

            timer.Stop();
        }

        private void EnableAnswerButtons(bool isEnabled)
        {
            if (uiMatchWindow.BtnAnswer1 != null) uiMatchWindow.BtnAnswer1.IsEnabled = isEnabled;
            if (uiMatchWindow.BtnAnswer2 != null) uiMatchWindow.BtnAnswer2.IsEnabled = isEnabled;
            if (uiMatchWindow.BtnAnswer3 != null) uiMatchWindow.BtnAnswer3.IsEnabled = isEnabled;
            if (uiMatchWindow.BtnAnswer4 != null) uiMatchWindow.BtnAnswer4.IsEnabled = isEnabled;
        }

        private void ResetAnswerButtons()
        {
            ResetAnswerButton(uiMatchWindow.BtnAnswer1);
            ResetAnswerButton(uiMatchWindow.BtnAnswer2);
            ResetAnswerButton(uiMatchWindow.BtnAnswer3);
            ResetAnswerButton(uiMatchWindow.BtnAnswer4);
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
            if (uiMatchWindow.TxtTimer == null)
            {
                return;
            }

            if (remainingSeconds < 0)
            {
                uiMatchWindow.TxtTimer.Text = MatchConstants.DEFAULT_TIMER_TEXT;
                return;
            }

            uiMatchWindow.TxtTimer.Text = TimeSpan.FromSeconds(remainingSeconds).ToString(MatchConstants.TIMER_FORMAT);
        }

        private void SetTimerText(string text)
        {
            if (uiMatchWindow.TxtTimer != null)
            {
                uiMatchWindow.TxtTimer.Text = text;
            }
        }
    }
}
