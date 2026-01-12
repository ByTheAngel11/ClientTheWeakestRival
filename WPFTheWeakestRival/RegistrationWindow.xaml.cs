using log4net;
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
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure.Faults;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class RegistrationWindow : Window
    {
        private const int ONE_KILOBYTE_BYTES = 1024;
        private const int PROFILE_IMAGE_MAX_KB = 512;
        private const int PROFILE_IMAGE_MAX_BYTES = PROFILE_IMAGE_MAX_KB * ONE_KILOBYTE_BYTES;

        private const string CONTENT_TYPE_PNG = "image/png";
        private const string CONTENT_TYPE_JPEG = "image/jpeg";
        private const string REGISTER_ERROR = "registerError";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(RegistrationWindow));

        private byte[] selectedProfileImageBytes;
        private string selectedProfileImageContentType;

        public RegistrationWindow()
        {
            InitializeComponent();

            UiValidationHelper.ApplyMaxLength(txtUsername, UiValidationHelper.DISPLAY_NAME_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(txtEmail, UiValidationHelper.EMAIL_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(pstPassword, UiValidationHelper.PASSWORD_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(pstConfirmPassword, UiValidationHelper.PASSWORD_MAX_LENGTH);

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
                Filter = "Images|*.png;*.jpg;*.jpeg",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                string filePath = dialog.FileName;

                byte[] bytes = File.ReadAllBytes(filePath);
                string contentType = DetectContentTypeOrEmpty(filePath, bytes);

                ValidateProfileImageOrThrow(bytes, contentType);

                selectedProfileImageBytes = bytes;
                selectedProfileImageContentType = contentType;

                imgPreview.Source = CreateBitmapFromBytes(bytes);
                txtImgName.Text = Path.GetFileName(filePath);
            }
            catch (Exception ex)
            {
                Logger.Warn("RegistrationWindow.ChooseImageClick: invalid image.", ex);

                selectedProfileImageBytes = null;
                selectedProfileImageContentType = null;
                imgPreview.Source = null;
                txtImgName.Text = string.Empty;

                MessageBox.Show(
                    "Only PNG/JPG images up to 512 KB are allowed.",
                    Lang.registerTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static BitmapImage CreateBitmapFromBytes(byte[] bytes)
        {
            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
            }

            return bitmap;
        }

        private static void ValidateProfileImageOrThrow(byte[] bytes, string contentType)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            if (bytes.Length > PROFILE_IMAGE_MAX_BYTES)
            {
                throw new InvalidOperationException("Image too large.");
            }

            bool isAllowed =
                string.Equals(contentType, CONTENT_TYPE_PNG, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contentType, CONTENT_TYPE_JPEG, StringComparison.OrdinalIgnoreCase);

            if (!isAllowed)
            {
                throw new InvalidOperationException("Invalid content type.");
            }

            if (!MatchesSignature(bytes, contentType))
            {
                throw new InvalidOperationException("Signature mismatch.");
            }
        }

        private static bool MatchesSignature(byte[] bytes, string contentType)
        {
            if (bytes == null || bytes.Length < 8)
            {
                return false;
            }

            if (string.Equals(contentType, CONTENT_TYPE_PNG, StringComparison.OrdinalIgnoreCase))
            {
                return bytes[0] == 0x89 &&
                       bytes[1] == 0x50 &&
                       bytes[2] == 0x4E &&
                       bytes[3] == 0x47 &&
                       bytes[4] == 0x0D &&
                       bytes[5] == 0x0A &&
                       bytes[6] == 0x1A &&
                       bytes[7] == 0x0A;
            }

            if (string.Equals(contentType, CONTENT_TYPE_JPEG, StringComparison.OrdinalIgnoreCase))
            {
                return bytes[0] == 0xFF && bytes[1] == 0xD8;
            }

            return false;
        }

        private static string DetectContentTypeOrEmpty(string filePath, byte[] bytes)
        {
            string ext = (Path.GetExtension(filePath) ?? string.Empty).ToLowerInvariant();
            if (ext == ".png")
            {
                return CONTENT_TYPE_PNG;
            }

            if (ext == ".jpg" || ext == ".jpeg")
            {
                return CONTENT_TYPE_JPEG;
            }

            if (MatchesSignature(bytes, CONTENT_TYPE_PNG))
            {
                return CONTENT_TYPE_PNG;
            }

            if (MatchesSignature(bytes, CONTENT_TYPE_JPEG))
            {
                return CONTENT_TYPE_JPEG;
            }

            return string.Empty;
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

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);

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
                string displayName = UiValidationHelper.TrimOrEmpty(txtUsername.Text);
                string email = UiValidationHelper.TrimOrEmpty(txtEmail.Text);
                string password = pstPassword.Password ?? string.Empty;
                string confirmPassword = pstConfirmPassword.Password ?? string.Empty;

                if (displayName.Length == 0)
                {
                    MessageBox.Show("El nombre no puede estar vacío.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

                if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
                {
                    MessageBox.Show(Lang.errorMatchingPasswords, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var authClient = new AuthServiceClient();

                try
                {
                    var beginResponse = await authClient.BeginRegisterAsync(new BeginRegisterRequest { Email = email });

                    SafeClose(authClient);

                    var verificationWindow = new EmailVerificationWindow(
                        email: email,
                        displayName: displayName,
                        password: password,
                        profileImageBytes: selectedProfileImageBytes,
                        profileImageContentType: selectedProfileImageContentType,
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
                    ResolveFaultMessage(fx.Detail),
                    Localize(REGISTER_ERROR),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                }
                catch (EndpointNotFoundException ex)
                {
                    authClient.Abort();
                    Logger.Error("RegistrationWindow.RegisterClick: endpoint not found.", ex);

                    MessageBox.Show("Service is unavailable. Please try again later.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (CommunicationException ex)
                {
                    authClient.Abort();
                    Logger.Error("RegistrationWindow.RegisterClick: communication error.", ex);

                    MessageBox.Show("Network error. Please try again later.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (TimeoutException ex)
                {
                    authClient.Abort();
                    Logger.Error("RegistrationWindow.RegisterClick: timeout.", ex);

                    MessageBox.Show("Request timed out. Please try again.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    authClient.Abort();
                    Logger.Error("RegistrationWindow.RegisterClick: unexpected error.", ex);

                    MessageBox.Show("Unexpected error. Please try again later.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                btnRegister.IsEnabled = true;
            }
        }

        private static void SafeClose(ICommunicationObject obj)
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                if (obj.State != CommunicationState.Faulted)
                {
                    obj.Close();
                }
                else
                {
                    obj.Abort();
                }
            }
            catch
            {
                obj.Abort();
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            Close();
        }

        private static string Localize(string key)
        {
            var safeKey = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(safeKey))
            {
                return string.Empty;
            }

            var value = Lang.ResourceManager.GetString(safeKey, Lang.Culture);
            return string.IsNullOrWhiteSpace(value) ? safeKey : value;
        }

        private static string ResolveFaultMessage(AuthService.ServiceFault fault)
        {
            var key = fault == null ? string.Empty : (fault.Message ?? string.Empty);
            return FaultKeyMessageResolver.Resolve(key, Localize);
        }
    }
}
