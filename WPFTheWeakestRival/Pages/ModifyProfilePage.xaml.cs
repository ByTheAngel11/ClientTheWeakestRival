using Microsoft.Win32;
using System;
using System.IO;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using log4net;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class ModifyProfilePage : Page
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ModifyProfilePage));

        private readonly LobbyServiceClient lobbyClient;
        private readonly AuthServiceClient authClient;
        private readonly string authToken;

        public LobbyServiceClient Lobby { get; }
        public string Token { get; }

        public event EventHandler Closed;
        public event EventHandler LoggedOut;

        public ModifyProfilePage(LobbyServiceClient lobbyClient, AuthServiceClient authClient, string authToken)
        {
            InitializeComponent();
            this.lobbyClient = lobbyClient ?? throw new ArgumentNullException(nameof(lobbyClient));
            this.authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
            this.authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
            LoadProfile();
        }

        public ModifyProfilePage(LobbyServiceClient lobby, string token)
        {
            Lobby = lobby;
            Token = token;
        }

        private void LoadProfile()
        {
            try
            {
                var profile = lobbyClient.GetMyProfile(authToken);
                txtEmail.Text = profile.Email ?? string.Empty;
                txtDisplayName.Text = profile.DisplayName ?? string.Empty;
                txtAvatarUrl.Text = profile.ProfileImageUrl ?? string.Empty;
                LoadPreviewFromURL(txtAvatarUrl.Text);
            }
            catch (FaultException<AuthService.ServiceFault> ex)
            {
                Logger.Warn("Auth fault while loading profile.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while loading profile.", ex);
                MessageBox.Show(
                    ex.Message,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while loading profile.", ex);
                MessageBox.Show(
                    ex.Message,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            var handler = Closed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void BtnSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var request = new UpdateAccountRequest
                {
                    Token = authToken,
                    Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text,
                    DisplayName = string.IsNullOrWhiteSpace(txtDisplayName.Text) ? null : txtDisplayName.Text,
                    ProfileImageUrl = string.IsNullOrWhiteSpace(txtAvatarUrl.Text) ? null : txtAvatarUrl.Text
                };

                lobbyClient.UpdateAccount(request);

                MessageBox.Show(
                    Lang.profileUpdated,
                    Lang.commonSucces,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                var handler = Closed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<AuthService.ServiceFault> ex)
            {
                Logger.Warn("Auth fault while updating profile.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    Lang.modifyProfileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while updating profile.", ex);
                MessageBox.Show(
                    ex.Message,
                    Lang.modifyProfileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while updating profile.", ex);
                MessageBox.Show(
                    ex.Message,
                    Lang.modifyProfileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnBrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = Lang.profileSelectAvatarTitle,
                Filter = Lang.profileImageFilter,
                Multiselect = false
            };

            var result = dialog.ShowDialog();
            if (result == true)
            {
                LoadPreviewFromFile(dialog.FileName);
                txtAvatarUrl.Text = new Uri(dialog.FileName, UriKind.Absolute).AbsoluteUri;
            }
        }

        private void BtnClearImageClick(object sender, RoutedEventArgs e)
        {
            txtAvatarUrl.Text = string.Empty;
            imgPreview.Source = null;
        }

        private void LoadPreviewFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Logger.WarnFormat(
                    "Requested avatar preview from invalid file path: '{0}'.",
                    filePath ?? "<null>");

                imgPreview.Source = null;
                return;
            }

            imgPreview.Source = UiImageHelper.TryCreateFromUrlOrPath(filePath, 128);
        }

        private void LoadPreviewFromURL(string source)
        {
            imgPreview.Source = UiImageHelper.TryCreateFromUrlOrPath(source, 128);
        }

        private void LogoutClick(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
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
                Logger.Warn("Auth fault while logging out.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    Lang.logoutTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while logging out.", ex);
                MessageBox.Show(
                    ex.Message,
                    Lang.logoutTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while logging out.", ex);
                MessageBox.Show(
                    ex.Message,
                    Lang.logoutTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                var handler = LoggedOut;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }
    }
}
