using log4net;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchReconnectController : IDisposable
    {
        private const int RECONNECT_CYCLE_DELAY_SECONDS = 5;

        private const string LOG_EXHAUSTED_HANDLER_ERROR = "MatchReconnect exhausted handler error.";
        private const string LOG_CONTINUE_RECONNECT_CYCLE_ERROR = "MatchReconnect ContinueReconnectCycle error.";
        private const string LOG_UI_ERROR = "MatchReconnect Ui error.";

        private readonly Grid overlayGrid;
        private readonly TextBlock statusText;
        private readonly Dispatcher dispatcher;
        private readonly GameplayHub hubGameplay;
        private readonly Action returnToLobby;
        private readonly ILog logger;

        private readonly DispatcherTimer reconnectCycleTimer;

        internal MatchReconnectController(
            Dispatcher dispatcher,
            GameplayHub hub,
            Grid overlayGrid,
            TextBlock statusText,
            Action returnToLobby,
            ILog logger)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.hubGameplay = hub ?? throw new ArgumentNullException(nameof(hub));
            this.overlayGrid = overlayGrid;
            this.statusText = statusText;
            this.returnToLobby = returnToLobby ?? throw new ArgumentNullException(nameof(returnToLobby));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            reconnectCycleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(RECONNECT_CYCLE_DELAY_SECONDS)
            };
            reconnectCycleTimer.Tick += ReconnectCycleTimerTick;

            hub.ReconnectStarted += OnReconnectStartedFromHub;
            hub.ReconnectAttempted += OnReconnectAttemptedFromHub;
            hub.ReconnectStopped += OnReconnectStoppedFromHub;
            hub.ReconnectExhausted += OnReconnectExhaustedFromHub;
        }

        private void OnReconnectStartedFromHub()
        {
            Ui(() => ShowOverlay(Lang.reconnectStatusStart));
        }

        private void OnReconnectAttemptedFromHub(int attempt)
        {
            Ui(() =>
                ShowOverlay(string.Format(
                    CultureInfo.CurrentCulture,
                    Lang.reconnectStatusAttemptFormat,
                    attempt)));
        }

        private void OnReconnectStoppedFromHub()
        {
            Ui(() =>
            {
                if (reconnectCycleTimer.IsEnabled)
                {
                    reconnectCycleTimer.Stop();
                }

                HideOverlay();
            });
        }

        private void OnReconnectExhaustedFromHub()
        {
            Ui(() =>
            {
                try
                {
                    MessageBoxResult result = MessageBox.Show(
                        Lang.reconnectExhaustedMessage,
                        Lang.gameMessageTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        ShowOverlay(Lang.reconnectWaitingLineMessage);
                        StartNextReconnectCycle();
                        return;
                    }

                    returnToLobby();
                }
                catch (Exception ex)
                {
                    logger.Error(LOG_EXHAUSTED_HANDLER_ERROR, ex);
                }
            });
        }

        private void StartNextReconnectCycle()
        {
            if (reconnectCycleTimer.IsEnabled)
            {
                reconnectCycleTimer.Stop();
            }

            reconnectCycleTimer.Start();
        }

        private void ReconnectCycleTimerTick(object sender, EventArgs e)
        {
            try
            {
                if (reconnectCycleTimer.IsEnabled)
                {
                    reconnectCycleTimer.Stop();
                }

                hubGameplay.ContinueReconnectCycle();
            }
            catch (Exception ex)
            {
                logger.Warn(LOG_CONTINUE_RECONNECT_CYCLE_ERROR, ex);
            }
        }

        private void ShowOverlay(string status)
        {
            if (statusText != null)
            {
                statusText.Text = status ?? string.Empty;
            }

            if (overlayGrid != null)
            {
                overlayGrid.Visibility = Visibility.Visible;
                overlayGrid.IsHitTestVisible = true;
            }
        }

        private void HideOverlay()
        {
            if (overlayGrid != null)
            {
                overlayGrid.Visibility = Visibility.Collapsed;
                overlayGrid.IsHitTestVisible = false;
            }
        }

        private void Ui(Action action)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                if (dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                dispatcher.BeginInvoke(action, DispatcherPriority.Send);
            }
            catch (Exception ex)
            {
                logger.Warn(LOG_UI_ERROR, ex);
            }
        }

        public void Dispose()
        {
            hubGameplay.ReconnectStarted -= OnReconnectStartedFromHub;
            hubGameplay.ReconnectAttempted -= OnReconnectAttemptedFromHub;
            hubGameplay.ReconnectStopped -= OnReconnectStoppedFromHub;
            hubGameplay.ReconnectExhausted -= OnReconnectExhaustedFromHub;

            reconnectCycleTimer.Tick -= ReconnectCycleTimerTick;

            if (reconnectCycleTimer.IsEnabled)
            {
                reconnectCycleTimer.Stop();
            }
        }
    }
}
