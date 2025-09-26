using Microsoft.Win32;
using WPFTheWeakestRival.Properties.Langs;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace WPFTheWeakestRival
{
    public partial class RegistrationWindow : Window
    {
        
        public RegistrationWindow()
        {
            InitializeComponent();

            cmbLanguage.Items.Add("Español");
            cmbLanguage.Items.Add("English");
            cmbLanguage.SelectedIndex = 0;

            cmbLanguage.SelectionChanged += CmbLanguage_SelectionChanged;
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedLang = cmbLanguage.SelectedItem as string;
            if (selectedLang == "Español")
                Properties.Langs.Lang.Culture = new CultureInfo("es");
            else
                Properties.Langs.Lang.Culture = new CultureInfo("en");

            UpdateUILanguage();
        }

        private void UpdateUILanguage()
        {
            lblRegistration.Content = Properties.Langs.Lang.registerTitle;
            lblUsername.Content = Properties.Langs.Lang.registerDisplayName;
            lblEmail.Content = Properties.Langs.Lang.lblEmail;
            lblPassword.Content = Properties.Langs.Lang.lblPassword;
            lblConfirmPassword.Content = Properties.Langs.Lang.lblConfirmPassword;
            btnChooseImage.Content = Properties.Langs.Lang.btnChooseImage;
            btnRegister.Content = Properties.Langs.Lang.regist;
            btnBack.Content = Properties.Langs.Lang.cancel;
        }

        private void UpdateLabelColors()
        {
            // Esqueleto: implementación eliminada para evitar lógica en este archivo
        }

        private void ChooseImageClick(object sender, RoutedEventArgs e)
        {
            // Esqueleto: implementación eliminada
        }

        private static string SaveProfileImageCopy(string originalPath)
        {
            // Esqueleto: implementación eliminada
            return null;
        }

        private static byte[] HashPassword(string password)
        {
            // Esqueleto: implementación eliminada
            return null;
        }

        private static bool IsValidEmail(string email)
        {
            // Esqueleto: implementación eliminada
            return false;
        }

        private static bool PasswordMeetsRequirements(string password)
        {
            // Esqueleto: implementación eliminada
            return false;
        }

        private static bool UsernameHasNoSpaces(string username)
        {
            // Esqueleto: implementación eliminada
            return false;
        }

        private void RegisterClick(object sender, RoutedEventArgs e)
        {
            // Esqueleto: implementación eliminada
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            // Esqueleto: implementación eliminada
        }

    }
}