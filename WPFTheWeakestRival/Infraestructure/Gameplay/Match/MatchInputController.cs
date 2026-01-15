using System;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchInputController
    {
        private readonly MatchWindowUiRefs uiMatchWindow;
        private readonly MatchSessionState state;
        private readonly WildcardController wildcards;
        private readonly Func<bool> canUseWildcardNow;

        internal MatchInputController(
            MatchWindowUiRefs ui,
            MatchSessionState state,
            WildcardController wildcards,
            Func<bool> canUseWildcardNow)
        {
            this.uiMatchWindow = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.wildcards = wildcards ?? throw new ArgumentNullException(nameof(wildcards));
            this.canUseWildcardNow = canUseWildcardNow ?? throw new ArgumentNullException(nameof(canUseWildcardNow));
        }

        internal void DisableGameplayInputs()
        {
            state.IsMyTurn = false;

            if (uiMatchWindow.BtnBank != null) uiMatchWindow.BtnBank.IsEnabled = false;
            if (uiMatchWindow.BtnAnswer1 != null) uiMatchWindow.BtnAnswer1.IsEnabled = false;
            if (uiMatchWindow.BtnAnswer2 != null) uiMatchWindow.BtnAnswer2.IsEnabled = false;
            if (uiMatchWindow.BtnAnswer3 != null) uiMatchWindow.BtnAnswer3.IsEnabled = false;
            if (uiMatchWindow.BtnAnswer4 != null) uiMatchWindow.BtnAnswer4.IsEnabled = false;

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

            if (uiMatchWindow.BtnBank != null)
            {
                uiMatchWindow.BtnBank.IsEnabled = canInteract && !state.IsSurpriseExamActive;
            }

            if (uiMatchWindow.BtnAnswer1 != null) uiMatchWindow.BtnAnswer1.IsEnabled = canInteract;
            if (uiMatchWindow.BtnAnswer2 != null) uiMatchWindow.BtnAnswer2.IsEnabled = canInteract;
            if (uiMatchWindow.BtnAnswer3 != null) uiMatchWindow.BtnAnswer3.IsEnabled = canInteract;
            if (uiMatchWindow.BtnAnswer4 != null) uiMatchWindow.BtnAnswer4.IsEnabled = canInteract;

            wildcards.RefreshUseState(canUseWildcardNow());
        }
    }
}
