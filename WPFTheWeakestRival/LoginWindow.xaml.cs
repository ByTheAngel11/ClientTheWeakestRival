using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
            txtPassword.Text = Properties.Langs.Lang.txtPassword;
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
    }
}
