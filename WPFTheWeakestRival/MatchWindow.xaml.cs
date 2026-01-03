using System;
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

            state = new MatchSessionState(match, token, myUserId, isHost);

            var ui = new MatchWindowUiRefs(
                window: this,
                txtMatchCodeSmall: txtMatchCodeSmall,
                lstPlayers: lstPlayers,
                txtPlayersSummary: txtPlayersSummary,
                txtChain: txtChain,
                txtBanked: txtBanked,
                txtTurnPlayerName: txtTurnPlayerName,
                txtTurnLabel: txtTurnLabel,
                txtTimer: txtTimer,
                txtQuestion: txtQuestion,
                txtAnswerFeedback: txtAnswerFeedback,
                txtPhase: txtPhase,
                txtWildcardName: txtWildcardName,
                txtWildcardDescription: txtWildcardDescription,
                imgWildcardIcon: imgWildcardIcon,
                btnWildcardPrev: btnWildcardPrev,
                btnWildcardNext: btnWildcardNext,
                btnAnswer1: btnAnswer1,
                btnAnswer2: btnAnswer2,
                btnAnswer3: btnAnswer3,
                btnAnswer4: btnAnswer4,
                btnBank: btnBank,
                turnBannerBackground: TurnBannerBackground,
                turnAvatar: TurnAvatar,
                introOverlay: IntroOverlay,
                introVideo: introVideo,
                coinFlipOverlay: CoinFlipOverlay,
                coinFlipResultText: CoinFlipResultText,
                specialEventOverlay: SpecialEventOverlay,
                specialEventTitleText: SpecialEventTitleText,
                specialEventDescriptionText: SpecialEventDescriptionText);

            coordinator = new MatchSessionCoordinator(ui, state);

            Loaded += MatchWindowLoaded;
            Closed += (s, e) =>
            {
                try
                {
                    coordinator.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Warn("MatchWindow.Closed: coordinator.Dispose error.", ex);
                }

                try
                {
                    if (lobbyWindow != null)
                    {
                        lobbyWindow.Show();
                        lobbyWindow.Activate();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("MatchWindow.Closed: lobbyWindow.Show/Activate error.", ex);
                }
            };
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

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                coordinator.OnCloseRequested(
                    closeWindow: Close,
                    showResultAndClose: () => coordinator.ShowResultAndClose(Close));
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
    }
}
