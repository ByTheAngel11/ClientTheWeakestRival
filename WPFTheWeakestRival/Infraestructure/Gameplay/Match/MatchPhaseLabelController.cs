using System;
using System.Globalization;
using log4net;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchPhaseLabelController
    {
        private const string PHASE_LABEL_FORMAT = "{0}: {1}";
        private const string OVERLAY_EMPTY_DESCRIPTION = "";

        private const string LOG_SHOW_SPECIAL_EVENT_ERROR = "MatchPhaseLabelController.ShowSpecialEvent error.";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchPhaseLabelController));

        private readonly MatchWindowUiRefs uiMatchWindow;
        private readonly MatchSessionState state;
        private readonly OverlayController overlay;

        internal MatchPhaseLabelController(MatchWindowUiRefs ui, MatchSessionState state, OverlayController overlay)
        {
            this.uiMatchWindow = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        internal void DetectAndApplyFinalPhaseIfApplicable(int finalPlayersCount)
        {
            if (state.IsMatchFinished)
            {
                return;
            }

            int alivePlayers = state.GetAlivePlayersCount();

            if (alivePlayers != finalPlayersCount)
            {
                return;
            }

            if (!state.IsInFinalPhase())
            {
                state.CurrentPhase = MatchPhase.Final;
                UpdatePhaseLabel();
            }

            if (state.HasAnnouncedFinalPhase)
            {
                return;
            }

            state.HasAnnouncedFinalPhase = true;

            try
            {
                overlay.ShowSpecialEvent(Lang.matchFinalPhaseTitle, OVERLAY_EMPTY_DESCRIPTION);
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_SHOW_SPECIAL_EVENT_ERROR, ex);
            }
        }

        internal void UpdatePhaseLabel()
        {
            if (uiMatchWindow.TxtPhase == null)
            {
                return;
            }

            string phaseTitle = Lang.phaseTitle;

            string phaseDetail;

            switch (state.CurrentPhase)
            {
                case MatchPhase.NormalRound:
                    phaseDetail = string.Format(
                        CultureInfo.CurrentCulture,
                        Lang.phaseRoundFormat,
                        state.CurrentRoundNumber);
                    break;

                case MatchPhase.Duel:
                    phaseDetail = Lang.phaseDuel;
                    break;

                case MatchPhase.SpecialEvent:
                    phaseDetail = Lang.phaseSpecialEvent;
                    break;

                case MatchPhase.Final:
                    phaseDetail = Lang.matchFinalPhaseTitle;
                    break;

                case MatchPhase.Finished:
                    phaseDetail = Lang.phaseFinished;
                    break;

                default:
                    phaseDetail = string.Empty;
                    break;
            }

            uiMatchWindow.TxtPhase.Text = string.Format(
                CultureInfo.CurrentCulture,
                PHASE_LABEL_FORMAT,
                phaseTitle,
                phaseDetail);
        }
    }
}
