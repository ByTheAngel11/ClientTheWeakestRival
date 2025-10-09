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
            txtEmail.Text = Properties.Langs.Lang.txtEmail;
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

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;

            try
            {
                var email = txtEmail.Text?.Trim();           // usa el mismo control que en registro
                var password = pwdPassword.Password ?? "";

                // Validaciones mínimas del lado cliente (lo fino puede quedar igual que en Register)
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

                // Éxito: tienes el token emitido por el servidor
                var token = resp.Token; // contiene UserId, Token (string), ExpiresAtUtc

                // Reemplaza Lang.loginSuccess por Lang.succesLoginMessage en la línea correspondiente
                MessageBox.Show(
                    $"{Lang.succesLoginMessage}\nUserId: {token.UserId}\nToken: {token.Token}\nExp: {token.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC",
                    "Auth", MessageBoxButton.OK, MessageBoxImage.Information);

                // TODO: guarda el token donde te convenga (ejemplo simple):
                AppSession.CurrentToken = token;

                // Navega a tu siguiente ventana (cámbiala por la que uses)
                var home = new LobbyWindow();
                home.Show();
                this.Close();
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        // Session sencilla para tener el token disponible en la app
        public static class AppSession
        {
            public static AuthToken CurrentToken { get; set; }
        }
    }
}
