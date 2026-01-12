using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;
using WildcardFault = WPFTheWeakestRival.WildcardService.ServiceFault;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchSessionCoordinator : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchSessionCoordinator));

        private const string LegacyDarknessStartCode = "DARKNESS_STARTED";
        private const string LegacyDarknessEndCode = "DARKNESS_ENDED";

        private const string DarkModeStartCode = "DARK_MODE_STARTED";
        private const string DarkModeEndCode = "DARK_MODE_ENDED";
        private const string DarkModeVoteRevealCode = "DARK_MODE_VOTE_REVEAL";

        private const string DarknessKeywordEs = "oscuras";
        private const string DarknessUnknownName = "???";
        private const string DarknessTurnLabel = "A oscuras";

        private const string DarkModeVoteRevealTitle = "A oscuras";
        private const string DarkModeVoteRevealDescription = "Voto revelado.";

        private const string SabotageCode = "SABOTAGE";
        private const string SabotageUsedCode = "SABOTAGE_USED";
        private const string SabotageAppliedCode = "SABOTAGE_APPLIED";
        private const string SabotageKeywordEs = "sabotaje";

        private const int SabotageTimeSeconds = 15;

        private const int FinalPlayersCount = 2;
        private const string FinalPhaseLabelText = "Final 1 vs 1";

        private const string GenericPlayerNameTemplate = "Jugador {0}";
        private const string RevealVoteTemplate = "Votaste por: {0}";

        private const string VotePhaseLogTemplate = "OnServerVotePhaseStarted. MatchId={0}, TimeLimitSeconds={1}";
        private const string SpecialEventLogTemplate = "OnSpecialEvent. MatchId={0}, Name='{1}', Desc='{2}'";

        private const string LightningSuccessTemplate = "¡Has completado el reto relámpago! Respuestas correctas: {0}.";
        private const string LightningFailTemplate = "Reto relámpago finalizado. Respuestas correctas: {0}.";
        private const string MatchFinishedLabelText = "Partida finalizada";

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;

        private readonly OverlayController overlay;
        private readonly WildcardController wildcards;
        private readonly TurnOrderController turns;
        private readonly QuestionTimerController timer;

        private GameplayClientProxy gameplay;
        private GameplayCallbackBridge callbackBridge;
        private QuestionController questions;
        private MatchDialogController dialogs;

        private int lightningTimeLimitSeconds;

        public MatchSessionCoordinator(MatchWindowUiRefs ui, MatchSessionState state)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));

            overlay = new OverlayController(this.ui);
            wildcards = new WildcardController(this.ui, this.state);
            turns = new TurnOrderController(this.ui, this.state);
            timer = new QuestionTimerController(TimeSpan.FromSeconds(MatchConstants.TIMER_INTERVAL_SECONDS));
        }

        public async Task InitializeAsync()
        {
            InitializeUi();

            await wildcards.LoadAsync();

            InitializeGameplayClient();

            await JoinMatchAsync();
            await EnsureMatchStartedAsync();

            RefreshWildcardUseState();
        }

        private void InitializeUi()
        {
            if (ui.TxtMatchCodeSmall != null)
            {
                string code = string.IsNullOrWhiteSpace(state.Match.MatchCode)
                    ? MatchConstants.DEFAULT_MATCH_CODE_TEXT
                    : state.Match.MatchCode;

                ui.TxtMatchCodeSmall.Text = MatchConstants.DEFAULT_MATCH_CODE_PREFIX + code;
            }

            turns.InitializePlayers();

            if (ui.TxtChain != null) ui.TxtChain.Text = MatchConstants.DEFAULT_CHAIN_INITIAL_VALUE;
            if (ui.TxtBanked != null) ui.TxtBanked.Text = MatchConstants.DEFAULT_BANKED_INITIAL_VALUE;
            if (ui.TxtTurnPlayerName != null) ui.TxtTurnPlayerName.Text = MatchConstants.DEFAULT_PLAYER_NAME;
            if (ui.TxtTurnLabel != null) ui.TxtTurnLabel.Text = MatchConstants.DEFAULT_WAITING_MATCH_TEXT;
            if (ui.TxtTimer != null) ui.TxtTimer.Text = MatchConstants.DEFAULT_TIMER_TEXT;

            wildcards.InitializeEmpty();
            UpdatePhaseLabel();
        }

        private void InitializeGameplayClient()
        {
            callbackBridge = new GameplayCallbackBridge(ui.Window.Dispatcher);

            callbackBridge.NextQuestion += (matchId, targetPlayer, question, chain, banked) =>
                HandleNextQuestion(targetPlayer, question, chain, banked);

            callbackBridge.AnswerEvaluated += (matchId, player, result) =>
                questions.OnAnswerEvaluated(player, result);

            callbackBridge.BankUpdated += (matchId, bank) =>
                questions.OnBankUpdated(bank);

            callbackBridge.VotePhaseStarted += async (matchId, timeLimit) =>
                await HandleVotePhaseStartedAsync(matchId, timeLimit);

            callbackBridge.Elimination += (matchId, eliminated) =>
                HandleElimination(eliminated);

            callbackBridge.SpecialEvent += async (matchId, name, description) =>
                await HandleSpecialEventAsync(matchId, name, description);

            callbackBridge.CoinFlipResolved += (matchId, coinFlip) =>
                HandleCoinFlip(coinFlip);

            callbackBridge.DuelCandidates += async (matchId, duelCandidates) =>
                await HandleDuelCandidatesAsync(duelCandidates);

            callbackBridge.MatchFinished += (matchId, winner) =>
                HandleMatchFinished(winner);

            callbackBridge.LightningChallengeStarted += (m, r, tp, tq, ts) =>
                HandleLightningChallengeStarted(tp, ts);

            callbackBridge.LightningChallengeQuestion += (m, r, qi, qq) =>
                HandleLightningChallengeQuestion(qq);

            callbackBridge.LightningChallengeFinished += async (m, r, ca, ok) =>
                await HandleLightningChallengeFinishedAsync(ca, ok);

            callbackBridge.TurnOrderInitialized += async (matchId, turnOrder) =>
                await HandleTurnOrderInitializedAsync(turnOrder);

            callbackBridge.TurnOrderChanged += async (matchId, turnOrder, reason) =>
                await HandleTurnOrderChangedAsync(turnOrder, reason);

            var instanceContext = new InstanceContext(callbackBridge);

            var client = new GameplayServiceProxy.GameplayServiceClient(
                instanceContext,
                "WSDualHttpBinding_IGameplayService");

            gameplay = new GameplayClientProxy(client);

            questions = new QuestionController(ui, state, gameplay, timer);
            questions.InitializeEmptyUi();

            dialogs = new MatchDialogController(ui, state, gameplay);
        }

        private async Task JoinMatchAsync()
        {
            try
            {
                var request = new GameplayServiceProxy.GameplayJoinMatchRequest
                {
                    Token = state.Token,
                    MatchId = state.Match.MatchId
                };

                await gameplay.JoinMatchAsync(request);
            }
            catch (Exception ex)
            {
                Logger.Warn("JoinMatchAsync error.", ex);

                MessageBox.Show(
                    ex.Message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task EnsureMatchStartedAsync()
        {
            try
            {
                var request = new GameplayServiceProxy.GameplayStartMatchRequest
                {
                    Token = state.Token,
                    MatchId = state.Match.MatchId,
                    Difficulty = DifficultyMapper.MapDifficultyToByte(state.Match.Config != null ? state.Match.Config.DifficultyCode : null),
                    LocaleCode = MatchConstants.DEFAULT_LOCALE,
                    MaxQuestions = MatchConstants.MAX_QUESTIONS,
                    MatchDbId = state.MatchDbId,
                    ExpectedPlayerUserIds = BuildExpectedPlayerIds()
                };

                await gameplay.StartMatchAsync(request);
            }
            catch (FaultException<WildcardFault> ex)
            {
                if (ex.Detail != null &&
                    string.Equals(ex.Detail.Code, MatchConstants.FAULT_CODE_MATCH_ALREADY_STARTED, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Logger.Warn("EnsureMatchStartedAsync Fault.", ex);

                MessageBox.Show(
                    ex.Detail != null ? ex.Detail.Message : ex.Message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Warn("EnsureMatchStartedAsync error.", ex);

                MessageBox.Show(
                    ex.Message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private int[] BuildExpectedPlayerIds()
        {
            PlayerSummary[] lobbyPlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();

            return lobbyPlayers
                .Where(p => p != null && p.UserId > 0)
                .Select(p => p.UserId)
                .Distinct()
                .ToArray();
        }

        private void HandleNextQuestion(
            GameplayServiceProxy.PlayerSummary targetPlayer,
            GameplayServiceProxy.QuestionWithAnswersDto question,
            decimal chain,
            decimal banked)
        {
            DetectAndApplyFinalPhaseIfApplicable();

            if (!state.IsInFinalPhase())
            {
                state.CurrentPhase = state.IsSurpriseExamActive || state.IsDarknessActive
                    ? MatchPhase.SpecialEvent
                    : MatchPhase.NormalRound;
            }

            UpdatePhaseLabel();

            questions.OnNextQuestion(
                targetPlayer,
                question,
                chain,
                banked,
                userId => turns.TryHighlightPlayerInList(userId));

            RefreshWildcardUseState();
        }

        private async Task HandleVotePhaseStartedAsync(Guid matchId, TimeSpan timeLimit)
        {
            DetectAndApplyFinalPhaseIfApplicable();

            if (state.IsInFinalPhase() || state.IsMatchFinished)
            {
                return;
            }

            Logger.InfoFormat(
                VotePhaseLogTemplate,
                matchId,
                timeLimit.TotalSeconds);

            int voteDurationSeconds = (int)Math.Ceiling(timeLimit.TotalSeconds);

            await dialogs.ShowVoteAndSendAsync(voteDurationSeconds);
        }

        private void HandleElimination(GameplayServiceProxy.PlayerSummary eliminated)
        {
            if (eliminated == null)
            {
                return;
            }

            state.AddEliminated(eliminated.UserId);

            if (state.IsDarknessActive)
            {
                EndDarknessMode(revealVote: true);
            }

            bool isMe = eliminated.UserId == state.MyUserId;

            dialogs.ShowEliminationMessage(eliminated);
            questions.OnEliminated(isMe);

            DetectAndApplyFinalPhaseIfApplicable();

            RefreshWildcardUseState();
        }

        private void DetectAndApplyFinalPhaseIfApplicable()
        {
            if (state.IsMatchFinished)
            {
                return;
            }

            int alivePlayers = state.GetAlivePlayersCount();

            if (alivePlayers == FinalPlayersCount)
            {
                if (!state.IsInFinalPhase())
                {
                    state.CurrentPhase = MatchPhase.Final;
                    UpdatePhaseLabel();
                }

                if (!state.HasAnnouncedFinalPhase)
                {
                    state.HasAnnouncedFinalPhase = true;

                    try
                    {
                        overlay.ShowSpecialEvent(FinalPhaseLabelText, string.Empty);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("DetectAndApplyFinalPhaseIfApplicable overlay error.", ex);
                    }
                }
            }
        }

        private async Task HandleSpecialEventAsync(Guid matchId, string eventName, string description)
        {
            Logger.InfoFormat(
                SpecialEventLogTemplate,
                matchId,
                eventName ?? string.Empty,
                description ?? string.Empty);

            state.CurrentPhase = MatchPhase.SpecialEvent;
            UpdatePhaseLabel();

            SpecialEventInfo info = SpecialEventInfo.Create(eventName, description);

            bool handledVoteReveal = await TryHandleDarkModeVoteRevealAsync(info);
            if (handledVoteReveal)
            {
                return;
            }

            ShowGenericSpecialEventOverlay(info);

            SpecialEventKind kind = DetermineSpecialEventKind(info);
            await ApplySpecialEventAsync(kind, info);
        }

        private async Task<bool> TryHandleDarkModeVoteRevealAsync(SpecialEventInfo info)
        {
            if (!IsDarkModeVoteRevealEvent(info.EventName, info.Description))
            {
                return false;
            }

            int? revealedUserId = TryParseFirstInt(info.EventName) ?? TryParseFirstInt(info.Description);
            if (revealedUserId.HasValue && revealedUserId.Value > 0)
            {
                state.PendingDarknessVotedUserId = revealedUserId.Value;
            }

            overlay.ShowSpecialEvent(DarkModeVoteRevealTitle, DarkModeVoteRevealDescription);
            TryRevealDarknessVote();

            await overlay.AutoHideSpecialEventAsync(MatchConstants.SURPRISE_EXAM_OVERLAY_AUTOHIDE_MS);

            return true;
        }

        private void ShowGenericSpecialEventOverlay(SpecialEventInfo info)
        {
            string title = string.IsNullOrWhiteSpace(info.EventName)
                ? MatchConstants.PHASE_SPECIAL_EVENT_TEXT
                : info.EventName;

            string desc = string.IsNullOrWhiteSpace(info.Description)
                ? string.Empty
                : info.Description;

            overlay.ShowSpecialEvent(title, desc);
        }

        private SpecialEventKind DetermineSpecialEventKind(SpecialEventInfo info)
        {
            if (IsDarkModeStartEvent(info.EventName, info.Description))
            {
                return SpecialEventKind.DarkModeStart;
            }

            if (IsDarkModeEndEvent(info.EventName, info.Description))
            {
                return SpecialEventKind.DarkModeEnd;
            }

            if (IsLegacyDarknessEndEvent(info.EventName, info.Description))
            {
                return SpecialEventKind.LegacyDarknessEnd;
            }

            if (string.Equals(
                info.EventName,
                MatchConstants.SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.SurpriseExamStarted;
            }

            if (string.Equals(
                info.EventName,
                MatchConstants.SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.SurpriseExamResolved;
            }

            if (string.Equals(
                info.EventName,
                MatchConstants.SPECIAL_EVENT_BOMB_QUESTION_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.BombQuestion;
            }

            if (string.Equals(
                info.EventName,
                MatchConstants.SPECIAL_EVENT_BOMB_APPLIED_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.BombApplied;
            }

            if (IsSabotageEvent(info.EventName, info.Description))
            {
                return SpecialEventKind.Sabotage;
            }

            if (string.Equals(info.EventName, MatchConstants.SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(info.EventName, MatchConstants.SPECIAL_EVENT_EXTRA_WILDCARD_CODE, StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.WildcardGranted;
            }

            return SpecialEventKind.Other;
        }

        private async Task ApplySpecialEventAsync(SpecialEventKind kind, SpecialEventInfo info)
        {
            switch (kind)
            {
                case SpecialEventKind.DarkModeStart:
                    BeginDarknessMode();
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.DarkModeEnd:
                    EndDarknessMode(revealVote: false);
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.LegacyDarknessEnd:
                    EndDarknessMode(revealVote: true);
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.SurpriseExamStarted:
                    questions.SetSurpriseExamActive(true);
                    state.IsSurpriseExamActive = true;
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.SurpriseExamResolved:
                    questions.SetSurpriseExamActive(false);
                    state.IsSurpriseExamActive = false;
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.BombQuestion:
                    questions.SetBombQuestionUi(true);
                    state.IsBombQuestionActive = true;
                    RefreshWildcardUseState();
                    return;

                case SpecialEventKind.BombApplied:
                    questions.SetBombQuestionUi(false);
                    state.IsBombQuestionActive = false;
                    RefreshWildcardUseState();
                    return;

                case SpecialEventKind.Sabotage:
                    ApplySabotageTimeOverride(info);
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.WildcardGranted:
                    await wildcards.LoadAsync();
                    RefreshWildcardUseState();
                    return;

                default:
                    return;
            }
        }

        private void ApplySabotageTimeOverride(SpecialEventInfo info)
        {
            int sourceTurnUserId = state.CurrentTurnUserId;

            int? parsedTargetUserId =
                TryParseFirstInt(info.EventName) ??
                TryParseFirstInt(info.Description);

            if (questions == null)
            {
                return;
            }

            questions.ScheduleNextTurnTimeLimitOverride(
                SabotageTimeSeconds,
                sourceTurnUserId > 0 ? (int?)sourceTurnUserId : null,
                parsedTargetUserId);
        }

        private async Task AutoHideAndRefreshWildcardsAsync()
        {
            await overlay.AutoHideSpecialEventAsync(MatchConstants.SURPRISE_EXAM_OVERLAY_AUTOHIDE_MS);
            RefreshWildcardUseState();
        }

        private enum SpecialEventKind
        {
            Other = 0,
            DarkModeStart = 1,
            DarkModeEnd = 2,
            LegacyDarknessEnd = 3,
            SurpriseExamStarted = 4,
            SurpriseExamResolved = 5,
            BombQuestion = 6,
            BombApplied = 7,
            Sabotage = 8,
            WildcardGranted = 9
        }

        private sealed class SpecialEventInfo
        {
            private SpecialEventInfo(string eventName, string description)
            {
                EventName = eventName;
                Description = description;
            }

            public string EventName { get; }
            public string Description { get; }

            public static SpecialEventInfo Create(string eventName, string description)
            {
                return new SpecialEventInfo(
                    eventName ?? string.Empty,
                    description ?? string.Empty);
            }
        }

        private static bool IsSabotageEvent(string eventName, string description)
        {
            return IsCode(eventName, SabotageCode)
                || IsCode(description, SabotageCode)
                || IsCode(eventName, SabotageUsedCode)
                || IsCode(description, SabotageUsedCode)
                || IsCode(eventName, SabotageAppliedCode)
                || IsCode(description, SabotageAppliedCode)
                || StartsWithCode(eventName, SabotageCode)
                || StartsWithCode(description, SabotageCode)
                || ContainsKeyword(eventName, SabotageKeywordEs)
                || ContainsKeyword(description, SabotageKeywordEs)
                || ContainsKeyword(eventName, SabotageCode)
                || ContainsKeyword(description, SabotageCode);
        }

        private static bool IsDarkModeStartEvent(string eventName, string description)
        {
            return IsCode(eventName, DarkModeStartCode)
                || IsCode(description, DarkModeStartCode)
                || IsCode(eventName, LegacyDarknessStartCode)
                || IsCode(description, LegacyDarknessStartCode)
                || ContainsKeyword(eventName, DarknessKeywordEs)
                || ContainsKeyword(description, DarknessKeywordEs);
        }

        private static bool IsDarkModeEndEvent(string eventName, string description)
        {
            return IsCode(eventName, DarkModeEndCode)
                || IsCode(description, DarkModeEndCode);
        }

        private static bool IsLegacyDarknessEndEvent(string eventName, string description)
        {
            return IsCode(eventName, LegacyDarknessEndCode)
                || IsCode(description, LegacyDarknessEndCode);
        }

        private static bool IsDarkModeVoteRevealEvent(string eventName, string description)
        {
            return StartsWithCode(eventName, DarkModeVoteRevealCode)
                || StartsWithCode(description, DarkModeVoteRevealCode);
        }

        private static bool IsCode(string text, string code)
        {
            return string.Equals(text != null ? text.Trim() : null, code, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWithCode(string text, string code)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            return text.Trim().StartsWith(code, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BeginDarknessMode()
        {
            if (state.IsDarknessActive)
            {
                return;
            }

            state.IsDarknessActive = true;

            int seed = BuildDarknessSeed(state.Match.MatchId, state.CurrentRoundNumber);

            state.DarknessSeed = seed;

            turns.EnableDarknessMode(seed);

            questions.SetDarknessActive(true);

            ApplyDarknessUiImmediate();
        }

        private void ApplyDarknessUiImmediate()
        {
            if (ui.TurnAvatar != null)
            {
                ui.TurnAvatar.Visibility = Visibility.Collapsed;
            }

            if (ui.TxtTurnPlayerName != null)
            {
                ui.TxtTurnPlayerName.Text = DarknessUnknownName;
            }

            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = DarknessTurnLabel;
            }

            if (ui.TurnBannerBackground != null)
            {
                ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource("Brush.Turn.OtherTurn");
            }
        }

        private void EndDarknessMode(bool revealVote)
        {
            if (!state.IsDarknessActive)
            {
                return;
            }

            state.IsDarknessActive = false;
            state.DarknessSeed = null;

            turns.DisableDarknessMode();
            questions.SetDarknessActive(false);

            if (revealVote)
            {
                TryRevealDarknessVote();
            }

            if (!state.IsMatchFinished)
            {
                DetectAndApplyFinalPhaseIfApplicable();

                if (!state.IsInFinalPhase())
                {
                    state.CurrentPhase = MatchPhase.NormalRound;
                }

                UpdatePhaseLabel();
            }
        }

        private void TryRevealDarknessVote()
        {
            int? votedUserId = state.PendingDarknessVotedUserId;
            state.PendingDarknessVotedUserId = null;

            if (!votedUserId.HasValue || votedUserId.Value <= 0)
            {
                return;
            }

            try
            {
                PlayerSummary[] lobbyPlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();

                PlayerSummary voted = lobbyPlayers.FirstOrDefault(p => p != null && p.UserId == votedUserId.Value);

                string name = voted != null && !string.IsNullOrWhiteSpace(voted.DisplayName)
                    ? voted.DisplayName
                    : string.Format(CultureInfo.CurrentCulture, GenericPlayerNameTemplate, votedUserId.Value);

                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, RevealVoteTemplate, name),
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Warn("TryRevealDarknessVote error.", ex);
            }
        }

        private static int BuildDarknessSeed(Guid matchId, int roundNumber)
        {
            return matchId.GetHashCode() ^ roundNumber;
        }

        private static int? TryParseFirstInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string t = text.Trim();

            int i = 0;
            while (i < t.Length && !char.IsDigit(t[i]))
            {
                i++;
            }

            if (i >= t.Length)
            {
                return null;
            }

            int start = i;

            while (i < t.Length && char.IsDigit(t[i]))
            {
                i++;
            }

            string numberText = t.Substring(start, i - start);

            int value;
            if (int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            return null;
        }

        private void HandleCoinFlip(GameplayServiceProxy.CoinFlipResolvedDto coinFlip)
        {
            if (coinFlip == null)
            {
                return;
            }

            int serverRoundId = coinFlip.RoundId;

            if (serverRoundId > 0)
            {
                state.CurrentRoundNumber = serverRoundId;
            }
            else
            {
                state.CurrentRoundNumber = state.CurrentRoundNumber < 1
                    ? 1
                    : state.CurrentRoundNumber + 1;
            }

            state.CurrentPhase = coinFlip.ShouldEnableDuel ? MatchPhase.Duel : MatchPhase.NormalRound;

            DetectAndApplyFinalPhaseIfApplicable();

            if (state.IsInFinalPhase())
            {
                state.CurrentPhase = MatchPhase.Final;
            }

            UpdatePhaseLabel();

            overlay.ShowCoinFlip(coinFlip);

            RefreshWildcardUseState();
        }

        private async Task HandleDuelCandidatesAsync(GameplayServiceProxy.DuelCandidatesDto duelCandidates)
        {
            DetectAndApplyFinalPhaseIfApplicable();

            if (state.IsInFinalPhase() || state.IsMatchFinished)
            {
                return;
            }

            if (duelCandidates == null || duelCandidates.Candidates == null || duelCandidates.Candidates.Length == 0)
            {
                Logger.Warn("OnServerDuelCandidates: sin candidatos.");
                return;
            }

            int weakestRivalUserId = duelCandidates.WeakestRivalUserId;

            var items = new List<DuelCandidateItem>();

            foreach (GameplayServiceProxy.DuelCandidateDto candidate in duelCandidates.Candidates)
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
                            ? MatchConstants.DEFAULT_PLAYER_NAME
                            : candidate.DisplayName
                    });
            }

            if (state.MyUserId != weakestRivalUserId)
            {
                return;
            }

            state.CurrentPhase = MatchPhase.Duel;
            UpdatePhaseLabel();

            await dialogs.ShowDuelSelectionAndSendAsync(weakestRivalUserId, items);
        }

        private void HandleMatchFinished(GameplayServiceProxy.PlayerSummary winner)
        {
            if (state.IsDarknessActive)
            {
                EndDarknessMode(revealVote: false);
            }

            state.IsMatchFinished = true;
            state.FinalWinner = winner;
            state.IsMyTurn = false;

            try
            {
                timer.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn("HandleMatchFinished timer stop error.", ex);
            }

            if (ui.BtnBank != null) ui.BtnBank.IsEnabled = false;
            if (ui.BtnAnswer1 != null) ui.BtnAnswer1.IsEnabled = false;
            if (ui.BtnAnswer2 != null) ui.BtnAnswer2.IsEnabled = false;
            if (ui.BtnAnswer3 != null) ui.BtnAnswer3.IsEnabled = false;
            if (ui.BtnAnswer4 != null) ui.BtnAnswer4.IsEnabled = false;

            RefreshWildcardUseState();

            dialogs.ShowMatchFinishedMessage(winner);

            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = MatchFinishedLabelText;
            }

            state.CurrentPhase = MatchPhase.Finished;
            UpdatePhaseLabel();
        }

        private void HandleLightningChallengeStarted(GameplayServiceProxy.PlayerSummary targetPlayer, int totalTimeSeconds)
        {
            state.CurrentPhase = MatchPhase.SpecialEvent;
            UpdatePhaseLabel();

            int targetUserId = targetPlayer != null ? targetPlayer.UserId : 0;

            bool isTargetMe = targetUserId == state.MyUserId && !state.IsEliminated(state.MyUserId);

            state.IsMyTurn = isTargetMe;

            lightningTimeLimitSeconds = totalTimeSeconds > 0
                ? totalTimeSeconds
                : MatchConstants.QUESTION_TIME_SECONDS;

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = false;
            }

            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = state.IsMyTurn ? "Reto relámpago: tu turno" : "Reto relámpago en curso";
            }

            if (ui.TurnBannerBackground != null)
            {
                ui.TurnBannerBackground.Background =
                    (Brush)ui.Window.FindResource(state.IsMyTurn ? "Brush.Turn.MyTurn" : "Brush.Turn.OtherTurn");
            }

            wildcards.RefreshUseState(false);
        }

        private void HandleLightningChallengeQuestion(GameplayServiceProxy.QuestionWithAnswersDto question)
        {
            state.CurrentPhase = MatchPhase.SpecialEvent;
            UpdatePhaseLabel();

            questions.OnLightningQuestion(question, lightningTimeLimitSeconds);
        }

        private async Task HandleLightningChallengeFinishedAsync(int correctAnswers, bool isSuccess)
        {
            try
            {
                timer.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn("HandleLightningChallengeFinishedAsync timer stop error.", ex);
            }

            state.IsMyTurn = false;

            string message = isSuccess
                ? string.Format(CultureInfo.CurrentCulture, LightningSuccessTemplate, correctAnswers)
                : string.Format(CultureInfo.CurrentCulture, LightningFailTemplate, correctAnswers);

            MessageBox.Show(
                message,
                MatchConstants.GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (isSuccess)
            {
                await wildcards.LoadAsync();
            }

            DetectAndApplyFinalPhaseIfApplicable();

            if (!state.IsMatchFinished)
            {
                state.CurrentPhase = state.IsInFinalPhase() ? MatchPhase.Final : MatchPhase.NormalRound;
            }

            UpdatePhaseLabel();

            RefreshWildcardUseState();
        }

        private async Task HandleTurnOrderInitializedAsync(object turnOrder)
        {
            turns.ApplyTurnOrder(turnOrder);

            SyncMyTurnFromTurnOrder(turnOrder);

            DetectAndApplyFinalPhaseIfApplicable();

            RefreshWildcardUseState();

            if (state.IsDarknessActive)
            {
                return;
            }

            await turns.PlayTurnIntroAsync(
                turnOrder,
                (t, d) => overlay.ShowSpecialEvent(t, d),
                () => overlay.HideSpecialEvent());
        }

        private async Task HandleTurnOrderChangedAsync(object turnOrder, string reasonCode)
        {
            turns.ApplyTurnOrder(turnOrder);

            SyncMyTurnFromTurnOrder(turnOrder);

            DetectAndApplyFinalPhaseIfApplicable();

            RefreshWildcardUseState();

            if (state.IsDarknessActive)
            {
                return;
            }

            overlay.ShowSpecialEvent(
                MatchConstants.TURN_ORDER_CHANGED_TITLE,
                string.IsNullOrWhiteSpace(reasonCode) ? string.Empty : reasonCode);

            await Task.Delay(MatchConstants.TURN_INTRO_FINAL_DELAY_MS);

            overlay.HideSpecialEvent();
        }

        public async Task OnAnswerButtonClickAsync(System.Windows.Controls.Button button)
        {
            await questions.SubmitAnswerFromButtonAsync(button);
        }

        public async Task OnBankClickAsync()
        {
            await questions.BankAsync();
        }

        public void OnWildcardPrev()
        {
            wildcards.SelectPrev();
        }

        public void OnWildcardNext()
        {
            wildcards.SelectNext();
        }

        public async Task OnUseWildcardClickAsync()
        {
            bool canUse = CanUseWildcardNow();

            await wildcards.UseSelectedAsync(canUse);

            RefreshWildcardUseState();
        }

        public void OnIntroEnded()
        {
            overlay.HideIntro();
        }

        public void OnSkipIntro()
        {
            overlay.HideIntro();
            overlay.StopIntroVideoSafe();
        }

        public void OnCloseSpecialEvent()
        {
            overlay.HideSpecialEvent();

            if (!state.IsMatchFinished)
            {
                DetectAndApplyFinalPhaseIfApplicable();

                state.CurrentPhase = state.IsInFinalPhase()
                    ? MatchPhase.Final
                    : MatchPhase.NormalRound;

                UpdatePhaseLabel();
            }
        }

        public void OnCloseRequested(Action closeWindow, Action showResultAndClose)
        {
            if (!state.IsMatchFinished)
            {
                MessageBoxResult result = MessageBox.Show(
                    "La partida aún no ha terminado. ¿Deseas salir al lobby?",
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                closeWindow?.Invoke();
                return;
            }

            showResultAndClose?.Invoke();
        }

        public void ShowResultAndClose(Action closeWindow)
        {
            dialogs.ShowMatchResultAndClose(closeWindow);
        }

        private void UpdatePhaseLabel()
        {
            if (ui.TxtPhase == null)
            {
                return;
            }

            string phaseDetail;

            switch (state.CurrentPhase)
            {
                case MatchPhase.NormalRound:
                    phaseDetail = string.Format(CultureInfo.CurrentCulture, MatchConstants.PHASE_ROUND_FORMAT, state.CurrentRoundNumber);
                    break;

                case MatchPhase.Duel:
                    phaseDetail = MatchConstants.PHASE_DUEL_TEXT;
                    break;

                case MatchPhase.SpecialEvent:
                    phaseDetail = MatchConstants.PHASE_SPECIAL_EVENT_TEXT;
                    break;

                case MatchPhase.Final:
                    phaseDetail = FinalPhaseLabelText;
                    break;

                case MatchPhase.Finished:
                    phaseDetail = MatchConstants.PHASE_FINISHED_TEXT;
                    break;

                default:
                    phaseDetail = string.Empty;
                    break;
            }

            ui.TxtPhase.Text = string.Format(
                CultureInfo.CurrentCulture,
                "{0}: {1}",
                MatchConstants.PHASE_TITLE,
                phaseDetail);
        }

        private void SyncMyTurnFromTurnOrder(object turnOrder)
        {
            var dto = turnOrder as GameplayServiceProxy.TurnOrderDto;
            if (dto == null)
            {
                return;
            }

            bool isMyTurnNow = dto.CurrentTurnUserId == state.MyUserId
                && !state.IsEliminated(state.MyUserId)
                && !state.IsMatchFinished;

            state.IsMyTurn = isMyTurnNow;
        }

        private bool CanUseWildcardNow()
        {
            if (state.IsMatchFinished)
            {
                return false;
            }

            if (state.IsInFinalPhase())
            {
                return false;
            }

            if (state.IsEliminated(state.MyUserId))
            {
                return false;
            }

            if (!state.IsMyTurn)
            {
                return false;
            }

            return true;
        }

        private void RefreshWildcardUseState()
        {
            wildcards.RefreshUseState(CanUseWildcardNow());
        }

        public void Dispose()
        {
            try
            {
                turns.CancelIntro();

                if (timer != null && timer.IsRunning)
                {
                    timer.Stop();
                }

                if (gameplay != null)
                {
                    gameplay.CloseSafely();
                    gameplay = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("MatchSessionCoordinator.Dispose error.", ex);
            }
        }
    }
}
