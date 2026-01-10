using System;
using System.Linq;
using System.Threading;
using System.Windows;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure
{
    public static class SessionLogoutCoordinator
    {
        private static int isLoggingOut;

        private const string CODE_FORCED_LOGOUT = "FORCED_LOGOUT";
        private const string CODE_INVALID_SESSION = "INVALID_SESSION";

        public static void Attach(LobbyHub lobbyHub)
        {
            if (lobbyHub == null)
            {
                return;
            }

            lobbyHub.ForcedLogout -= OnForcedLogout;
            lobbyHub.ForcedLogout += OnForcedLogout;
        }

        public static bool IsSessionFault(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            return string.Equals(code, CODE_FORCED_LOGOUT, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, CODE_INVALID_SESSION, StringComparison.OrdinalIgnoreCase);
        }

        public static void ForceLogout(string code)
        {
            if (Interlocked.Exchange(ref isLoggingOut, 1) == 1)
            {
                return;
            }

            var app = Application.Current;
            if (app == null)
            {
                Interlocked.Exchange(ref isLoggingOut, 0);
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    LoginWindow.AppSession.CurrentToken = null;
                }
                catch
                {
                }

                try
                {
                    AppServices.StopAll();
                    AppServices.ResetAll();
                }
                catch
                {
                }

                var login = new LoginWindow();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.MainWindow = login;
                login.Show();

                var toClose = app.Windows.Cast<Window>()
                    .Where(w => w != null && w != login)
                    .ToList();

                foreach (Window w in toClose)
                {
                    try { w.Close(); } catch { }
                }

                Interlocked.Exchange(ref isLoggingOut, 0);
            }));
        }

        private static void OnForcedLogout(ForcedLogoutNotification notification)
        {
            ForceLogout(notification?.Code);
        }
    }
}
