using log4net;
using System;
using System.Globalization;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using WPFTheWeakestRival.LobbyService;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchSpecialEventController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchSpecialEventController));

        private const string SpecialEventLogTemplate = "OnSpecialEvent. MatchId={0}, Name='{1}', Desc='{2}'";

        private const string SabotageCode = "SABOTAGE";
        private const string SabotageUsedCode = "SABOTAGE_USED";
        private const string SabotageAppliedCode = "SABOTAGE_APPLIED";
        private const string SabotageKeywordEs = "sabotaje";

        private const int SabotageTimeSeconds = 15;

        private readonly MatchSessionState state;
        private readonly OverlayController overlay;
        private readonly WildcardController wildcards;
        private readonly QuestionController questions;
        private readonly MatchDialogController dialogs;
        private readonly MatchPhaseLabelController phaseController;
        private readonly MatchDarknessController darknessController;
        private readonly Action refreshWildcardUseState;

        internal MatchSpecialEventController(
            MatchSessionState state,
            OverlayController overlay,
            WildcardController wildcards,
            QuestionController questions,
            MatchDialogController dialogs,
            MatchPhaseLabelController phaseController,
            MatchDarknessController darknessController,
            Action refreshWildcardUseState)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            this.wildcards = wildcards ?? throw new ArgumentNullException(nameof(wildcards));
            this.questions = questions ?? throw new ArgumentNullException(nameof(questions));
            this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            this.phaseController = phaseController ?? throw new ArgumentNullException(nameof(phaseController));
            this.darknessController = darknessController ?? throw new ArgumentNullException(nameof(darknessController));
            this.refreshWildcardUseState = refreshWildcardUseState ?? throw new ArgumentNullException(nameof(refreshWildcardUseState));
        }

        internal async Task HandleSpecialEventAsync(Guid matchId, string eventName, string description)
        {
            Logger.InfoFormat(
                SpecialEventLogTemplate,
                matchId,
                eventName ?? string.Empty,
                description ?? string.Empty);

            state.CurrentPhase = MatchPhase.SpecialEvent;
            phaseController.UpdatePhaseLabel();

            string name = eventName ?? string.Empty;
            string desc = description ?? string.Empty;

            if (await TryHandleVoteRevealAsync(name, desc))
            {
                return;
            }

            ShowGenericOverlay(name, desc);

            SpecialEventKind kind = DetermineKind(name, desc);
            await ApplyAsync(kind, name, desc);
        }

        internal void HandleElimination(GameplayServiceProxy.PlayerSummary eliminated)
        {
            if (eliminated == null)
            {
                return;
            }

            state.AddEliminated(eliminated.UserId);

            if (state.IsDarknessActive)
            {
                darknessController.EndDarknessMode(revealVote: true);
            }

            bool isMe = eliminated.UserId == state.MyUserId;

            dialogs.ShowEliminationMessage(eliminated);
            questions.OnEliminated(isMe);

            refreshWildcardUseState();
        }

        private async Task<bool> TryHandleVoteRevealAsync(string eventName, string description)
        {
            if (!darknessController.IsVoteRevealEvent(eventName, description))
            {
                return false;
            }

            int? revealedUserId = TryParseFirstInt(eventName) ?? TryParseFirstInt(description);
            darknessController.SetPendingVoteRevealUserId(revealedUserId);

            darknessController.ShowVoteRevealOverlay(overlay);
            darknessController.RevealPendingVoteIfAny();

            await overlay.AutoHideSpecialEventAsync(MatchConstants.SURPRISE_EXAM_OVERLAY_AUTOHIDE_MS);

            return true;
        }

        private void ShowGenericOverlay(string eventName, string description)
        {
            string title = string.IsNullOrWhiteSpace(eventName)
                ? MatchConstants.PHASE_SPECIAL_EVENT_TEXT
                : eventName;

            string desc = string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : description;

            overlay.ShowSpecialEvent(title, desc);
        }

        private SpecialEventKind DetermineKind(string eventName, string description)
        {
            if (darknessController.IsDarkModeStartEvent(eventName, description))
            {
                return SpecialEventKind.DarkModeStart;
            }

            if (darknessController.IsDarkModeEndEvent(eventName, description))
            {
                return SpecialEventKind.DarkModeEnd;
            }

            if (darknessController.IsLegacyDarknessEndEvent(eventName, description))
            {
                return SpecialEventKind.LegacyDarknessEnd;
            }

            if (string.Equals(
                eventName,
                MatchConstants.SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.SurpriseExamStarted;
            }

            if (string.Equals(
                eventName,
                MatchConstants.SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.SurpriseExamResolved;
            }

            if (string.Equals(
                eventName,
                MatchConstants.SPECIAL_EVENT_BOMB_QUESTION_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.BombQuestion;
            }

            if (string.Equals(
                eventName,
                MatchConstants.SPECIAL_EVENT_BOMB_APPLIED_CODE,
                StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.BombApplied;
            }

            if (IsSabotageEvent(eventName, description))
            {
                return SpecialEventKind.Sabotage;
            }

            if (string.Equals(eventName, MatchConstants.SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventName, MatchConstants.SPECIAL_EVENT_EXTRA_WILDCARD_CODE, StringComparison.OrdinalIgnoreCase))
            {
                return SpecialEventKind.WildcardGranted;
            }

            return SpecialEventKind.Other;
        }

        private async Task ApplyAsync(SpecialEventKind kind, string eventName, string description)
        {
            switch (kind)
            {
                case SpecialEventKind.DarkModeStart:
                    darknessController.BeginDarknessMode();
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.DarkModeEnd:
                    darknessController.EndDarknessMode(revealVote: false);
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.LegacyDarknessEnd:
                    darknessController.EndDarknessMode(revealVote: true);
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
                    refreshWildcardUseState();
                    return;

                case SpecialEventKind.BombApplied:
                    questions.SetBombQuestionUi(false);
                    state.IsBombQuestionActive = false;
                    refreshWildcardUseState();
                    return;

                case SpecialEventKind.Sabotage:
                    ApplySabotageTimeOverride(eventName, description);
                    await AutoHideAndRefreshWildcardsAsync();
                    return;

                case SpecialEventKind.WildcardGranted:
                    await wildcards.LoadAsync();
                    refreshWildcardUseState();
                    return;

                default:
                    return;
            }
        }

        private void ApplySabotageTimeOverride(string eventName, string description)
        {
            int sourceTurnUserId = state.CurrentTurnUserId;

            int? parsedTargetUserId =
                TryParseFirstInt(eventName) ??
                TryParseFirstInt(description);

            questions.ScheduleNextTurnTimeLimitOverride(
                SabotageTimeSeconds,
                sourceTurnUserId > 0 ? (int?)sourceTurnUserId : null,
                parsedTargetUserId);
        }

        private async Task AutoHideAndRefreshWildcardsAsync()
        {
            await overlay.AutoHideSpecialEventAsync(MatchConstants.SURPRISE_EXAM_OVERLAY_AUTOHIDE_MS);
            refreshWildcardUseState();
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
    }
}
