using System;
using System.Globalization;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Globalization; // <<— para LocalizationManager
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class LoginWindow : Window
    {
        private bool isPasswordVisible = false;

        public LoginWindow()
        {
            InitializeComponent();

            // Ajusta selección inicial del combo según la cultura actual
            var current = LocalizationManager.Current.Culture.TwoLetterISOLanguageName;
            cmblanguage.SelectedIndex = (current == "es") ? 0 : 1;

            UpdateMainImage(); // carga logo según idioma actual
        }

        private void CmbLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmblanguage.SelectedItem is ComboBoxItem item)
            {
                var code = (item.Tag as string) ?? "es";
                LocalizationManager.Current.SetCulture(code); // refresca TODOS los {loc:Loc ...}
                UpdateMainImage(); // re-resuelve Lang.imageLogo con nueva cultura
            }
        }

        private void UpdateMainImage()
        {
            try
            {
                var path = Lang.imageLogo; // depende de Lang.Culture (ya sincronizada por LocalizationManager)
                if (string.IsNullOrWhiteSpace(path))
                {
                    logoImage.Source = null;
                    return;
                }
                logoImage.Source = new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
            }
            catch
            {
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
                var email = txtEmail.Text?.Trim();
                var password = pwdPassword.Password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(email))
                {
                    MessageBox.Show(Lang.errorInvalidEmail, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show(Lang.errorMisingFields, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var request = new LoginRequest { Email = email, Password = password };

                var client = new AuthServiceClient();
                LoginResponse response;

                try
                {
                    response = await client.LoginAsync(request);
                    if (client.State != CommunicationState.Faulted) client.Close(); else client.Abort();
                }
                catch (FaultException<ServiceFault> fx)
                {
                    client.Abort();
                    MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Auth", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                catch (Exception ex)
                {
                    client.Abort();
                    MessageBox.Show("Error de red/servicio: " + ex.Message, "Auth", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var token = response.Token;

                MessageBox.Show(
                    $"{Lang.succesLoginMessage}\nUserId: {token.UserId}\nToken: {token.Token}\nExp: {token.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC",
                    "Auth", MessageBoxButton.OK, MessageBoxImage.Information);

                AppSession.CurrentToken = token;

                var main = new MainMenuWindow();
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
    }
}
