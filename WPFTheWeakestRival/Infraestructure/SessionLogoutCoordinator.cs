using log4net;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure
{
    public static class SessionLogoutCoordinator
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SessionLogoutCoordinator));

        private const string LOG_CTX_FORCE_LOGOUT = "SessionLogoutCoordinator.ForceLogout";
        private const string LOG_CTX_DISPATCHER_INVOKE = "SessionLogoutCoordinator.Dispatcher.BeginInvoke";
        private const string LOG_CTX_CLEAR_TOKEN = "SessionLogoutCoordinator.ClearCurrentTokenSafe";
        private const string LOG_CTX_RESET_SERVICES = "SessionLogoutCoordinator.ResetServicesSafe";
        private const string LOG_CTX_OPEN_LOGIN = "SessionLogoutCoordinator.OpenLoginWindow";
        private const string LOG_CTX_CLOSE_WINDOWS = "SessionLogoutCoordinator.CloseWindows";

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

            Application app = Application.Current;
            if (app == null)
            {
                Logger.WarnFormat("{0}: Application.Current is null. Code={1}", LOG_CTX_FORCE_LOGOUT, code ?? string.Empty);
                Interlocked.Exchange(ref isLoggingOut, 0);
                return;
            }

            try
            {
                app.Dispatcher.BeginInvoke(new Action(() => ExecuteLogoutOnUiThread(app, code)));
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_CTX_DISPATCHER_INVOKE, ex);
                Interlocked.Exchange(ref isLoggingOut, 0);
            }
        }

        private static void ExecuteLogoutOnUiThread(Application app, string code)
        {
            try
            {
                Logger.WarnFormat("{0}: forced logout triggered. Code={1}", LOG_CTX_FORCE_LOGOUT, code ?? string.Empty);

                ClearCurrentTokenSafe();
                ResetServicesSafe();

                LoginWindow login = OpenLoginWindow(app);
                CloseOtherWindows(app, login);
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_CTX_FORCE_LOGOUT, ex);
            }
            finally
            {
                Interlocked.Exchange(ref isLoggingOut, 0);
            }
        }

        private static void ClearCurrentTokenSafe()
        {
            try
            {
                LoginWindow.AppSession.CurrentToken = null;
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_CTX_CLEAR_TOKEN, ex);
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
                Logger.Warn(LOG_CTX_RESET_SERVICES, ex);
            }
        }

        private static LoginWindow OpenLoginWindow(Application app)
        {
            try
            {
                var login = new LoginWindow();

                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.MainWindow = login;

                login.Show();

                return login;
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_CTX_OPEN_LOGIN, ex);
                throw;
            }
        }

        private static void CloseOtherWindows(Application app, Window keepOpen)
        {
            try
            {
                var toClose = app.Windows.Cast<Window>()
                    .Where(w => w != null && w != keepOpen)
                    .ToList();

                for (int i = 0; i < toClose.Count; i++)
                {
                    CloseWindowSafe(toClose[i]);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_CTX_CLOSE_WINDOWS, ex);
            }
        }

        private static void CloseWindowSafe(Window window)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                Logger.Debug("SessionLogoutCoordinator.CloseWindowSafe: close failed.", ex);
            }
        }

        private static void OnForcedLogout(ForcedLogoutNotification notification)
        {
            ForceLogout(notification?.Code);
        }
    }
}
