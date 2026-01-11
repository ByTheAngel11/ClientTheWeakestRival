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
        private const string LOG_CTX_SHOW_GENERIC_ERROR = "App.ShowGenericErrorSafe";

        private const string UI_ERROR_TITLE = "Error";
        private const string UI_GENERIC_ERROR_MESSAGE = "Ocurrió un error inesperado.";

        private const int EXIT_CODE_SUCCESS = 0;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureLogging();

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var loginWindow = new LoginWindow();
            MainWindow = loginWindow;
            loginWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (e != null)
            {
                e.ApplicationExitCode = EXIT_CODE_SUCCESS;
            }

            base.OnExit(e);
        }

        private static void ConfigureLogging()
        {
            XmlConfigurator.Configure();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e != null ? e.Exception : null;

            if (ex != null)
            {
                Logger.Error(LOG_CTX_UI_UNHANDLED, ex);
            }
            else
            {
                Logger.ErrorFormat("{0}: dispatcher exception is null.", LOG_CTX_UI_UNHANDLED);
            }

            ShowGenericErrorSafe();

            if (e != null)
            {
                e.Handled = true;
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Exception ex = e != null ? e.Exception : null;

            if (ex != null)
            {
                Logger.Error(LOG_CTX_TASK_UNOBSERVED, ex);
            }
            else
            {
                Logger.ErrorFormat("{0}: task exception is null.", LOG_CTX_TASK_UNOBSERVED);
            }

            if (e != null)
            {
                e.SetObserved();
            }

            ShowGenericErrorSafe();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            object exceptionObject = e != null ? e.ExceptionObject : null;
            Exception ex = exceptionObject as Exception;

            if (ex != null)
            {
                Logger.Error(LOG_CTX_DOMAIN_UNHANDLED, ex);
            }
            else
            {
                Logger.ErrorFormat(
                    "{0}: ExceptionObject is not Exception. Type={1}",
                    LOG_CTX_DOMAIN_UNHANDLED,
                    exceptionObject != null ? exceptionObject.GetType().FullName : "null");
            }

            ShowGenericErrorSafe();
        }

        private static void ShowGenericErrorSafe()
        {
            try
            {
                Dispatcher dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;
                if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    return;
                }

                if (dispatcher.CheckAccess())
                {
                    ShowGenericError();
                    return;
                }

                dispatcher.BeginInvoke((Action)ShowGenericError, DispatcherPriority.Send);
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_CTX_SHOW_GENERIC_ERROR, ex);
            }
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
