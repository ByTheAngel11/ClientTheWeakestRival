using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using log4net;
using WPFTheWeakestRival.Infrastructure.Gameplay.Match;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival
{
    public partial class MatchWindow : Window
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchWindow));

        private readonly MatchSessionState state;
        private readonly MatchSessionCoordinator coordinator;
        private readonly LobbyWindow lobbyWindow;

        private bool isCloseFlowInProgress;
        private bool skipClosePrompt;
        private bool skipCloseConfirmation;
        private bool skipExitPrompt;
        private bool skipReturnToLobbyOnClose;
        private Action postCloseAction;

        public MatchWindow(
            MatchInfo match,
            string token,
            int myUserId,
            bool isHost,
            LobbyWindow lobbyWindow)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            InitializeComponent();

            this.lobbyWindow = lobbyWindow;

            state = new MatchSessionState(match, token, myUserId, isHost);

            var ui = new MatchWindowUiRefs(
                new MatchWindowUiRefsArgs
                {
                    Window = this,

                    TxtMatchCodeSmall = txtMatchCodeSmall,
                    LstPlayers = lstPlayers,
                    TxtPlayersSummary = txtPlayersSummary,

                    TxtChain = txtChain,
                    TxtBanked = txtBanked,

                    TxtTurnPlayerName = txtTurnPlayerName,
                    TxtTurnLabel = txtTurnLabel,
                    TxtTimer = txtTimer,

                    TxtQuestion = txtQuestion,
                    TxtAnswerFeedback = txtAnswerFeedback,

                    TxtPhase = txtPhase,

                    TxtWildcardName = txtWildcardName,
                    TxtWildcardDescription = txtWildcardDescription,
                    ImgWildcardIcon = imgWildcardIcon,

                    BtnWildcardPrev = btnWildcardPrev,
                    BtnWildcardNext = btnWildcardNext,
                    BtnUseWildcard = btnUseWildcard,

                    BtnAnswer1 = btnAnswer1,
                    BtnAnswer2 = btnAnswer2,
                    BtnAnswer3 = btnAnswer3,
                    BtnAnswer4 = btnAnswer4,

                    BtnBank = btnBank,

                    TurnBannerBackground = TurnBannerBackground,
                    TurnAvatar = TurnAvatar,

                    IntroOverlay = IntroOverlay,
                    IntroVideo = introVideo,

                    CoinFlipOverlay = CoinFlipOverlay,
                    CoinFlipResultText = CoinFlipResultText,

                    SpecialEventOverlay = SpecialEventOverlay,
                    SpecialEventTitleText = SpecialEventTitleText,
                    SpecialEventDescriptionText = SpecialEventDescriptionText,

                    GrdReconnectOverlay = grdReconnectOverlay,
                    TxtReconnectStatus = txtReconnectStatus
                });


            coordinator = new MatchSessionCoordinator(ui, state);

            Loaded += MatchWindowLoaded;
            Closing += MatchWindowClosing;
            Closed += MatchWindowClosed;
        }

        private async void MatchWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await coordinator.InitializeAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("MatchWindowLoaded error.", ex);

                MessageBox.Show(
                    ex.Message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MatchWindowClosing(object sender, CancelEventArgs e)
        {
            if (isCloseFlowInProgress)
            {
                return;
            }

            if (skipClosePrompt)
            {
                return;
            }

            e.Cancel = true;
            RequestCloseFlow();
        }


        private void MatchWindowClosed(object sender, EventArgs e)
        {
            coordinator.Dispose();

            if (skipReturnToLobbyOnClose)
            {
                return; 
            }

            lobbyWindow?.Show();
            lobbyWindow?.Activate();
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                RequestCloseFlow();
            }
            catch (Exception ex)
            {
                Logger.Error("BtnCloseClick error.", ex);

                MessageBox.Show(
                    ex.Message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RequestCloseFlow()
        {
            coordinator.OnCloseRequested(
                closeWindow: ForceClose,
                showResultAndClose: () => coordinator.ShowResultAndClose(ForceClose));
        }

        private void ForceClose()
        {
            if (isCloseFlowInProgress)
            {
                return;
            }

            isCloseFlowInProgress = true;
            Close();
        }

        private async void AnswerButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            try
            {
                await coordinator.OnAnswerButtonClickAsync(button);
            }
            catch (Exception ex)
            {
                Logger.Error("AnswerButtonClick error.", ex);

                MessageBox.Show(
                    ex.Message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnBankClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await coordinator.OnBankClickAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("BtnBankClick error.", ex);

                MessageBox.Show(
                    ex.Message,
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnUseWildcardClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await coordinator.OnUseWildcardClickAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("BtnUseWildcardClick error.", ex);

                MessageBox.Show(
                    ex.Message,
                    MatchConstants.WILDCARDS_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnWildcardPrevClick(object sender, RoutedEventArgs e)
        {
            try
            {
                coordinator.OnWildcardPrev();
            }
            catch (Exception ex)
            {
                Logger.Error("BtnWildcardPrevClick error.", ex);
            }
        }

        private void BtnWildcardNextClick(object sender, RoutedEventArgs e)
        {
            try
            {
                coordinator.OnWildcardNext();
            }
            catch (Exception ex)
            {
                Logger.Error("BtnWildcardNextClick error.", ex);
            }
        }

        private void IntroVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            try
            {
                coordinator.OnIntroEnded();
            }
            catch (Exception ex)
            {
                Logger.Error("IntroVideo_MediaEnded error.", ex);
            }
        }

        private void BtnSkipIntroClick(object sender, RoutedEventArgs e)
        {
            try
            {
                coordinator.OnSkipIntro();
            }
            catch (Exception ex)
            {
                Logger.Error("BtnSkipIntroClick error.", ex);
            }
        }

        private void SpecialEventCloseButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                coordinator.OnCloseSpecialEvent();
            }
            catch (Exception ex)
            {
                Logger.Error("SpecialEventCloseButtonClick error.", ex);
            }
        }

        public void SetSkipReturnToLobbyOnClose()
        {
            skipExitPrompt = true;
        }

        public void CloseToLogin(Action showLogin)
        {
            skipClosePrompt = true;
            skipReturnToLobbyOnClose = true;

            showLogin?.Invoke();

            ForceClose();
        }

    }
}