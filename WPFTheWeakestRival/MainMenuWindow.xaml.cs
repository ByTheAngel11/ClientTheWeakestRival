using log4net;
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
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Controls;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;
using WPFTheWeakestRival.Windows;
using LobbyContracts = WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival
{
    public partial class MainMenuWindow : Window
    {
        private const double OVERLAY_BLUR_MAX_RADIUS = 6.0;
        private const int OVERLAY_FADE_IN_DURATION_MS = 180;
        private const int OVERLAY_FADE_OUT_DURATION_MS = 160;
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;

        private const double FRIENDS_DRAWER_BLUR_RADIUS = 3.0;
        private const double FRIENDS_DRAWER_INITIAL_TRANSLATION_X = 340.0;

        private const int DEFAULT_AVATAR_SIZE = 28;
        private const string AVATAR_SIZE_RESOURCE_KEY = "AvatarSize";
        private const string ADD_FRIEND_BACKGROUND_RESOURCE_KEY = "AddFriendBackground";

        private const int DEFAULT_MAX_LOBBY_PLAYERS = 8;

        private const int PROFILE_BUTTON_AVATAR_DECODE_WIDTH = 48;
        private const int AVATAR_FACE_DECODE_WIDTH = 256;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(MainMenuWindow));

        private readonly BlurEffect overlayBlur = new BlurEffect { Radius = 0 };
        private readonly ObservableCollection<FriendItem> friendItems = new ObservableCollection<FriendItem>();
        private int pendingRequests;

        public MainMenuWindow()
        {
            InitializeComponent();

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

            if (imgAvatar != null)
            {
                RenderOptions.SetBitmapScalingMode(imgAvatar, BitmapScalingMode.HighQuality);
            }

            if (UserAvatarControl != null)
            {
                RenderOptions.SetBitmapScalingMode(UserAvatarControl, BitmapScalingMode.HighQuality);
            }

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
                if (friendItems != null)
                {
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("MainMenuWindow.OnUnloaded: detach failed.", ex);
            }

            try
            {
                AppServices.StopAll();
            }
            catch (Exception ex)
            {
                Logger.Warn("MainMenuWindow.OnUnloaded: stop failed.", ex);
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
            if (imgAvatar == null)
            {
                return;
            }

            imgAvatar.Source = imageSource;
        }

        private void SetDefaultAvatar()
        {
            var avatarSize = GetAvatarSize();

            SetAvatarImage(UiImageHelper.DefaultAvatar(avatarSize));

            if (UserAvatarControl != null)
            {
                UserAvatarControl.ProfileImage = null;
                UserAvatarControl.FacePhoto = null;
                UserAvatarControl.Appearance = null;
                UserAvatarControl.UseProfilePhotoAsFace = false;
            }
        }

        private static string GetCurrentToken()
        {
            return LoginWindow.AppSession.CurrentToken?.Token;
        }

        private void RefreshProfileButtonAvatar()
        {
            try
            {
                var token = GetCurrentToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    SetDefaultAvatar();
                    return;
                }

                var profile = AppServices.Lobby.GetMyProfile(token);

                var smallAvatar =
                    UiImageHelper.TryCreateFromBytes(profile?.ProfileImageBytes, PROFILE_BUTTON_AVATAR_DECODE_WIDTH) ??
                    UiImageHelper.DefaultAvatar(PROFILE_BUTTON_AVATAR_DECODE_WIDTH);

                SetAvatarImage(smallAvatar);

                if (UserAvatarControl == null)
                {
                    return;
                }

                var faceImage = UiImageHelper.TryCreateFromBytes(profile?.ProfileImageBytes, AVATAR_FACE_DECODE_WIDTH);

                var appearanceDto = profile?.Avatar;
                if (appearanceDto == null)
                {
                    UserAvatarControl.Appearance = null;
                    UserAvatarControl.ProfileImage = null;
                    return;
                }

                var appearance = AvatarMapper.FromLobbyDto(appearanceDto);
                appearance.ProfileImage = faceImage;
                appearance.UseProfilePhotoAsFace = appearanceDto.UseProfilePhotoAsFace && faceImage != null;

                UserAvatarControl.ProfileImage = faceImage;
                UserAvatarControl.Appearance = appearance;
            }
            catch
            {
                SetDefaultAvatar();
            }
        }


        private void BtnSettingsClick(object sender, RoutedEventArgs e)
        {
            // Reservado
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

        private Brush GetOverlayBackgroundForPage(Page page)
        {
            if (page == null)
            {
                return Brushes.Transparent;
            }

            if (page is Pages.AddFriendPage || page is Pages.FriendRequestsPage || page is Pages.PlayOptionsPage)
            {
                try
                {
                    var resource = FindResource(ADD_FRIEND_BACKGROUND_RESOURCE_KEY);
                    if (resource is ImageSource imageSource)
                    {
                        return new ImageBrush(imageSource)
                        {
                            Stretch = Stretch.UniformToFill,
                            AlignmentX = AlignmentX.Center,
                            AlignmentY = AlignmentY.Center
                        };
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error loading overlay background. Falling back to transparent background.", ex);
                }
            }

            return Brushes.Transparent;
        }

        private void ShowOverlay(Page page)
        {
            if (overlayContentGrid != null)
            {
                overlayContentGrid.Background = GetOverlayBackgroundForPage(page);
            }

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
            var anim = new DoubleAnimation(pnlOverlayHost.Opacity, 0, TimeSpan.FromMilliseconds(OVERLAY_FADE_OUT_DURATION_MS))
            {
                EasingFunction = new QuadraticEase()
            };

            anim.Completed += (_, __) =>
            {
                pnlOverlayHost.Visibility = Visibility.Collapsed;
                frmOverlayFrame.Content = null;

                if (overlayContentGrid != null)
                {
                    overlayContentGrid.Background = Brushes.Transparent;
                }

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

            page.CreateRequested += async (_, __) => { await OnCreateLobbyRequestedAsync(); };
            page.JoinRequested += async (_, code) => { await OnJoinLobbyRequestedAsync(code); };

            ShowOverlay(page);
        }

        private async Task OnCreateLobbyRequestedAsync()
        {
            HideOverlay();

            var token = GetCurrentToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            try
            {
                var result = await AppServices.Lobby.CreateLobbyAsync(token, Lang.lobbyTitle, DEFAULT_MAX_LOBBY_PLAYERS);
                if (result?.Lobby == null)
                {
                    MessageBox.Show(Lang.lobbyCreateFailed);
                    return;
                }

                OpenLobbyWindow(result.Lobby);
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                Logger.Warn("Lobby service fault while creating lobby.", ex);
                MessageBox.Show(
                    ex.Detail != null ? (ex.Detail.Code + ": " + ex.Detail.Message) : Lang.lobbyCreateFailed,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while creating lobby.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("Timeout while creating lobby.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while creating lobby.", ex);
                MessageBox.Show(Lang.lobbyCreateFailed, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnJoinLobbyRequestedAsync(string code)
        {
            HideOverlay();

            var token = GetCurrentToken();
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

                if (string.IsNullOrWhiteSpace(result.Lobby.AccessCode))
                {
                    result.Lobby.AccessCode = code;
                }

                OpenLobbyWindow(result.Lobby);
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                Logger.Warn("Lobby service fault while joining lobby by code.", ex);
                MessageBox.Show(
                    ex.Detail != null ? (ex.Detail.Code + ": " + ex.Detail.Message) : Lang.lobbyJoinFailed,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while joining lobby by code.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("Timeout while joining lobby by code.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while joining lobby by code.", ex);
                MessageBox.Show(Lang.lobbyJoinFailed, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLobbyWindow(LobbyContracts.LobbyInfo lobby)
        {
            var window = new LobbyWindow { Owner = this };
            window.InitializeExistingLobby(lobby);
            window.Show();
            Hide();

            window.Closed += (_, __) =>
            {
                try { Show(); }
                catch (Exception ex) { Logger.Warn("Error showing MainMenuWindow after LobbyWindow was closed.", ex); }
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
                new DoubleAnimation(overlayBlur.Radius, FRIENDS_DRAWER_BLUR_RADIUS, TimeSpan.FromMilliseconds(DRAWER_ANIM_IN_MS))
                {
                    EasingFunction = new QuadraticEase()
                });

            await AppServices.Friends.ManualRefreshAsync();

            friendsDrawerTT.X = FRIENDS_DRAWER_INITIAL_TRANSLATION_X;
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
            var token = GetCurrentToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new Pages.AddFriendPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);
            page.CloseRequested += (_, __) => { HideOverlay(); };

            ShowOverlay(page);
        }

        private void BtnViewRequestsClick(object sender, RoutedEventArgs e)
        {
            var token = GetCurrentToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new Pages.FriendRequestsPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);
            page.CloseRequested += (_, __) => { HideOverlay(); };

            ShowOverlay(page);
        }

        private void BtnModifyProfileClick(object sender, RoutedEventArgs e)
        {
            var token = GetCurrentToken();
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

        private void BtnScoreClick(object sender, RoutedEventArgs e)
        {
            var page = new Pages.LeaderboardPage();
            page.CloseRequested += (_, __) => { HideOverlay(); };

            ShowOverlay(page);
        }

        private void OnLoggedOut(object sender, EventArgs e)
        {
            HideOverlay();

            SessionCleanup.Shutdown("MainMenuWindow.OnLoggedOut");

            var loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();

            Close();
        }


        private async void BtnPersonalizationClick(object sender, RoutedEventArgs e)
        {
            if (UserAvatarControl == null)
            {
                return;
            }

            var dialog = new AvatarCustomizationWindow(
                UserAvatarControl.BodyColor,
                UserAvatarControl.PantsColor,
                UserAvatarControl.SkinColor,
                UserAvatarControl.HatColor,
                UserAvatarControl.HatType,
                UserAvatarControl.FaceType,
                UserAvatarControl.FacePhoto)
            {
                Owner = this
            };

            var result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            var resolvedFacePhoto = dialog.ResultUseProfilePhotoAsFace
                ? (UserAvatarControl.FacePhoto ?? UserAvatarControl.ProfileImage)
                : null;

            UserAvatarControl.SetAppearance(
                dialog.ResultSkinColor,
                dialog.ResultBodyColor,
                dialog.ResultPantsColor,
                dialog.ResultHatType,
                dialog.ResultHatColor,
                dialog.ResultFaceType,
                resolvedFacePhoto);

            await SaveAvatarToServerAsync();
            RefreshProfileButtonAvatar();
        }

        private async Task SaveAvatarToServerAsync()
        {
            try
            {
                var token = GetCurrentToken();
                if (string.IsNullOrWhiteSpace(token) || UserAvatarControl == null)
                {
                    return;
                }

                var bodyIndex = AvatarMapper.GetBodyColorIndex(UserAvatarControl.BodyColor);
                var pantsIndex = AvatarMapper.GetPantsColorIndex(UserAvatarControl.PantsColor);
                var hatColorIndex = AvatarMapper.GetHatColorIndex(UserAvatarControl.HatColor);
                var hatTypeIndex = AvatarMapper.GetHatTypeIndex(UserAvatarControl.HatType);
                var faceTypeIndex = AvatarMapper.GetFaceTypeIndex(UserAvatarControl.FaceType);

                var request = new LobbyService.UpdateAvatarRequest
                {
                    Token = token,
                    BodyColor = (byte)bodyIndex,
                    PantsColor = (byte)pantsIndex,
                    HatType = (byte)hatTypeIndex,
                    HatColor = (byte)hatColorIndex,
                    FaceType = (byte)faceTypeIndex,
                    UseProfilePhotoAsFace = UserAvatarControl.UseProfilePhotoAsFace
                };

                await AppServices.Lobby.RawClient.UpdateAvatarAsync(request);
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                Logger.Warn("Lobby service fault while updating avatar.", ex);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while updating avatar.", ex);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("Timeout while updating avatar.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while updating avatar.", ex);
            }
        }
    }
}
