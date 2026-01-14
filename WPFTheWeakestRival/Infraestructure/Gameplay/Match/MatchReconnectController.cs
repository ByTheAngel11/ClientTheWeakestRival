using log4net;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchReconnectController : IDisposable
    {
        private const string RECONNECT_STATUS_START = "Reconectando...";
        private const string RECONNECT_STATUS_ATTEMPT_FORMAT = "Reconectando... intento {0}";

        private const int RECONNECT_CYCLE_DELAY_SECONDS = 5;

        private const string RECONNECT_EXHAUSTED_MESSAGE =
            "No se pudo reconectar con el servidor.\n\n¿Quieres quedarte en espera?\n\n" +
            "Sí: me quedo y seguiré intentando.\n" +
            "No: regresar al lobby.";

        private const string WAITING_LINE_MESSAGE = "Sin conexión. En espera...";

        private readonly Grid overlayGrid;
        private readonly TextBlock statusText;
        private readonly Dispatcher dispatcher;
        private readonly GameplayHub hub;
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
            this.hub = hub ?? throw new ArgumentNullException(nameof(hub));
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
            Ui(() => ShowOverlay(RECONNECT_STATUS_START));
        }

        private void OnReconnectAttemptedFromHub(int attempt)
        {
            Ui(() =>
                ShowOverlay(string.Format(CultureInfo.InvariantCulture, RECONNECT_STATUS_ATTEMPT_FORMAT, attempt)));
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
                        RECONNECT_EXHAUSTED_MESSAGE,
                        MatchConstants.GAME_MESSAGE_TITLE,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        ShowOverlay(WAITING_LINE_MESSAGE);
                        StartNextReconnectCycle();
                        return;
                    }

                    returnToLobby();
                }
                catch (Exception ex)
                {
                    logger.Error("MatchReconnect exhausted handler error.", ex);
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

                hub.ContinueReconnectCycle();
            }
            catch (Exception ex)
            {
                logger.Warn("MatchReconnect ContinueReconnectCycle error.", ex);
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
                logger.Warn("MatchReconnect Ui error.", ex);
            }
        }

        public void Dispose()
        {
            hub.ReconnectStarted -= OnReconnectStartedFromHub;
            hub.ReconnectAttempted -= OnReconnectAttemptedFromHub;
            hub.ReconnectStopped -= OnReconnectStoppedFromHub;
            hub.ReconnectExhausted -= OnReconnectExhaustedFromHub;

            reconnectCycleTimer.Tick -= ReconnectCycleTimerTick;

            if (reconnectCycleTimer.IsEnabled)
            {
                reconnectCycleTimer.Stop();
            }
        }
    }
}
