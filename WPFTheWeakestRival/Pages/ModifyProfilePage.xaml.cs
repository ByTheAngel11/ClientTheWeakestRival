using log4net;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure.Faults;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class ModifyProfilePage : Page
    {
        private const string CTX_LOAD_PROFILE = "ModifyProfilePage.LoadProfile";
        private const string CTX_UPDATE_PROFILE = "ModifyProfilePage.BtnSaveClick";
        private const string CTX_LOGOUT = "ModifyProfilePage.LogoutClick";

        private const int PREVIEW_DECODE_WIDTH = 0;

        private const int MAX_PROFILE_IMAGE_BYTES = 524288;
        private const int JPEG_QUALITY_PRIMARY = 85;
        private const int JPEG_QUALITY_SECONDARY = 75;

        private const int RESIZE_W_PRIMARY = 512;
        private const int RESIZE_W_SECONDARY = 384;
        private const int RESIZE_W_TERTIARY = 256;

        private const string CONTENT_TYPE_PNG = "image/png";
        private const string CONTENT_TYPE_JPEG = "image/jpeg";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(ModifyProfilePage));

        private readonly LobbyServiceClient lobbyClient;
        private readonly AuthServiceClient authClient;
        private readonly string authToken;

        private string originalEmail = string.Empty;
        private string originalDisplayName = string.Empty;

        private byte[] selectedProfileImageBytes;
        private string selectedProfileImageContentType;
        private bool removeProfileImage;

        public event EventHandler Closed;
        public event EventHandler LoggedOut;

        public ModifyProfilePage(LobbyServiceClient lobbyClient, AuthServiceClient authClient, string authToken)
        {
            InitializeComponent();

            this.lobbyClient = lobbyClient ?? throw new ArgumentNullException(nameof(lobbyClient));
            this.authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
            this.authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));

            selectedProfileImageBytes = null;
            selectedProfileImageContentType = null;
            removeProfileImage = false;

            LoadProfile();
        }

        private void LoadProfile()
        {
            try
            {
                var profile = lobbyClient.GetMyProfile(authToken);

                originalEmail = (profile?.Email ?? string.Empty).Trim();
                originalDisplayName = (profile?.DisplayName ?? string.Empty).Trim();

                txtEmail.Text = originalEmail;
                txtDisplayName.Text = originalDisplayName;

                if (profile != null && profile.ProfileImageBytes != null && profile.ProfileImageBytes.Length > 0)
                {
                    imgPreview.Source = UiImageHelper.TryCreateFromBytes(profile.ProfileImageBytes, PREVIEW_DECODE_WIDTH);
                }
                else
                {
                    imgPreview.Source = null;
                }
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                string faultCode = ex.Detail == null ? string.Empty : (ex.Detail.Code ?? string.Empty);
                string faultMessage = ex.Detail == null ? (ex.Message ?? string.Empty) : (ex.Detail.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        faultCode,
                        faultMessage,
                        CTX_LOAD_PROFILE,
                        Logger,
                        this))
                {
                    return;
                }

                Logger.Warn("Lobby fault while loading profile.", ex);
                MessageBox.Show(Lang.profileLoadFailed, Lang.profileTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while loading profile.", ex);
                MessageBox.Show(Lang.noConnection, Lang.profileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("Timeout while loading profile.", ex);
                MessageBox.Show(Lang.noConnection, Lang.profileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while loading profile.", ex);
                MessageBox.Show(Lang.profileLoadFailed, Lang.profileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void BtnBrowseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = Lang.profileSelectAvatarTitle,
                    Filter = Lang.profileSelectAvatarFilter,
                    Multiselect = false
                };

                bool? result = dialog.ShowDialog();
                if (result != true)
                {
                    return;
                }

                if (!TryLoadProfileImage(dialog.FileName, out byte[] bytes, out string contentType))
                {
                    MessageBox.Show(Lang.profileInvalidImage, Lang.modifyProfileTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                selectedProfileImageBytes = bytes;
                selectedProfileImageContentType = contentType;
                removeProfileImage = false;

                imgPreview.Source = UiImageHelper.TryCreateFromBytes(selectedProfileImageBytes, PREVIEW_DECODE_WIDTH);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while browsing profile image.", ex);
                MessageBox.Show(Lang.profileLoadFailed, Lang.modifyProfileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearImageClick(object sender, RoutedEventArgs e)
        {
            removeProfileImage = true;
            selectedProfileImageBytes = null;
            selectedProfileImageContentType = null;
            imgPreview.Source = null;
        }

        private void BtnSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = (txtEmail.Text ?? string.Empty).Trim();
                string displayName = (txtDisplayName.Text ?? string.Empty).Trim();

                bool hasEmailChange = email.Length > 0 && !string.Equals(email, originalEmail, StringComparison.Ordinal);
                bool hasDisplayNameChange = displayName.Length > 0 && !string.Equals(displayName, originalDisplayName, StringComparison.Ordinal);

                var request = new UpdateAccountRequest
                {
                    Token = authToken
                };

                if (hasEmailChange)
                {
                    request.Email = email;
                }

                if (hasDisplayNameChange)
                {
                    request.DisplayName = displayName;
                }

                if (removeProfileImage)
                {
                    request.RemoveProfileImage = true;
                }
                else if (selectedProfileImageBytes != null && selectedProfileImageBytes.Length > 0)
                {
                    request.ProfileImageBytes = selectedProfileImageBytes;
                    request.ProfileImageContentType = selectedProfileImageContentType;
                }

                lobbyClient.UpdateAccount(request);

                MessageBox.Show(
                    Lang.profileUpdated,
                    Lang.commonSucces,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Closed?.Invoke(this, EventArgs.Empty);
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                string faultCode = ex.Detail == null ? string.Empty : (ex.Detail.Code ?? string.Empty);
                string faultMessage = ex.Detail == null ? (ex.Message ?? string.Empty) : (ex.Detail.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        faultCode,
                        faultMessage,
                        CTX_UPDATE_PROFILE,
                        Logger,
                        this))
                {
                    return;
                }

                Logger.Warn("Lobby fault while updating profile.", ex);

                string serverText = ex.Detail != null
                    ? string.Format(CultureInfo.CurrentCulture, "{0}: {1}", ex.Detail.Code ?? string.Empty, ex.Detail.Message ?? string.Empty)
                    : Lang.profileUpdateFailed;

                MessageBox.Show(
                    serverText,
                    Lang.modifyProfileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while updating profile.", ex);
                MessageBox.Show(Lang.noConnection, Lang.modifyProfileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("Timeout while updating profile.", ex);
                MessageBox.Show(Lang.noConnection, Lang.modifyProfileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while updating profile.", ex);
                MessageBox.Show(Lang.profileUpdateFailed, Lang.modifyProfileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogoutClick(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                Lang.logoutConfirmMessage,
                Lang.logoutTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                authClient.Logout(new LogoutRequest { Token = authToken });
            }
            catch (FaultException<AuthService.ServiceFault> ex)
            {
                string faultCode = ex.Detail == null ? string.Empty : (ex.Detail.Code ?? string.Empty);
                string faultMessage = ex.Detail == null ? (ex.Message ?? string.Empty) : (ex.Detail.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        faultCode,
                        faultMessage,
                        CTX_LOGOUT,
                        Logger,
                        this))
                {
                    return;
                }

                Logger.Warn("Auth fault while logging out.", ex);

                string serverText = ex.Detail != null
                    ? string.Format(CultureInfo.CurrentCulture, "{0}: {1}", ex.Detail.Code ?? string.Empty, ex.Detail.Message ?? string.Empty)
                    : Lang.logoutFailed;

                MessageBox.Show(
                    serverText,
                    Lang.logoutTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while logging out.", ex);
                MessageBox.Show(Lang.noConnection, Lang.logoutTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("Timeout while logging out.", ex);
                MessageBox.Show(Lang.noConnection, Lang.logoutTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while logging out.", ex);
                MessageBox.Show(Lang.logoutFailed, Lang.logoutTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try
                {
                    LoginWindow.AppSession.CurrentToken = null;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error clearing current session token during logout.", ex);
                }

                LoggedOut?.Invoke(this, EventArgs.Empty);
            }
        }

        private static bool TryLoadProfileImage(string filePath, out byte[] bytes, out string contentType)
        {
            bytes = null;
            contentType = null;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            string ext = Path.GetExtension(filePath) ?? string.Empty;
            bool isPng = ext.Equals(".png", StringComparison.OrdinalIgnoreCase);
            bool isJpg = ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

            if (!isPng && !isJpg)
            {
                return false;
            }

            byte[] originalBytes = File.ReadAllBytes(filePath);
            if (originalBytes.Length <= MAX_PROFILE_IMAGE_BYTES)
            {
                bytes = originalBytes;
                contentType = isPng ? CONTENT_TYPE_PNG : CONTENT_TYPE_JPEG;
                return true;
            }

            if (TryDownscaleToJpegUnderLimit(filePath, RESIZE_W_PRIMARY, JPEG_QUALITY_PRIMARY, out bytes))
            {
                contentType = CONTENT_TYPE_JPEG;
                return true;
            }

            if (TryDownscaleToJpegUnderLimit(filePath, RESIZE_W_SECONDARY, JPEG_QUALITY_PRIMARY, out bytes))
            {
                contentType = CONTENT_TYPE_JPEG;
                return true;
            }

            if (TryDownscaleToJpegUnderLimit(filePath, RESIZE_W_TERTIARY, JPEG_QUALITY_SECONDARY, out bytes))
            {
                contentType = CONTENT_TYPE_JPEG;
                return true;
            }

            return false;
        }

        private static bool TryDownscaleToJpegUnderLimit(string filePath, int decodeWidth, int quality, out byte[] jpegBytes)
        {
            jpegBytes = null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.DecodePixelWidth = decodeWidth;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                var encoder = new JpegBitmapEncoder
                {
                    QualityLevel = quality
                };

                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    byte[] result = ms.ToArray();

                    if (result.Length <= MAX_PROFILE_IMAGE_BYTES && result.Length > 0)
                    {
                        jpegBytes = result;
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
