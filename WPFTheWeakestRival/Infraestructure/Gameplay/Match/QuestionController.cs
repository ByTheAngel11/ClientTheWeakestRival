// QuestionController.cs
using log4net;
using System;
using System.Globalization;
using System.Linq;
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

        private const string DarknessUnknownName = "???";
        private const string DarknessTurnLabel = "A oscuras";

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;
        private readonly GameplayClientProxy gameplay;
        private readonly QuestionTimerController timer;

        private int remainingSeconds;

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

        public void SetDarknessActive(bool isActive)
        {
            state.IsDarknessActive = isActive;

            if (ui.TurnAvatar != null)
            {
                ui.TurnAvatar.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
            }

            if (isActive)
            {
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

                return;
            }

            RestoreTurnIdentityFromLobby();
        }

        private void RestoreTurnIdentityFromLobby()
        {
            int userId = state.CurrentTurnUserId;

            PlayerSummary[] lobbyPlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();
            PlayerSummary p = lobbyPlayers.FirstOrDefault(x => x != null && x.UserId == userId);

            if (ui.TxtTurnPlayerName != null)
            {
                ui.TxtTurnPlayerName.Text = p != null && !string.IsNullOrWhiteSpace(p.DisplayName)
                    ? p.DisplayName
                    : MatchConstants.DEFAULT_PLAYER_NAME;
            }

            if (ui.TurnAvatar != null)
            {
                AvatarAppearance appearance = AvatarMapper.FromLobbyDto(p != null ? p.Avatar : null);
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
            SetBombQuestionUi(false);

            int targetUserId = targetPlayer != null ? targetPlayer.UserId : 0;
            bool isTargetEliminated = targetUserId > 0 && state.IsEliminated(targetUserId);

            bool isTargetMe = targetUserId == state.MyUserId;
            state.IsMyTurn = isTargetMe && !isTargetEliminated;

            state.CurrentQuestion = question;
            state.CurrentTurnUserId = targetUserId;

            highlightPlayer?.Invoke(targetUserId);

            if (ui.TxtChain != null)
            {
                ui.TxtChain.Text = currentChain.ToString(MatchConstants.POINTS_FORMAT);
            }

            if (ui.TxtBanked != null)
            {
                ui.TxtBanked.Text = banked.ToString(MatchConstants.POINTS_FORMAT);
            }

            if (state.IsDarknessActive)
            {
                if (ui.TurnAvatar != null) ui.TurnAvatar.Visibility = Visibility.Collapsed;
                if (ui.TxtTurnPlayerName != null) ui.TxtTurnPlayerName.Text = DarknessUnknownName;
                if (ui.TxtTurnLabel != null) ui.TxtTurnLabel.Text = DarknessTurnLabel;
            }
            else
            {
                if (ui.TurnAvatar != null)
                {
                    AvatarAppearance appearance = AvatarMapper.FromGameplayDto(targetPlayer != null ? targetPlayer.Avatar : null);
                    ui.TurnAvatar.Appearance = appearance;
                    ui.TurnAvatar.Visibility = Visibility.Visible;
                }

                if (ui.TxtTurnPlayerName != null)
                {
                    ui.TxtTurnPlayerName.Text = !string.IsNullOrWhiteSpace(targetPlayer != null ? targetPlayer.DisplayName : null)
                        ? targetPlayer.DisplayName
                        : MatchConstants.DEFAULT_PLAYER_NAME;
                }
            }

            if (ui.BtnBank != null)
            {
                ui.BtnBank.IsEnabled = state.IsMyTurn && !state.IsSurpriseExamActive;
            }

            if (state.IsMyTurn)
            {
                if (!state.IsDarknessActive)
                {
                    if (ui.TxtTurnLabel != null)
                    {
                        ui.TxtTurnLabel.Text = state.IsSurpriseExamActive
                            ? "Examen sorpresa: responde"
                            : MatchConstants.TURN_MY_TURN_TEXT;
                    }

                    if (ui.TurnBannerBackground != null)
                    {
                        ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource("Brush.Turn.MyTurn");
                    }
                }

                int limitSeconds = state.IsSurpriseExamActive
                    ? MatchConstants.SURPRISE_EXAM_TIME_SECONDS
                    : MatchConstants.QUESTION_TIME_SECONDS;

                remainingSeconds = limitSeconds;
                UpdateTimerText();
                timer.Start(limitSeconds);
            }
            else
            {
                if (!state.IsDarknessActive)
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

                timer.Stop();
                SetTimerText(MatchConstants.DEFAULT_TIMER_TEXT);
            }

            if (!state.IsMyTurn)
            {
                InitializeEmptyUi();

                if (ui.TxtQuestion != null)
                {
                    ui.TxtQuestion.Text = MatchConstants.DEFAULT_WAITING_TURN_TEXT;
                }

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

        public void OnAnswerEvaluated(GameplayServiceProxy.PlayerSummary player, GameplayServiceProxy.AnswerResult result)
        {
            if (ui.TxtAnswerFeedback == null)
            {
                return;
            }

            bool isMyPlayer = player != null && player.UserId == state.MyUserId;

            if (isMyPlayer)
            {
                state.MyTotalAnswers++;

                if (result != null && result.IsCorrect)
                {
                    state.MyCorrectAnswers++;
                }

                bool isCorrect = result != null && result.IsCorrect;

                ui.TxtAnswerFeedback.Text = isCorrect
                    ? MatchConstants.DEFAULT_CORRECT_TEXT
                    : MatchConstants.DEFAULT_INCORRECT_TEXT;

                ui.TxtAnswerFeedback.Foreground = isCorrect ? Brushes.LawnGreen : Brushes.OrangeRed;
                return;
            }

            if (state.IsDarknessActive)
            {
                ui.TxtAnswerFeedback.Text = "Respuesta registrada.";
                ui.TxtAnswerFeedback.Foreground = Brushes.LightGray;
                return;
            }

            string name = string.IsNullOrWhiteSpace(player != null ? player.DisplayName : null)
                ? MatchConstants.DEFAULT_OTHER_PLAYER_NAME
                : player.DisplayName;

            bool otherIsCorrect = result != null && result.IsCorrect;

            ui.TxtAnswerFeedback.Text = otherIsCorrect
                ? string.Format(CultureInfo.CurrentCulture, "{0} respondió correcto.", name)
                : string.Format(CultureInfo.CurrentCulture, "{0} respondió incorrecto.", name);

            ui.TxtAnswerFeedback.Foreground = Brushes.LightGray;
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

                int limitSeconds = state.IsSurpriseExamActive
                    ? MatchConstants.SURPRISE_EXAM_TIME_SECONDS
                    : MatchConstants.QUESTION_TIME_SECONDS;

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
            }
            else
            {
                button.Content = string.Empty;
                button.Tag = null;
                button.Visibility = Visibility.Collapsed;
            }
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
