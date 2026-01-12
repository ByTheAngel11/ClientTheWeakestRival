using log4net;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Globalization;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.Properties.Langs;
using WPFTheWeakestRival.Helpers;

namespace WPFTheWeakestRival
{
    public partial class LoginWindow : Window
    {
        private const string LANGUAGE_CODE_ES = "es";
        private const string DEFAULT_LANGUAGE_CODE = LANGUAGE_CODE_ES;

        private const int LANGUAGE_INDEX_ES = 0;
        private const int LANGUAGE_INDEX_EN = 1;

        private const int PASSWORD_TOGGLE_ICON_FONT_SIZE = 14;
        private const string PASSWORD_ICON_SHOW = "👁";
        private const string PASSWORD_ICON_HIDE = "🙈";

        private const int FORGOT_PASSWORD_WINDOW_TIMEOUT_SECONDS = 60;

        private const string CTX_LOGIN = "Login";
        private const string CTX_GUEST_LOGIN = "GuestLogin";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LoginWindow));

        private bool isPasswordVisible;

        public LoginWindow()
        {
            InitializeComponent();

            UiValidationHelper.ApplyMaxLength(txtEmail, UiValidationHelper.EMAIL_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(pwdPassword, UiValidationHelper.PASSWORD_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(txtPasswordVisible, UiValidationHelper.PASSWORD_MAX_LENGTH);

            string current = LocalizationManager.Current.Culture.TwoLetterISOLanguageName;

            cmblanguage.SelectedIndex = string.Equals(current, LANGUAGE_CODE_ES, StringComparison.OrdinalIgnoreCase)
                ? LANGUAGE_INDEX_ES
                : LANGUAGE_INDEX_EN;

            UpdateMainImage();
        }

        private void CmbLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmblanguage.SelectedItem is ComboBoxItem item)
            {
                string code = (item.Tag as string) ?? DEFAULT_LANGUAGE_CODE;
                LocalizationManager.Current.SetCulture(code);
                UpdateMainImage();
            }
        }

        private void UpdateMainImage()
        {
            try
            {
                string path = Lang.imageLogo;

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
            SetPasswordVisibility(!isPasswordVisible);
        }

        private void SetPasswordVisibility(bool visible)
        {
            isPasswordVisible = visible;

            if (visible)
            {
                txtPasswordVisible.Text = pwdPassword.Password;
                txtPasswordVisible.Visibility = Visibility.Visible;
                pwdPassword.Visibility = Visibility.Collapsed;

                btnTogglePassword.Content = new TextBlock
                {
                    Text = PASSWORD_ICON_HIDE,
                    FontSize = PASSWORD_TOGGLE_ICON_FONT_SIZE
                };
            }
            else
            {
                pwdPassword.Password = txtPasswordVisible.Text;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                pwdPassword.Visibility = Visibility.Visible;

                btnTogglePassword.Content = new TextBlock
                {
                    Text = PASSWORD_ICON_SHOW,
                    FontSize = PASSWORD_TOGGLE_ICON_FONT_SIZE
                };
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
                string email = UiValidationHelper.TrimOrEmpty(txtEmail.Text);
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

                var request = new LoginRequest
                {
                    Email = email,
                    Password = password
                };

                LoginResponse response = await InvokeAuthAsync(
                    client => client.LoginAsync(request),
                    CTX_LOGIN);

                if (response == null)
                {
                    return;
                }

                if (!TryStartSessionAndNavigate(response))
                {
                    return;
                }
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        private async void BtnGuestLoginClick(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;

            try
            {
                var request = new GuestLoginRequest
                {
                    DisplayName = string.Empty
                };

                LoginResponse response = await InvokeAuthAsync(
                    client => client.GuestLoginAsync(request),
                    CTX_GUEST_LOGIN);

                if (response == null)
                {
                    return;
                }

                if (!TryStartSessionAndNavigate(response))
                {
                    return;
                }
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        private static async Task<LoginResponse> InvokeAuthAsync(
            Func<AuthServiceClient, Task<LoginResponse>> actionAsync,
            string context)
        {
            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            var client = new AuthServiceClient();

            try
            {
                LoginResponse response = await actionAsync(client);

                CloseClientSafe(client);

                return response;
            }
            catch (FaultException<ServiceFault> ex)
            {
                AbortClientSafe(client);

                Logger.WarnFormat(
                    "{0} fault. Code={1}, Message={2}",
                    context,
                    ex.Detail?.Code ?? string.Empty,
                    ex.Detail?.Message ?? ex.Message);

                MessageBox.Show(
                    ex.Detail != null
                        ? (ex.Detail.Code + ": " + ex.Detail.Message)
                        : Lang.noConnection,
                    Lang.loginTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return null;
            }
            catch (Exception ex)
            {
                AbortClientSafe(client);

                Logger.Error(context + " unexpected error.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.loginTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return null;
            }
        }

        private static void CloseClientSafe(ICommunicationObject client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    client.Abort();
                }
                else
                {
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("CloseClientSafe failed.", ex);

                try
                {
                    client.Abort();
                }
                catch (Exception abortEx)
                {
                    Logger.Warn("CloseClientSafe abort failed.", abortEx);
                }
            }
        }

        private static void AbortClientSafe(ICommunicationObject client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                client.Abort();
            }
            catch (Exception ex)
            {
                Logger.Warn("AbortClientSafe failed.", ex);
            }
        }

        private bool TryStartSessionAndNavigate(LoginResponse response)
        {
            AuthToken token = response?.Token;

            if (token == null || string.IsNullOrWhiteSpace(token.Token))
            {
                MessageBox.Show(Lang.noConnection, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
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

            return true;
        }

        public static class AppSession
        {
            public static AuthToken CurrentToken { get; set; }
        }

        private void BtnForgotPassword(object sender, RoutedEventArgs e)
        {
            var forgotWindow = new ForgotPasswordWindow(txtEmail.Text, FORGOT_PASSWORD_WINDOW_TIMEOUT_SECONDS);
            forgotWindow.Owner = this;
            forgotWindow.Show();
            Hide();
        }
    }
}
