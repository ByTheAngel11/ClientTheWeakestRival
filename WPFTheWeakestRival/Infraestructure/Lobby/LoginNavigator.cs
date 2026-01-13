using System;
using System.Linq;
using System.Windows;
using log4net;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal interface ILoginNavigator
    {
        void NavigateFrom(Window currentWindow);
    }

    internal sealed class LoginNavigator : ILoginNavigator
    {
        private readonly ILog logger;

        internal LoginNavigator(ILog logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void NavigateFrom(Window currentWindow)
        {
            if (currentWindow == null)
            {
                return;
            }

            try
            {
                var existingLogin = Application.Current?.Windows.OfType<LoginWindow>().FirstOrDefault();
                if (existingLogin != null)
                {
                    Application.Current.MainWindow = existingLogin;
                    existingLogin.Show();
                    existingLogin.Activate();

                    currentWindow.Hide();
                    currentWindow.Close();
                    return;
                }

                Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

                var loginWindow = new LoginWindow();
                Application.Current.MainWindow = loginWindow;

                loginWindow.Show();
                loginWindow.Activate();

                currentWindow.Hide();
                currentWindow.Close();
            }
            catch (Exception ex)
            {
                logger.Error("LoginNavigator.NavigateFrom error.", ex);
            }
        }
    }
}
