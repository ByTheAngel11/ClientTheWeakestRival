using log4net;
using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Globalization;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class LoginWindow : Window
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LoginWindow));

        private bool isPasswordVisible;

        public LoginWindow()
        {
            InitializeComponent();

            var current = LocalizationManager.Current.Culture.TwoLetterISOLanguageName;
            cmblanguage.SelectedIndex = (current == "es") ? 0 : 1;

            UpdateMainImage();
        }

        private void CmbLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmblanguage.SelectedItem is ComboBoxItem item)
            {
                var code = (item.Tag as string) ?? "es";
                LocalizationManager.Current.SetCulture(code);
                UpdateMainImage();
            }
        }

        private void UpdateMainImage()
        {
            try
            {
                var path = Lang.imageLogo;
                if (string.IsNullOrWhiteSpace(path))
                {
                    logoImage.Source = null;
                    return;
                }

                logoImage.Source = new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
            }
            catch (Exception ex)
            {
                Logger.Warn("UpdateMainImage failed.", ex);
                logoImage.Source = null;
            }
        }

        private void RegistrationClick(object sender, RoutedEventArgs e)
        {
            var registration = new RegistrationWindow();
            registration.Show();
            Close();
        }

        private void BtnTogglePasswordClick(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;

            if (isPasswordVisible)
            {
                txtPasswordVisible.Text = pwdPassword.Password;
                txtPasswordVisible.Visibility = Visibility.Visible;
                pwdPassword.Visibility = Visibility.Collapsed;
                btnTogglePassword.Content = new TextBlock { Text = "🙈", FontSize = 14 };
            }
            else
            {
                pwdPassword.Password = txtPasswordVisible.Text;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                pwdPassword.Visibility = Visibility.Visible;
                btnTogglePassword.Content = new TextBlock { Text = "👁", FontSize = 14 };
            }
        }

        private void PwdPasswordChanged(object sender, RoutedEventArgs e)
        {
            placeholderPassword.Visibility = string.IsNullOrEmpty(pwdPassword.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void PwdPasswordGotFocus(object sender, RoutedEventArgs e)
        {
            placeholderPassword.Visibility = Visibility.Collapsed;
        }

        private void PwdPasswordLostFocus(object sender, RoutedEventArgs e)
        {
            placeholderPassword.Visibility = string.IsNullOrEmpty(pwdPassword.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void TxtPasswordVisibleTextChanged(object sender, TextChangedEventArgs e)
        {
            placeholderPassword.Visibility = string.IsNullOrEmpty(txtPasswordVisible.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void BtnLoginClick(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;

            try
            {
                string email = txtEmail.Text?.Trim();
                string password = pwdPassword.Password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(email))
                {
                    MessageBox.Show(Lang.errorInvalidEmail, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show(Lang.errorMisingFields, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var request = new LoginRequest { Email = email, Password = password };

                LoginResponse response;
                var client = new AuthServiceClient();

                try
                {
                    response = await client.LoginAsync(request);

                    if (client.State != CommunicationState.Faulted)
                    {
                        client.Close();
                    }
                    else
                    {
                        client.Abort();
                    }
                }
                catch (FaultException<ServiceFault> fx)
                {
                    client.Abort();
                    Logger.WarnFormat(
                        "Login fault. Code={0}, Message={1}",
                        fx.Detail?.Code ?? string.Empty,
                        fx.Detail?.Message ?? fx.Message);

                    MessageBox.Show(
                        fx.Detail != null ? (fx.Detail.Code + ": " + fx.Detail.Message) : Lang.noConnection,
                        Lang.loginTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }
                catch (Exception ex)
                {
                    client.Abort();
                    Logger.Error("Login unexpected error.", ex);

                    MessageBox.Show(Lang.noConnection, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var token = response?.Token;
                if (token == null || string.IsNullOrWhiteSpace(token.Token))
                {
                    MessageBox.Show(Lang.noConnection, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AppSession.CurrentToken = token;

                AppServices.ResetAll();

                _ = AppServices.Lobby;

                var main = new MainMenuWindow();

                var app = Application.Current;
                if (app != null)
                {
                    app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    app.MainWindow = main;
                }

                main.Show();
                Close();
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        public static class AppSession
        {
            public static AuthToken CurrentToken { get; set; }
        }

        private void BtnForgotPassword(object sender, RoutedEventArgs e)
        {
            var forgotWindow = new ForgotPasswordWindow(txtEmail.Text, 60);
            forgotWindow.Owner = this;
            forgotWindow.Show();
            Hide();
        }
    }
}
