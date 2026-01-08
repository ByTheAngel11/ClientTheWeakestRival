using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
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

        private static int _isHandling;

        public static void Handle(ForcedLogoutNotification notification)
        {
            if (notification == null)
            {
                return;
            }

            if (Interlocked.Exchange(ref _isHandling, 1) == 1)
            {
                return;
            }

            Application app = Application.Current;
            if (app == null)
            {
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    ShowMessage(notification);
                }
                catch (Exception ex)
                {
                    Logger.Warn("ForcedLogoutCoordinator: error showing message.", ex);
                }

                try
                {
                    LoginWindow.AppSession.CurrentToken = null;
                }
                catch (Exception ex)
                {
                    Logger.Error("ForcedLogoutCoordinator: error clearing session.", ex);
                }

                try
                {
                    AppServices.StopAll();
                    AppServices.ResetAll();
                }
                catch (Exception ex)
                {
                    Logger.Error("ForcedLogoutCoordinator: error disposing services.", ex);
                }

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

                try
                {
                    var windows = app.Windows.Cast<Window>().ToList();
                    foreach (var w in windows)
                    {
                        if (w != null && !ReferenceEquals(w, loginWindow))
                        {
                            try { w.Close(); }
                            catch (Exception ex)
                            {
                                Logger.Warn("ForcedLogoutCoordinator: error closing window.", ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("ForcedLogoutCoordinator: error closing windows.", ex);
                }
            }));
        }

        private static void ShowMessage(ForcedLogoutNotification notification)
        {
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
