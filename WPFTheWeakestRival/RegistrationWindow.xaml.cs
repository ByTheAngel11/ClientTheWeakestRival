using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class RegistrationWindow : Window
    {
        private string selectedImagePath;
        private string copiedImagePath;

        public RegistrationWindow()
        {
            InitializeComponent();

            cmbLanguage.Items.Add(Lang.es);
            cmbLanguage.Items.Add(Lang.en);
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

        private void ChooseImageClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = Lang.btnChooseImage,
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Multiselect = false,
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true)
            {
                selectedImagePath = dlg.FileName;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(selectedImagePath);
                bmp.EndInit();
                imgPreview.Source = bmp;
            }
        }


        private static string SaveProfileImageCopy(string originalPath)
        {
            if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
                return null;

            var destDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TheWeakestRival", "ProfileImages");

            Directory.CreateDirectory(destDir);

            var ext = Path.GetExtension(originalPath);
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var destPath = Path.Combine(destDir, fileName);

            File.Copy(originalPath, destPath, overwrite: false);
            return destPath;
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new MailAddress(email.Trim());
                return addr.Address == email.Trim();
            }
            catch { return false; }
        }

        private static bool PasswordMeetsRequirements(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (password.Length < 8) return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);

            return hasUpper && hasLower && hasDigit;
        }

        private static bool UsernameHasNoSpaces(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            return !username.Any(char.IsWhiteSpace);
        }

        private async void RegisterClick(object sender, RoutedEventArgs e)
        {
            btnRegister.IsEnabled = false;

            try
            {
                var displayName = txtUsername.Text?.Trim();
                var email = txtEmail.Text?.Trim();
                var password = pstPassword.Password ?? "";
                var confirm = pstConfirmPassword.Password ?? "";

                if (!UsernameHasNoSpaces(displayName))
                {
                    MessageBox.Show(Lang.errorUsernameWithoutSpaces, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!IsValidEmail(email))
                {
                    MessageBox.Show(Lang.errorInvalidEmail, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!PasswordMeetsRequirements(password))
                {
                    MessageBox.Show(Lang.errorPasswordStructure, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!string.Equals(password, confirm, StringComparison.Ordinal))
                {
                    MessageBox.Show(Lang.errorMatchingPasswords, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string profilePath = null;
                if (!string.IsNullOrWhiteSpace(selectedImagePath))
                {
                    profilePath = SaveProfileImageCopy(selectedImagePath);
                    copiedImagePath = profilePath;
                }

                var req = new RegisterRequest
                {
                    Email = email,
                    Password = password,
                    DisplayName = displayName,
                    ProfileImageUrl = profilePath
                };

                var client = new AuthServiceClient();

                RegisterResponse resp;

                try
                {
                    resp = await client.RegisterAsync(req);
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
                    $"{Lang.registSucces}\nUserId: {resp.UserId}\nExp: {token.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC",
                    "Auth", MessageBoxButton.OK, MessageBoxImage.Information);

                new LoginWindow().Show();
                this.Close();
            }
            finally
            {
                btnRegister.IsEnabled = true;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            this.Close();
        }

    }
}