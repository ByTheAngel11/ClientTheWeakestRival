using log4net;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyReconnectController : IDisposable
    {
        private const string ReconnectStatusStart = "Reconectando...";
        private const string ReconnectStatusAttemptFormat = "Reconectando... intento {0}";

        private const int ReconnectCycleDelaySeconds = 5;

        private const string ReconnectExhaustedMessage =
            "No se pudo reconectar con el servidor.\n\n¿Quieres quedarte en espera?\n\n" +
            "Sí: me quedo y seguiré intentando.\n" +
            "No: regresar al inicio de sesión.";

        private const string WaitingLineMessage = "Sin conexión. En espera...";

        private readonly LobbyUiDispatcher ui;
        private readonly LobbyRuntimeState state;
        private readonly Grid overlayGrid;
        private readonly TextBlock statusText;
        private readonly ILoginNavigator loginNavigator;
        private readonly LobbyChatController chatController;
        private readonly ILog logger;

        private readonly DispatcherTimer reconnectCycleTimer;

        internal LobbyReconnectController(
            LobbyUiDispatcher ui,
            LobbyRuntimeState state,
            Grid overlayGrid,
            TextBlock statusText,
            ILoginNavigator loginNavigator,
            LobbyChatController chatController,
            ILog logger)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.overlayGrid = overlayGrid;
            this.statusText = statusText;
            this.loginNavigator = loginNavigator ?? throw new ArgumentNullException(nameof(loginNavigator));
            this.chatController = chatController ?? throw new ArgumentNullException(nameof(chatController));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            reconnectCycleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ReconnectCycleDelaySeconds)
            };
            reconnectCycleTimer.Tick += ReconnectCycleTimerTick;

            AppServices.Lobby.ReconnectStarted += OnReconnectStartedFromHub;
            AppServices.Lobby.ReconnectAttempted += OnReconnectAttemptedFromHub;
            AppServices.Lobby.ReconnectStopped += OnReconnectStoppedFromHub;
            AppServices.Lobby.ReconnectExhausted += OnReconnectExhaustedFromHub;
        }

        private void OnReconnectStartedFromHub()
        {
            ui.Ui(() => ShowOverlay(ReconnectStatusStart));
        }

        private void OnReconnectAttemptedFromHub(int attempt)
        {
            ui.Ui(() =>
                ShowOverlay(string.Format(CultureInfo.InvariantCulture, ReconnectStatusAttemptFormat, attempt)));
        }

        private void OnReconnectStoppedFromHub()
        {
            ui.Ui(() =>
            {
                state.IsAutoWaitingForReconnect = false;

                if (reconnectCycleTimer.IsEnabled)
                {
                    reconnectCycleTimer.Stop();
                }

                HideOverlay();
            });
        }

        private void OnReconnectExhaustedFromHub()
        {
            ui.Ui(() =>
            {
                try
                {
                    if (state.IsAutoWaitingForReconnect)
                    {
                        StartNextReconnectCycle();
                        return;
                    }

                    MessageBoxResult result = MessageBox.Show(
                        ReconnectExhaustedMessage,
                        Lang.lobbyTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        state.IsAutoWaitingForReconnect = true;

                        chatController.AppendSystemLine(WaitingLineMessage);
                        ShowOverlay(WaitingLineMessage);

                        StartNextReconnectCycle();
                        return;
                    }

                    var currentWindow = Application.Current?.MainWindow;
                    if (currentWindow != null)
                    {
                        loginNavigator.NavigateFrom(currentWindow);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Reconnect exhausted handler error.", ex);
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

                AppServices.Lobby.ContinueReconnectCycle();
            }
            catch (Exception ex)
            {
                logger.Warn("ContinueReconnectCycle error.", ex);
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

        public void Dispose()
        {
            AppServices.Lobby.ReconnectStarted -= OnReconnectStartedFromHub;
            AppServices.Lobby.ReconnectAttempted -= OnReconnectAttemptedFromHub;
            AppServices.Lobby.ReconnectStopped -= OnReconnectStoppedFromHub;
            AppServices.Lobby.ReconnectExhausted -= OnReconnectExhaustedFromHub;

            reconnectCycleTimer.Tick -= ReconnectCycleTimerTick;

            if (reconnectCycleTimer.IsEnabled)
            {
                reconnectCycleTimer.Stop();
            }
        }
    }
}
