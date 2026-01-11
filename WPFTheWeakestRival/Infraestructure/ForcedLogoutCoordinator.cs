using log4net;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infraestructure
{
    internal static class ForcedLogoutCoordinator
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ForcedLogoutCoordinator));

        private const byte ACCOUNT_STATUS_SUSPENDED = 3;
        private const byte ACCOUNT_STATUS_BANNED = 4;

        private const string SANCTION_END_TIME_FORMAT = "g";

        private static int isHandling;

        public static void Handle(ForcedLogoutNotification notification)
        {
            if (!TryBeginHandling(notification, out Application app))
            {
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowMessageSafe(notification);
                ClearSessionSafe();
                ResetServicesSafe();

                Window loginWindow = ShowLoginWindowSafe(app);

                CloseOtherWindowsSafe(app, loginWindow);
                EndHandling();
            }));
        }

        private static bool TryBeginHandling(ForcedLogoutNotification notification, out Application app)
        {
            app = null;

            if (notification == null)
            {
                return false;
            }

            if (Interlocked.Exchange(ref isHandling, 1) == 1)
            {
                return false;
            }

            app = Application.Current;
            if (app == null)
            {
                EndHandling();
                return false;
            }

            return true;
        }

        private static void EndHandling()
        {
            Interlocked.Exchange(ref isHandling, 0);
        }

        private static void ShowMessageSafe(ForcedLogoutNotification notification)
        {
            try
            {
                ShowMessage(notification);
            }
            catch (Exception ex)
            {
                Logger.Warn("ForcedLogoutCoordinator: error showing message.", ex);
            }
        }

        private static void ClearSessionSafe()
        {
            try
            {
                LoginWindow.AppSession.CurrentToken = null;
            }
            catch (Exception ex)
            {
                Logger.Error("ForcedLogoutCoordinator: error clearing session.", ex);
            }
        }

        private static void ResetServicesSafe()
        {
            try
            {
                AppServices.StopAll();
                AppServices.ResetAll();
            }
            catch (Exception ex)
            {
                Logger.Error("ForcedLogoutCoordinator: error disposing services.", ex);
            }
        }

        private static Window ShowLoginWindowSafe(Application app)
        {
            var loginWindow = new LoginWindow();

            try
            {
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.MainWindow = loginWindow;
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                Logger.Error("ForcedLogoutCoordinator: error showing LoginWindow.", ex);
            }

            return loginWindow;
        }

        private static void CloseOtherWindowsSafe(Application app, Window loginWindow)
        {
            try
            {
                Window[] windows = app.Windows.Cast<Window>().ToArray();

                foreach (Window window in windows)
                {
                    CloseWindowIfNeededSafe(window, loginWindow);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("ForcedLogoutCoordinator: error closing windows.", ex);
            }
        }

        private static void CloseWindowIfNeededSafe(Window window, Window loginWindow)
        {
            if (window == null || ReferenceEquals(window, loginWindow))
            {
                return;
            }

            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                Logger.Warn("ForcedLogoutCoordinator: error closing window.", ex);
            }
        }

        private static void ShowMessage(ForcedLogoutNotification notification)
        {
            if (notification == null)
            {
                return;
            }

            if (notification.SanctionType == ACCOUNT_STATUS_SUSPENDED && notification.SanctionEndAtUtc.HasValue)
            {
                string localEnd = notification.SanctionEndAtUtc.Value
                    .ToLocalTime()
                    .ToString(SANCTION_END_TIME_FORMAT);

                MessageBox.Show(
                    string.Format(Lang.reportSanctionTemporary, localEnd),
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (notification.SanctionType == ACCOUNT_STATUS_BANNED)
            {
                MessageBox.Show(
                    Lang.reportSanctionPermanent,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            MessageBox.Show(
                Lang.noValidSessionCode,
                Lang.reportPlayer,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
