using System;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchInputController
    {
        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;
        private readonly WildcardController wildcards;
        private readonly Func<bool> canUseWildcardNow;

        internal MatchInputController(
            MatchWindowUiRefs ui,
            MatchSessionState state,
            WildcardController wildcards,
            Func<bool> canUseWildcardNow)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.wildcards = wildcards ?? throw new ArgumentNullException(nameof(wildcards));
            this.canUseWildcardNow = canUseWildcardNow ?? throw new ArgumentNullException(nameof(canUseWildcardNow));
        }

        internal void DisableGameplayInputs()
        {
            state.IsMyTurn = false;

            if (ui.BtnBank != null) ui.BtnBank.IsEnabled = false;
            if (ui.BtnAnswer1 != null) ui.BtnAnswer1.IsEnabled = false;
            if (ui.BtnAnswer2 != null) ui.BtnAnswer2.IsEnabled = false;
            if (ui.BtnAnswer3 != null) ui.BtnAnswer3.IsEnabled = false;
            if (ui.BtnAnswer4 != null) ui.BtnAnswer4.IsEnabled = false;

            wildcards.RefreshUseState(false);
        }

        internal void RefreshGameplayInputs()
        {
            if (state.IsMatchFinished)
            {
                DisableGameplayInputs();
                return;
            }

            bool canInteract = state.IsMyTurn
                && !state.IsEliminated(state.MyUserId)
                && !state.IsInFinalPhase();

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = canInteract && !state.IsSurpriseExamActive;
            }

            if (ui.BtnAnswer1 != null) ui.BtnAnswer1.IsEnabled = canInteract;
            if (ui.BtnAnswer2 != null) ui.BtnAnswer2.IsEnabled = canInteract;
            if (ui.BtnAnswer3 != null) ui.BtnAnswer3.IsEnabled = canInteract;
            if (ui.BtnAnswer4 != null) ui.BtnAnswer4.IsEnabled = canInteract;

            wildcards.RefreshUseState(canUseWildcardNow());
        }
    }
}
