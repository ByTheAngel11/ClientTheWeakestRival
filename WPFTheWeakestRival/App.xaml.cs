using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using log4net;
using log4net.Config;

namespace WPFTheWeakestRival
{
    public sealed partial class App : Application
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(App));

        private const string LOG_CTX_UI_UNHANDLED = "App.DispatcherUnhandledException";
        private const string LOG_CTX_TASK_UNOBSERVED = "App.TaskScheduler.UnobservedTaskException";
        private const string LOG_CTX_DOMAIN_UNHANDLED = "App.CurrentDomain.UnhandledException";

        private const string UI_ERROR_TITLE = "Error";
        private const string UI_GENERIC_ERROR_MESSAGE = "Ocurrió un error inesperado.";

        private const int EXIT_CODE_SUCCESS = 0;

        private void OnAppStartup(object sender, StartupEventArgs e)
        {
            ConfigureLogging();
            RegisterGlobalExceptionHandlers();

            OpenLoginWindow();
        }

        private void OnAppExit(object sender, ExitEventArgs e)
        {
            Shutdown(EXIT_CODE_SUCCESS);
        }

        private static void ConfigureLogging()
        {
            XmlConfigurator.Configure();
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OpenLoginWindow()
        {
            LoginWindow loginWindow = new LoginWindow();
            MainWindow = loginWindow;
            loginWindow.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;

            Logger.Error(LOG_CTX_UI_UNHANDLED, ex);

            ShowGenericError();
            e.Handled = true;
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Exception ex = e.Exception;

            Logger.Error(LOG_CTX_TASK_UNOBSERVED, ex);

            e.SetObserved();
            ShowGenericError();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            if (ex != null)
            {
                Logger.Error(LOG_CTX_DOMAIN_UNHANDLED, ex);
            }

            ShowGenericError();
        }

        private static void ShowGenericError()
        {
            MessageBox.Show(
                UI_GENERIC_ERROR_MESSAGE,
                UI_ERROR_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
