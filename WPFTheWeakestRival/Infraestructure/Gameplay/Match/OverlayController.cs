using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using log4net;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class OverlayController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(OverlayController));

        private readonly MatchWindowUiRefs uiMatchWindow;

        private GameplayServiceProxy.CoinFlipResolvedDto lastCoinFlip;

        public OverlayController(MatchWindowUiRefs ui)
        {
            this.uiMatchWindow = ui ?? throw new ArgumentNullException(nameof(ui));
        }

        public void HideIntro()
        {
            if (uiMatchWindow.IntroOverlay != null)
            {
                uiMatchWindow.IntroOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public void StopIntroVideoSafe()
        {
            try
            {
                if (uiMatchWindow.IntroVideo != null)
                {
                    uiMatchWindow.IntroVideo.Stop();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("OverlayController.StopIntroVideoSafe error.", ex);
            }
        }

        public void ShowSpecialEvent(string title, string description)
        {
            if (uiMatchWindow.SpecialEventTitleText != null)
            {
                uiMatchWindow.SpecialEventTitleText.Text = string.IsNullOrWhiteSpace(title) ? string.Empty : title;
            }

            if (uiMatchWindow.SpecialEventDescriptionText != null)
            {
                uiMatchWindow.SpecialEventDescriptionText.Text = string.IsNullOrWhiteSpace(description) ? string.Empty : description;
            }

            if (uiMatchWindow.SpecialEventOverlay != null)
            {
                uiMatchWindow.SpecialEventOverlay.Visibility = Visibility.Visible;
            }
        }

        public void HideSpecialEvent()
        {
            if (uiMatchWindow.SpecialEventOverlay != null)
            {
                uiMatchWindow.SpecialEventOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public void ShowCoinFlip(GameplayServiceProxy.CoinFlipResolvedDto coinFlip)
        {
            lastCoinFlip = coinFlip;

            if (uiMatchWindow.CoinFlipOverlay == null || uiMatchWindow.CoinFlipResultText == null)
            {
                string fallback = coinFlip != null && coinFlip.Result == GameplayServiceProxy.CoinFlipResultType.Heads
                    ? MatchConstants.COIN_FLIP_HEADS_MESSAGE
                    : MatchConstants.COIN_FLIP_TAILS_MESSAGE;

                MessageBox.Show(
                    fallback,
                    MatchConstants.COIN_FLIP_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string message = coinFlip != null && coinFlip.Result == GameplayServiceProxy.CoinFlipResultType.Heads
                ? MatchConstants.COIN_FLIP_HEADS_MESSAGE
                : MatchConstants.COIN_FLIP_TAILS_MESSAGE;

            uiMatchWindow.CoinFlipResultText.Text = message;
            uiMatchWindow.CoinFlipOverlay.Visibility = Visibility.Visible;

            Storyboard storyboard = uiMatchWindow.Window.TryFindResource("CoinFlipStoryboard") as Storyboard;
            if (storyboard != null)
            {
                storyboard.Completed -= CoinFlipStoryboardCompleted;
                storyboard.Completed += CoinFlipStoryboardCompleted;
                storyboard.Begin();
            }
        }

        private void CoinFlipStoryboardCompleted(object sender, EventArgs e)
        {
            try
            {
                if (uiMatchWindow.CoinFlipOverlay != null)
                {
                    uiMatchWindow.CoinFlipOverlay.Visibility = Visibility.Collapsed;
                }

                if (lastCoinFlip != null && lastCoinFlip.ShouldEnableDuel)
                {
                    MessageBox.Show(
                        "Habrá duelo.",
                        MatchConstants.COIN_FLIP_TITLE,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("OverlayController.CoinFlipStoryboardCompleted error.", ex);
            }
        }

        public async Task AutoHideSpecialEventAsync(int milliseconds)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            await Task.Delay(milliseconds);

            HideSpecialEvent();
        }
    }
}
