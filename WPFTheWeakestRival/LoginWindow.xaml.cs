using System;
using System.Globalization;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    /// <summary>
    /// Lógica de interacción para LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            UpdateMainImage();

            cmblanguage.Items.Add(Lang.es);
            cmblanguage.Items.Add(Lang.en);
            cmblanguage.SelectedIndex = 0;

            cmblanguage.SelectionChanged += Cmblanguage_SelectionChanged;
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
            catch
            {
                logoImage.Source = null;
            }
        }

        private void Cmblanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedLang = cmblanguage.SelectedItem as string;
            if (selectedLang == Lang.es)
                Properties.Langs.Lang.Culture = new CultureInfo("es");
            else
                Properties.Langs.Lang.Culture = new CultureInfo("en");

            UpdateUILanguage();
            UpdateMainImage();
        }

        private void UpdateUILanguage()
        {
            lblWelcome.Content = Properties.Langs.Lang.lblWelcome;
            if (placeholderEmail != null)
                placeholderEmail.Text = Properties.Langs.Lang.emailPlaceholder;

            if (placeholderPassword != null)
                placeholderPassword.Text = Properties.Langs.Lang.passwordPlaceHolder;

            btnLogin.Content = Properties.Langs.Lang.btnLogin;
            btnForgotPassword.Content = Properties.Langs.Lang.forgotPassword;
            btnNotAccount.Content = Properties.Langs.Lang.notAccount;
            btnRegist.Content = Properties.Langs.Lang.regist;
            btnPlayAsGuest.Content = Properties.Langs.Lang.playAsGuest;

            string prevSelected = cmblanguage.SelectedItem as string;
            cmblanguage.SelectionChanged -= Cmblanguage_SelectionChanged;
            cmblanguage.Items.Clear();
            cmblanguage.Items.Add(Lang.es);
            cmblanguage.Items.Add(Lang.en);

            if (Properties.Langs.Lang.Culture.TwoLetterISOLanguageName == "es")
                cmblanguage.SelectedIndex = 0;
            else
                cmblanguage.SelectedIndex = 1;
            cmblanguage.SelectionChanged += Cmblanguage_SelectionChanged;
        }

        private void cmblanguage_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {

        }

        private void RegistrationClick(object sender, RoutedEventArgs e)
        {
            RegistrationWindow regWindow = new RegistrationWindow();
            regWindow.Show();
            this.Close();
        }

        private bool isPasswordVisible = false;

        private void btnTogglePassword_Click(object sender, RoutedEventArgs e)
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

        private void pwdPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            placeholderPassword.Visibility = string.IsNullOrEmpty(pwdPassword.Password)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void pwdPassword_GotFocus(object sender, RoutedEventArgs e)
        {
            placeholderPassword.Visibility = Visibility.Collapsed;
        }

        private void pwdPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            placeholderPassword.Visibility = string.IsNullOrEmpty(pwdPassword.Password)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            placeholderPassword.Visibility = string.IsNullOrEmpty(txtPasswordVisible.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;

            try
            {
                var email = txtEmail.Text?.Trim();
                var password = pwdPassword.Password ?? "";

                if (string.IsNullOrWhiteSpace(email))
                {
                    MessageBox.Show(Lang.errorInvalidEmail, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show(Lang.errorMisingFields, Lang.loginTitle, MessageBoxButton.OK, MessageBoxImage.Warning); return;
                }

                var req = new LoginRequest
                {
                    Email = email,
                    Password = password
                };

                var client = new AuthServiceClient();
                LoginResponse resp;

                try
                {
                    resp = await client.LoginAsync(req);

                    if (client.State != CommunicationState.Faulted) client.Close();
                    else client.Abort();
                }
                catch (FaultException<ServiceFault> fx)
                {
                    client.Abort();
                    MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Auth",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                catch (Exception ex)
                {
                    client.Abort();
                    MessageBox.Show("Error de red/servicio: " + ex.Message, "Auth",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var token = resp.Token;

                MessageBox.Show(
                    $"{Lang.succesLoginMessage}\nUserId: {token.UserId}\nToken: {token.Token}\nExp: {token.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC",
                    "Auth", MessageBoxButton.OK, MessageBoxImage.Information);

                AppSession.CurrentToken = token;

                var home = new LobbyWindow();
                home.Show();
                this.Close();
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