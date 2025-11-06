using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using log4net;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival
{
    public partial class MainMenuWindow : Window
    {
        private const double OVERLAY_BLUR_MAX_RADIUS = 6.0;
        private const int OVERLAY_FADE_IN_DURATION_MS = 180;
        private const int OVERLAY_FADE_OUT_DURATION_MS = 160;
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;

        private const int DEFAULT_AVATAR_SIZE = 28;
        private const string AVATAR_SIZE_RESOURCE_KEY = "AvatarSize";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(MainMenuWindow));

        private readonly BlurEffect overlayBlur = new BlurEffect { Radius = 0 };
        private readonly ObservableCollection<FriendItem> friendItems = new ObservableCollection<FriendItem>();
        private int pendingRequests;

        public MainMenuWindow()
        {
            InitializeComponent();

            lstFriends.ItemsSource = friendItems;
            UpdateFriendDrawerUi();

            RefreshProfileButtonAvatar();

            AppServices.Friends.FriendsUpdated += OnFriendsUpdated;
            AppServices.Friends.Start();

            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AppServices.Friends.FriendsUpdated -= OnFriendsUpdated;
            }
            catch (Exception ex)
            {
                Logger.Warn("Error detaching FriendsUpdated handler on window unload.", ex);
            }

            try
            {
                AppServices.Friends.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error stopping Friends service on window unload.", ex);
            }
        }

        private int GetAvatarSize()
        {
            try
            {
                var resource = FindResource(AVATAR_SIZE_RESOURCE_KEY);
                if (resource is double doubleValue)
                {
                    return (int)doubleValue;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error retrieving avatar size resource. Using default avatar size.", ex);
            }

            return DEFAULT_AVATAR_SIZE;
        }

        private void SetAvatarImage(ImageSource imageSource)
        {
            if (FindName("imgAvatar") is Image img && imageSource != null)
            {
                img.Source = imageSource;
            }
        }

        private void SetDefaultAvatar()
        {
            var avatarSize = GetAvatarSize();
            var defaultAvatar = UiImageHelper.DefaultAvatar(avatarSize);
            SetAvatarImage(defaultAvatar);
        }

        private void RefreshProfileButtonAvatar()
        {
            var avatarSize = GetAvatarSize();

            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    SetDefaultAvatar();
                    return;
                }

                var profile = AppServices.Lobby.GetMyProfile(token);

                var avatarPath = profile?.ProfileImageUrl;
                var avatarImage = UiImageHelper.TryCreateFromUrlOrPath(avatarPath)
                                  ?? UiImageHelper.DefaultAvatar(avatarSize);

                SetAvatarImage(avatarImage);
            }
            catch (FaultException<AuthService.ServiceFault> ex)
            {
                Logger.Warn("Auth fault while refreshing profile button avatar.", ex);
                SetDefaultAvatar();
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while refreshing profile button avatar.", ex);
                SetDefaultAvatar();
            }
            catch (ObjectDisposedException ex)
            {
                Logger.Warn("ObjectDisposedException while refreshing profile button avatar.", ex);
                SetDefaultAvatar();
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while refreshing profile button avatar.", ex);
                SetDefaultAvatar();
            }
        }

        private void BtnSettingsClick(object sender, RoutedEventArgs e)
        {
            // TODO: Settings page
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            if (friendsDrawerHost.Visibility == Visibility.Visible)
            {
                CloseFriendsDrawer();
                return;
            }

            if (pnlOverlayHost.Visibility == Visibility.Visible)
            {
                HideOverlay();
            }
        }

        private void CloseOverlayClick(object sender, RoutedEventArgs e)
        {
            HideOverlay();
        }

        private void ShowOverlay(Page page)
        {
            frmOverlayFrame.Content = page;
            grdMainArea.Effect = overlayBlur;
            pnlOverlayHost.Visibility = Visibility.Visible;

            pnlOverlayHost.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(OVERLAY_FADE_IN_DURATION_MS))
                {
                    EasingFunction = new QuadraticEase()
                });

            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(0, OVERLAY_BLUR_MAX_RADIUS, TimeSpan.FromMilliseconds(OVERLAY_FADE_IN_DURATION_MS))
                {
                    EasingFunction = new QuadraticEase()
                });
        }

        private void HideOverlay()
        {
            var anim = new DoubleAnimation(
                pnlOverlayHost.Opacity,
                0,
                TimeSpan.FromMilliseconds(OVERLAY_FADE_OUT_DURATION_MS))
            {
                EasingFunction = new QuadraticEase()
            };

            anim.Completed += (_, __) =>
            {
                pnlOverlayHost.Visibility = Visibility.Collapsed;
                frmOverlayFrame.Content = null;

                if (friendsDrawerHost.Visibility != Visibility.Visible)
                {
                    grdMainArea.Effect = null;
                }
            };

            pnlOverlayHost.BeginAnimation(OpacityProperty, anim);

            if (grdMainArea.Effect is BlurEffect be)
            {
                be.BeginAnimation(
                    BlurEffect.RadiusProperty,
                    new DoubleAnimation(be.Radius, 0, TimeSpan.FromMilliseconds(OVERLAY_FADE_OUT_DURATION_MS))
                    {
                        EasingFunction = new QuadraticEase()
                    });
            }
        }

        private void BtnPlayClick(object sender, RoutedEventArgs e)
        {
            var page = new Pages.PlayOptionsPage
            {
                Title = Lang.playOptionsTitle
            };

            page.CreateRequested += async (_, __) =>
            {
                HideOverlay();

                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    MessageBox.Show(Lang.noValidSessionCode);
                    return;
                }

                try
                {
                    var result = await AppServices.Lobby.CreateLobbyAsync(token, Lang.lobbyTitle, 8);
                    if (result?.Lobby == null)
                    {
                        MessageBox.Show(Lang.lobbyCreateFailed);
                        return;
                    }

                    OpenLobbyWindow(result.Lobby.LobbyName, result.Lobby.LobbyId, result.Lobby.AccessCode);
                }
                catch (FaultException<LobbyService.ServiceFault> ex)
                {
                    Logger.Warn("Lobby service fault while creating lobby.", ex);
                    MessageBox.Show(
                        $"{ex.Detail.Code}: {ex.Detail.Message}",
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (CommunicationException ex)
                {
                    Logger.Error("Communication error while creating lobby.", ex);
                    MessageBox.Show(
                        Lang.noConnection + Environment.NewLine + ex.Message,
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Logger.Error("Unexpected error while creating lobby.", ex);
                    MessageBox.Show(
                        Lang.lobbyCreateFailed + " " + ex.Message,
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            page.JoinRequested += async (_, code) =>
            {
                HideOverlay();

                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    MessageBox.Show(Lang.noValidSessionCode);
                    return;
                }

                try
                {
                    var result = await AppServices.Lobby.JoinByCodeAsync(token, code);
                    if (result?.Lobby == null)
                    {
                        MessageBox.Show(Lang.lobbyJoinFailed);
                        return;
                    }

                    var accessCode = string.IsNullOrWhiteSpace(result.Lobby.AccessCode)
                        ? code
                        : result.Lobby.AccessCode;

                    OpenLobbyWindow(result.Lobby.LobbyName, result.Lobby.LobbyId, accessCode);
                }
                catch (FaultException<LobbyService.ServiceFault> ex)
                {
                    Logger.Warn("Lobby service fault while joining lobby by code.", ex);
                    MessageBox.Show(
                        $"{ex.Detail.Code}: {ex.Detail.Message}",
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (CommunicationException ex)
                {
                    Logger.Error("Communication error while joining lobby by code.", ex);
                    MessageBox.Show(
                        Lang.noConnection + Environment.NewLine + ex.Message,
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Logger.Error("Unexpected error while joining lobby by code.", ex);
                    MessageBox.Show(
                        Lang.lobbyJoinFailed + " " + ex.Message,
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            ShowOverlay(page);
        }

        private void OpenLobbyWindow(string lobbyName, Guid lobbyId, string accessCode)
        {
            var window = new LobbyWindow
            {
                Owner = this
            };

            window.InitializeExistingLobby(lobbyId, accessCode, lobbyName);
            window.Show();
            Hide();

            window.Closed += (_, __) =>
            {
                try
                {
                    Show();
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error showing MainMenuWindow after LobbyWindow was closed.", ex);
                }
            };
        }

        private void OnFriendsUpdated(System.Collections.Generic.IReadOnlyList<FriendItem> list, int pending)
        {
            friendItems.Clear();

            foreach (var item in list)
            {
                friendItems.Add(item);
            }

            pendingRequests = pending;
            UpdateFriendDrawerUi();
        }

        private void UpdateFriendDrawerUi()
        {
            var isEmpty = friendItems.Count == 0;

            friendsEmptyPanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            lstFriends.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            txtRequestsCount.Text = pendingRequests.ToString();
        }

        private async Task OpenFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible)
            {
                return;
            }

            grdMainArea.Effect = overlayBlur;

            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlur.Radius, 3.0, TimeSpan.FromMilliseconds(DRAWER_ANIM_IN_MS))
                {
                    EasingFunction = new QuadraticEase()
                });

            await AppServices.Friends.ManualRefreshAsync();

            friendsDrawerTT.X = 340;
            friendsDrawerHost.Opacity = 0;
            friendsDrawerHost.Visibility = Visibility.Visible;

            if (FindResource("sbOpenFriendsDrawer") is Storyboard openStoryboard)
            {
                openStoryboard.Begin(this, true);
            }
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility != Visibility.Visible)
            {
                return;
            }

            if (FindResource("sbCloseFriendsDrawer") is Storyboard closeStoryboard)
            {
                closeStoryboard.Completed += (_, __) =>
                {
                    friendsDrawerHost.Visibility = Visibility.Collapsed;

                    if (pnlOverlayHost.Visibility != Visibility.Visible)
                    {
                        grdMainArea.Effect = null;
                    }
                };

                closeStoryboard.Begin(this, true);
            }

            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlur.Radius, 0.0, TimeSpan.FromMilliseconds(DRAWER_ANIM_OUT_MS))
                {
                    EasingFunction = new QuadraticEase()
                });
        }

        private void FriendsDimmerMouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseFriendsDrawer();
        }

        private void BtnCloseFriendsClick(object sender, RoutedEventArgs e)
        {
            CloseFriendsDrawer();
        }

        private void BtnFriendsClick(object sender, RoutedEventArgs e)
        {
            _ = OpenFriendsDrawer();
        }

        private void BtnSendFriendRequestClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new Pages.AddFriendPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);
            ShowOverlay(page);
        }

        private void BtnViewRequestsClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new Pages.FriendRequestsPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);
            ShowOverlay(page);
        }

        private void BtnModifyProfileClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new ModifyProfilePage(
                AppServices.Lobby.RawClient,
                new AuthServiceClient("WSHttpBinding_IAuthService"),
                token)
            {
                Title = Lang.profileTitle
            };

            page.Closed += (_, __) =>
            {
                RefreshProfileButtonAvatar();
                HideOverlay();
            };

            page.LoggedOut += OnLoggedOut;

            ShowOverlay(page);
        }

        private void OnLoggedOut(object sender, EventArgs e)
        {
            try
            {
                LoginWindow.AppSession.CurrentToken = null;
            }
            catch (Exception ex)
            {
                Logger.Warn("Error clearing current session token during logout.", ex);
            }

            HideOverlay();

            var loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();

            Close();
        }
    }
}
