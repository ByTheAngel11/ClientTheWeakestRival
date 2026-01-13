using log4net;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class LightningChallengeController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LightningChallengeController));

        private const string LightningMyTurnLabel = "Reto relámpago: tu turno";
        private const string LightningInProgressLabel = "Reto relámpago en curso";

        private const string LightningSuccessTemplate = "¡Has completado el reto relámpago! Respuestas correctas: {0}.";
        private const string LightningFailTemplate = "Reto relámpago finalizado. Respuestas correctas: {0}.";

        private const string BrushTurnMyTurn = "Brush.Turn.MyTurn";
        private const string BrushTurnOtherTurn = "Brush.Turn.OtherTurn";

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;
        private readonly WildcardController wildcards;
        private readonly QuestionController questions;
        private readonly QuestionTimerController timer;
        private readonly MatchPhaseLabelController phaseController;
        private readonly MatchInputController inputController;
        private readonly Action refreshWildcardUseState;

        private int lightningTimeLimitSeconds;

        internal LightningChallengeController(
            MatchWindowUiRefs ui,
            MatchSessionState state,
            WildcardController wildcards,
            QuestionController questions,
            QuestionTimerController timer,
            MatchPhaseLabelController phaseController,
            MatchInputController inputController,
            Action refreshWildcardUseState)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.wildcards = wildcards ?? throw new ArgumentNullException(nameof(wildcards));
            this.questions = questions ?? throw new ArgumentNullException(nameof(questions));
            this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
            this.phaseController = phaseController ?? throw new ArgumentNullException(nameof(phaseController));
            this.inputController = inputController ?? throw new ArgumentNullException(nameof(inputController));
            this.refreshWildcardUseState = refreshWildcardUseState ?? throw new ArgumentNullException(nameof(refreshWildcardUseState));
        }

        internal void HandleStarted(GameplayServiceProxy.PlayerSummary targetPlayer, int totalTimeSeconds)
        {
            state.CurrentPhase = MatchPhase.SpecialEvent;
            phaseController.UpdatePhaseLabel();

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
                ui.TxtTurnLabel.Text = state.IsMyTurn ? LightningMyTurnLabel : LightningInProgressLabel;
            }

            if (ui.TurnBannerBackground != null)
            {
                string brushKey = state.IsMyTurn ? BrushTurnMyTurn : BrushTurnOtherTurn;
                ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource(brushKey);
            }

            wildcards.RefreshUseState(false);
        }

        internal void HandleQuestion(GameplayServiceProxy.QuestionWithAnswersDto question)
        {
            state.CurrentPhase = MatchPhase.SpecialEvent;
            phaseController.UpdatePhaseLabel();

            questions.OnLightningQuestion(question, lightningTimeLimitSeconds);
        }

        internal async Task HandleFinishedAsync(int correctAnswers, bool isSuccess)
        {
            try
            {
                timer.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn("HandleFinishedAsync timer stop error.", ex);
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

            if (!state.IsMatchFinished)
            {
                state.CurrentPhase = state.IsInFinalPhase() ? MatchPhase.Final : MatchPhase.NormalRound;
            }

            phaseController.UpdatePhaseLabel();

            refreshWildcardUseState();
            inputController.RefreshGameplayInputs();
        }
    }
}
