using System;
using System.Globalization;
using log4net;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchPhaseLabelController
    {
        private const string FinalPhaseLabelText = "Final 1 vs 1";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchPhaseLabelController));

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;
        private readonly OverlayController overlay;

        internal MatchPhaseLabelController(MatchWindowUiRefs ui, MatchSessionState state, OverlayController overlay)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
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
                overlay.ShowSpecialEvent(FinalPhaseLabelText, string.Empty);
            }
            catch (Exception ex)
            {
                Logger.Warn("MatchPhaseLabelController.ShowSpecialEvent error.", ex);
            }
        }

        internal void UpdatePhaseLabel()
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
    }
}
