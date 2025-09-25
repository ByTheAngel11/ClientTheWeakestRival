using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

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

            cmblanguage.Items.Add("Español");
            cmblanguage.Items.Add("English");
            cmblanguage.SelectedIndex = 0; 

            cmblanguage.SelectionChanged += Cmblanguage_SelectionChanged;
        }

        private void Cmblanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedLang = cmblanguage.SelectedItem as string;
            if (selectedLang == "Español")
                Properties.Langs.Lang.Culture = new CultureInfo("es");
            else
                Properties.Langs.Lang.Culture = new CultureInfo("en");

            UpdateUILanguage();
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
        }
    }
}
