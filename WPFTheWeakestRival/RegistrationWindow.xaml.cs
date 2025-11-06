using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Globalization;
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

            var current = LocalizationManager.Current.Culture.TwoLetterISOLanguageName;
            switch (current)
            {
                case "es":
                    cmbLanguage.SelectedIndex = 0;
                    break;
                case "en":
                    cmbLanguage.SelectedIndex = 1;
                    break;
                case "pt":
                    cmbLanguage.SelectedIndex = 2;
                    break;
                case "it":
                    cmbLanguage.SelectedIndex = 3;
                    break;
                case "fr":
                    cmbLanguage.SelectedIndex = 4;
                    break;
                default:
                    cmbLanguage.SelectedIndex = 0;
                    break;
            }
        }

        private void CmbLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLanguage.SelectedItem is ComboBoxItem item)
            {
                var code = (item.Tag as string) ?? "es";

                LocalizationManager.Current.SetCulture(code);
            }
        }

        private void ChooseImageClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = Lang.btnChooseImage,
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                selectedImagePath = dialog.FileName;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(selectedImagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                imgPreview.Source = bitmap;
                txtImgName.Text = Path.GetFileName(selectedImagePath);
            }
        }

        private static string SaveProfileImageCopy(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return null;
            }

            var destinationDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TheWeakestRival",
                "ProfileImages");

            Directory.CreateDirectory(destinationDirectory);

            var extension = Path.GetExtension(sourcePath);
            var fileName = Guid.NewGuid().ToString("N") + extension;
            var destinationPath = Path.Combine(destinationDirectory, fileName);

            File.Copy(sourcePath, destinationPath, overwrite: false);
            return destinationPath;
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                var addr = new MailAddress(email.Trim());
                return string.Equals(addr.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool PasswordMeetsRequirements(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            if (password.Length < 8)
            {
                return false;
            }

            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);

            return hasUpper && hasLower && hasDigit;
        }

        private static bool UsernameHasNoSpaces(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            return !username.Any(char.IsWhiteSpace);
        }

        private async void RegisterClick(object sender, RoutedEventArgs e)
        {
            btnRegister.IsEnabled = false;

            try
            {
                var displayName = txtUsername.Text?.Trim();
                var email = txtEmail.Text?.Trim();
                var password = pstPassword.Password ?? string.Empty;
                var confirmPassword = pstConfirmPassword.Password ?? string.Empty;

                if (!UsernameHasNoSpaces(displayName))
                {
                    MessageBox.Show(
                        Lang.errorUsernameWithoutSpaces,
                        Lang.registerTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!IsValidEmail(email))
                {
                    MessageBox.Show(
                        Lang.errorInvalidEmail,
                        Lang.registerTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!PasswordMeetsRequirements(password))
                {
                    MessageBox.Show(
                        Lang.errorPasswordStructure,
                        Lang.registerTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        Lang.errorMatchingPasswords,
                        Lang.registerTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string profilePath = null;
                if (!string.IsNullOrWhiteSpace(selectedImagePath))
                {
                    profilePath = SaveProfileImageCopy(selectedImagePath);
                    copiedImagePath = profilePath;
                }

                var authClient = new AuthServiceClient();
                try
                {
                    var beginResponse = await authClient.BeginRegisterAsync(new BeginRegisterRequest
                    {
                        Email = email
                    });

                    if (authClient.State != CommunicationState.Faulted)
                    {
                        authClient.Close();
                    }
                    else
                    {
                        authClient.Abort();
                    }

                    var verificationWindow = new EmailVerificationWindow(
                        email: email,
                        displayName: displayName,
                        password: password,
                        profileImagePath: profilePath,
                        resendCooldownSeconds: beginResponse.ResendAfterSeconds)
                    {
                        Owner = this
                    };

                    verificationWindow.Show();
                    Hide();
                }
                catch (FaultException<ServiceFault> fx)
                {
                    authClient.Abort();
                    MessageBox.Show(
                        $"{fx.Detail.Code}: {fx.Detail.Message}",
                        "Auth",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    authClient.Abort();
                    MessageBox.Show(
                        "Error de red/servicio: " + ex.Message,
                        "Auth",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            finally
            {
                btnRegister.IsEnabled = true;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            Close();
        }
    }
}
