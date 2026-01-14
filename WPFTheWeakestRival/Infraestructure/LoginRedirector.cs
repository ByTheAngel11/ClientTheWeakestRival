using log4net;
using System;
using System.Windows;
using System.Windows.Threading;

namespace WPFTheWeakestRival.Infrastructure
{
    internal static class LoginRedirector
    {
        private const string DEFAULT_CONTEXT = "LoginRedirector.RedirectToLogin";

        internal static void RedirectToLogin(DependencyObject origin, ILog logger, string context)
        {
            ExecuteOnUiThread(() =>
            {
                Window currentWindow = origin == null ? null : Window.GetWindow(origin);
                RedirectToLogin(currentWindow, logger, context);
            });
        }

        internal static void RedirectToLogin(Window currentWindow, ILog logger, string context)
        {
            ExecuteOnUiThread(() =>
            {
                try
                {
                    SessionCleanup.Shutdown(string.IsNullOrWhiteSpace(context) ? DEFAULT_CONTEXT : context);

                    var loginWindow = new LoginWindow();
                    Application.Current.MainWindow = loginWindow;
                    loginWindow.Show();

                    if (currentWindow != null && !ReferenceEquals(currentWindow, loginWindow))
                    {
                        currentWindow.Close();
                    }
                }
                catch (Exception ex)
                {
                    logger?.Error("LoginRedirector.RedirectToLogin failed.", ex);
                }
            });
        }

        private static void ExecuteOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }
    }
}
