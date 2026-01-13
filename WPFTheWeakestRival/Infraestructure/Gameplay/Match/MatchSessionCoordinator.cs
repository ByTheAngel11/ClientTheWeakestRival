using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using WPFTheWeakestRival.Infrastructure.Gameplay;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;
using WildcardFault = WPFTheWeakestRival.WildcardService.ServiceFault;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchSessionCoordinator : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchSessionCoordinator));

        private const string GameplayEndpointName = "WSDualHttpBinding_IGameplayService";

        private const string ReconnectingTitle = "Reconectando…";
        private const string ReconnectingDescription = "Intentando recuperar la conexión con el servidor.";

        private const string VotePhaseLogTemplate = "OnServerVotePhaseStarted. MatchId={0}, TimeLimitSeconds={1}";

        private const string ContextVotePhaseStarted = "MatchSessionCoordinator.VotePhaseStarted";
        private const string ContextSpecialEvent = "MatchSessionCoordinator.SpecialEvent";
        private const string ContextDuelCandidates = "MatchSessionCoordinator.DuelCandidates";
        private const string ContextLightningFinished = "MatchSessionCoordinator.LightningFinished";
        private const string ContextTurnOrderInitialized = "MatchSessionCoordinator.TurnOrderInitialized";
        private const string ContextTurnOrderChanged = "MatchSessionCoordinator.TurnOrderChanged";

        private const int FinalPlayersCount = 2;

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;

        private readonly OverlayController overlay;
        private readonly WildcardController wildcards;
        private readonly TurnOrderController turns;
        private readonly QuestionTimerController timer;

        private GameplayHub hub;
        private GameplayClientProxy gameplay;
        private GameplayCallbackBridge callbackBridge;

        private QuestionController questions;
        private MatchDialogController dialogs;

        private MatchInputController inputController;
        private MatchPhaseLabelController phaseController;
        private MatchDarknessController darknessController;
        private MatchSpecialEventController specialEventController;
        private LightningChallengeController lightningController;

        private bool isDisposed;

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
        }

        private void InitializeGameplayClient()
        {
            hub = new GameplayHub(GameplayEndpointName);

            hub.ConnectionLost += OnGameplayConnectionLost;
            hub.ConnectionRestored += OnGameplayConnectionRestored;

            callbackBridge = hub.Callbacks;

            gameplay = new GameplayClientProxy(hub);

            questions = new QuestionController(ui, state, gameplay, timer);
            questions.InitializeEmptyUi();

            dialogs = new MatchDialogController(ui, state, gameplay);

            phaseController = new MatchPhaseLabelController(ui, state, overlay);
            darknessController = new MatchDarknessController(ui, state, turns, questions);

            inputController = new MatchInputController(ui, state, wildcards, CanUseWildcardNow);

            specialEventController = new MatchSpecialEventController(
                state,
                overlay,
                wildcards,
                questions,
                dialogs,
                phaseController,
                darknessController,
                RefreshWildcardUseState);

            lightningController = new LightningChallengeController(
                ui,
                state,
                overlay,
                wildcards,
                questions,
                timer,
                phaseController,
                inputController,
                RefreshWildcardUseState);

            BindCallbacks();

            phaseController.UpdatePhaseLabel();
            RefreshWildcardUseState();
        }

        private void BindCallbacks()
        {
            callbackBridge.NextQuestion += (matchId, targetPlayer, question, chain, banked) =>
                HandleNextQuestion(targetPlayer, question, chain, banked);

            callbackBridge.AnswerEvaluated += (matchId, player, result) =>
            {
                questions?.OnAnswerEvaluated(player, result);
            };

            callbackBridge.BankUpdated += (matchId, bank) =>
            {
                questions?.OnBankUpdated(bank);
            };

            callbackBridge.VotePhaseStarted += (matchId, timeLimit) =>
                RunAsync(() => HandleVotePhaseStartedAsync(matchId, timeLimit), ContextVotePhaseStarted);

            callbackBridge.Elimination += (matchId, eliminated) =>
                specialEventController.HandleElimination(eliminated);

            callbackBridge.SpecialEvent += (matchId, name, description) =>
                RunAsync(() => specialEventController.HandleSpecialEventAsync(matchId, name, description), ContextSpecialEvent);

            callbackBridge.CoinFlipResolved += (matchId, coinFlip) =>
                HandleCoinFlip(coinFlip);

            callbackBridge.DuelCandidates += (matchId, duelCandidates) =>
                RunAsync(() => HandleDuelCandidatesAsync(duelCandidates), ContextDuelCandidates);

            callbackBridge.MatchFinished += (matchId, winner) =>
                HandleMatchFinished(winner);

            callbackBridge.LightningChallengeStarted += (m, r, tp, tq, ts) =>
                lightningController.HandleStarted(tp, ts);

            callbackBridge.LightningChallengeQuestion += (m, r, qi, qq) =>
                lightningController.HandleQuestion(qq);

            callbackBridge.LightningChallengeFinished += (m, r, ca, ok) =>
                RunAsync(() => lightningController.HandleFinishedAsync(ca, ok), ContextLightningFinished);

            callbackBridge.TurnOrderInitialized += (matchId, turnOrder) =>
                RunAsync(() => HandleTurnOrderInitializedAsync(turnOrder), ContextTurnOrderInitialized);

            callbackBridge.TurnOrderChanged += (matchId, turnOrder, reason) =>
                RunAsync(() => HandleTurnOrderChangedAsync(turnOrder, reason), ContextTurnOrderChanged);
        }

        private static void RunAsync(Func<Task> action, string context)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                Task task = action();
                if (task == null)
                {
                    return;
                }

                task.ContinueWith(
                    t =>
                    {
                        try
                        {
                            Logger.Warn(context, t.Exception);
                        }
                        catch (Exception ex)
                        {
                            GC.KeepAlive(ex);
                        }
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
            }
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

                _ = await hub.JoinMatchAsync(request);
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

        private void OnGameplayConnectionLost(Exception ex)
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                Logger.Warn("Gameplay connection lost.", ex);
            }
            catch (Exception logEx)
            {
                GC.KeepAlive(logEx);
            }

            try
            {
                timer.Stop();
            }
            catch (Exception timerEx)
            {
                Logger.Warn("Timer stop failed while reconnecting.", timerEx);
            }

            inputController.DisableGameplayInputs();

            try
            {
                overlay.ShowSpecialEvent(ReconnectingTitle, ReconnectingDescription);
            }
            catch (Exception overlayEx)
            {
                Logger.Warn("Overlay reconnect show failed.", overlayEx);
            }
        }

        private void OnGameplayConnectionRestored()
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                Logger.Info("Gameplay connection restored.");
            }
            catch (Exception logEx)
            {
                GC.KeepAlive(logEx);
            }

            try
            {
                overlay.HideSpecialEvent();
            }
            catch (Exception overlayEx)
            {
                Logger.Warn("Overlay reconnect hide failed.", overlayEx);
            }

            RefreshWildcardUseState();
            inputController.RefreshGameplayInputs();
        }

        private void HandleNextQuestion(
            GameplayServiceProxy.PlayerSummary targetPlayer,
            GameplayServiceProxy.QuestionWithAnswersDto question,
            decimal chain,
            decimal banked)
        {
            phaseController.DetectAndApplyFinalPhaseIfApplicable(FinalPlayersCount);

            if (!state.IsInFinalPhase())
            {
                state.CurrentPhase = state.IsSurpriseExamActive || state.IsDarknessActive
                    ? MatchPhase.SpecialEvent
                    : MatchPhase.NormalRound;
            }

            phaseController.UpdatePhaseLabel();

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
            phaseController.DetectAndApplyFinalPhaseIfApplicable(FinalPlayersCount);

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

            phaseController.DetectAndApplyFinalPhaseIfApplicable(FinalPlayersCount);

            if (state.IsInFinalPhase())
            {
                state.CurrentPhase = MatchPhase.Final;
            }

            phaseController.UpdatePhaseLabel();

            overlay.ShowCoinFlip(coinFlip);

            RefreshWildcardUseState();
        }

        private async Task HandleDuelCandidatesAsync(GameplayServiceProxy.DuelCandidatesDto duelCandidates)
        {
            phaseController.DetectAndApplyFinalPhaseIfApplicable(FinalPlayersCount);

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
            phaseController.UpdatePhaseLabel();

            await dialogs.ShowDuelSelectionAndSendAsync(weakestRivalUserId, items);
        }

        private void HandleMatchFinished(GameplayServiceProxy.PlayerSummary winner)
        {
            if (state.IsDarknessActive)
            {
                darknessController.EndDarknessMode(revealVote: false);
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

            inputController.DisableGameplayInputs();

            RefreshWildcardUseState();

            dialogs.ShowMatchFinishedMessage(winner);

            if (ui.TxtTurnLabel != null)
            {
                ui.TxtTurnLabel.Text = MatchConstants.MATCH_FINISHED_LABEL_TEXT;
            }

            state.CurrentPhase = MatchPhase.Finished;
            phaseController.UpdatePhaseLabel();
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
                phaseController.DetectAndApplyFinalPhaseIfApplicable(FinalPlayersCount);

                state.CurrentPhase = state.IsInFinalPhase()
                    ? MatchPhase.Final
                    : MatchPhase.NormalRound;

                phaseController.UpdatePhaseLabel();
            }
        }

        public void OnCloseRequested(Action closeWindow, Action showResultAndClose)
        {
            if (!state.IsMatchFinished)
            {
                MessageBoxResult result = MessageBox.Show(
                    MatchConstants.EXIT_TO_LOBBY_PROMPT,
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

            inputController.RefreshGameplayInputs();
        }

        private async Task HandleTurnOrderInitializedAsync(object turnOrder)
        {
            turns.ApplyTurnOrder(turnOrder);

            SyncMyTurnFromTurnOrder(turnOrder);

            phaseController.DetectAndApplyFinalPhaseIfApplicable(FinalPlayersCount);

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

            phaseController.DetectAndApplyFinalPhaseIfApplicable(FinalPlayersCount);

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
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            try
            {
                turns.CancelIntro();

                if (timer != null && timer.IsRunning)
                {
                    timer.Stop();
                }

                if (hub != null)
                {
                    hub.ConnectionLost -= OnGameplayConnectionLost;
                    hub.ConnectionRestored -= OnGameplayConnectionRestored;
                    hub.Dispose();
                    hub = null;
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
