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
using WPFTheWeakestRival.Infrastructure.Faults;
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

        private const string CTX_REFRESH_PROFILE_AVATAR = "MainMenuWindow.RefreshProfileButtonAvatar";
        private const string CTX_CREATE_LOBBY = "MainMenuWindow.OnCreateLobbyRequestedAsync";
        private const string CTX_JOIN_LOBBY = "MainMenuWindow.OnJoinLobbyRequestedAsync";
        private const string CTX_SAVE_AVATAR = "MainMenuWindow.SaveAvatarToServerAsync";

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

            if (lstFriends != null)
            {
                lstFriends.ItemsSource = friendItems;
            }
            else
            {
                Logger.Warn("MainMenuWindow: lstFriends is null. Friends list binding skipped.");
            }

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
                Logger.Warn("MainMenuWindow.OnUnloaded: detach FriendsUpdated failed.", ex);
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

                if (resource is int intValue)
                {
                    return intValue;
                }

                Logger.WarnFormat(
                    "MainMenuWindow.GetAvatarSize: resource '{0}' is not a number. Using default.",
                    AVATAR_SIZE_RESOURCE_KEY);
            }
            catch (Exception ex)
            {
                Logger.Warn("MainMenuWindow.GetAvatarSize: resource lookup failed. Using default avatar size.", ex);
            }

            return DEFAULT_AVATAR_SIZE;
        }

        private void SetAvatarImage(ImageSource imageSource)
        {
            if (imgAvatar == null)
            {
                Logger.Warn("MainMenuWindow.SetAvatarImage: imgAvatar is null.");
                return;
            }

            imgAvatar.Source = imageSource;
        }

        private void SetDefaultAvatar()
        {
            var avatarSize = GetAvatarSize();

            SetAvatarImage(UiImageHelper.DefaultAvatar(avatarSize));

            if (UserAvatarControl == null)
            {
                Logger.Warn("MainMenuWindow.SetDefaultAvatar: UserAvatarControl is null.");
                return;
            }

            UserAvatarControl.ProfileImage = null;
            UserAvatarControl.FacePhoto = null;
            UserAvatarControl.Appearance = null;
            UserAvatarControl.UseProfilePhotoAsFace = false;
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
                    Logger.Info("MainMenuWindow.RefreshProfileButtonAvatar: no token. Using default avatar.");
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
                    Logger.Warn("MainMenuWindow.RefreshProfileButtonAvatar: UserAvatarControl is null.");
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
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                string faultCode = ex.Detail == null ? string.Empty : (ex.Detail.Code ?? string.Empty);
                string faultMessage = ex.Detail == null ? (ex.Message ?? string.Empty) : (ex.Detail.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        faultCode,
                        faultMessage,
                        CTX_REFRESH_PROFILE_AVATAR,
                        Logger,
                        this))
                {
                    SetDefaultAvatar();
                    return;
                }

                Logger.Warn("MainMenuWindow.RefreshProfileButtonAvatar: lobby fault.", ex);
                SetDefaultAvatar();
            }
            catch (Exception ex)
            {
                Logger.Warn("MainMenuWindow.RefreshProfileButtonAvatar: unexpected error.", ex);
                SetDefaultAvatar();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            if (friendsDrawerHost != null && friendsDrawerHost.Visibility == Visibility.Visible)
            {
                CloseFriendsDrawer();
                return;
            }

            if (pnlOverlayHost != null && pnlOverlayHost.Visibility == Visibility.Visible)
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

                    Logger.WarnFormat(
                        "MainMenuWindow.GetOverlayBackgroundForPage: resource '{0}' is not an ImageSource.",
                        ADD_FRIEND_BACKGROUND_RESOURCE_KEY);
                }
                catch (Exception ex)
                {
                    Logger.Warn("MainMenuWindow.GetOverlayBackgroundForPage: background resource failed.", ex);
                }
            }

            return Brushes.Transparent;
        }

        private void ShowOverlay(Page page)
        {
            if (page == null)
            {
                Logger.Warn("MainMenuWindow.ShowOverlay: page is null.");
                return;
            }

            if (frmOverlayFrame == null || pnlOverlayHost == null || grdMainArea == null)
            {
                Logger.Warn("MainMenuWindow.ShowOverlay: overlay UI refs missing.");
                return;
            }

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
            if (pnlOverlayHost == null || frmOverlayFrame == null)
            {
                Logger.Warn("MainMenuWindow.HideOverlay: overlay UI refs missing.");
                return;
            }

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

                if (friendsDrawerHost == null || friendsDrawerHost.Visibility != Visibility.Visible)
                {
                    if (grdMainArea != null)
                    {
                        grdMainArea.Effect = null;
                    }
                }
            };

            pnlOverlayHost.BeginAnimation(OpacityProperty, anim);

            if (grdMainArea != null && grdMainArea.Effect is BlurEffect be)
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
                Logger.Warn("MainMenuWindow.OnCreateLobbyRequestedAsync: token missing.");
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            try
            {
                var result = await AppServices.Lobby.CreateLobbyAsync(token, Lang.lobbyTitle, DEFAULT_MAX_LOBBY_PLAYERS);
                if (result?.Lobby == null)
                {
                    Logger.Warn("MainMenuWindow.OnCreateLobbyRequestedAsync: CreateLobby returned null Lobby.");
                    MessageBox.Show(Lang.lobbyCreateFailed);
                    return;
                }

                OpenLobbyWindow(result.Lobby);
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                string faultCode = ex.Detail == null ? string.Empty : (ex.Detail.Code ?? string.Empty);
                string faultMessage = ex.Detail == null ? (ex.Message ?? string.Empty) : (ex.Detail.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        faultCode,
                        faultMessage,
                        CTX_CREATE_LOBBY,
                        Logger,
                        this))
                {
                    return;
                }

                Logger.Warn("MainMenuWindow.OnCreateLobbyRequestedAsync: lobby fault while creating lobby.", ex);

                MessageBox.Show(
                    ex.Detail != null ? (ex.Detail.Code + ": " + ex.Detail.Message) : Lang.lobbyCreateFailed,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("MainMenuWindow.OnCreateLobbyRequestedAsync: communication error.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("MainMenuWindow.OnCreateLobbyRequestedAsync: timeout.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("MainMenuWindow.OnCreateLobbyRequestedAsync: unexpected error.", ex);
                MessageBox.Show(Lang.lobbyCreateFailed, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnJoinLobbyRequestedAsync(string code)
        {
            HideOverlay();

            var token = GetCurrentToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Logger.Warn("MainMenuWindow.OnJoinLobbyRequestedAsync: token missing.");
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                Logger.Warn("MainMenuWindow.OnJoinLobbyRequestedAsync: access code empty.");
                MessageBox.Show(Lang.lobbyEnterAccessCode);
                return;
            }

            try
            {
                var result = await AppServices.Lobby.JoinByCodeAsync(token, code);
                if (result?.Lobby == null)
                {
                    Logger.Warn("MainMenuWindow.OnJoinLobbyRequestedAsync: JoinByCode returned null Lobby.");
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
                string faultCode = ex.Detail == null ? string.Empty : (ex.Detail.Code ?? string.Empty);
                string faultMessage = ex.Detail == null ? (ex.Message ?? string.Empty) : (ex.Detail.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        faultCode,
                        faultMessage,
                        CTX_JOIN_LOBBY,
                        Logger,
                        this))
                {
                    return;
                }

                Logger.Warn("MainMenuWindow.OnJoinLobbyRequestedAsync: lobby fault while joining lobby by code.", ex);

                MessageBox.Show(
                    ex.Detail != null ? (ex.Detail.Code + ": " + ex.Detail.Message) : Lang.lobbyJoinFailed,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("MainMenuWindow.OnJoinLobbyRequestedAsync: communication error.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("MainMenuWindow.OnJoinLobbyRequestedAsync: timeout.", ex);
                MessageBox.Show(Lang.noConnection, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("MainMenuWindow.OnJoinLobbyRequestedAsync: unexpected error.", ex);
                MessageBox.Show(Lang.lobbyJoinFailed, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLobbyWindow(LobbyContracts.LobbyInfo lobby)
        {
            if (lobby == null)
            {
                Logger.Warn("MainMenuWindow.OpenLobbyWindow: lobby is null.");
                MessageBox.Show(Lang.lobbyJoinFailed, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new LobbyWindow { Owner = this };
            window.InitializeExistingLobby(lobby);
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
                    Logger.Warn("MainMenuWindow.OpenLobbyWindow: error showing MainMenuWindow after LobbyWindow closed.", ex);
                }
            };
        }

        private void OnFriendsUpdated(System.Collections.Generic.IReadOnlyList<FriendItem> list, int pending)
        {
            if (list == null)
            {
                Logger.Warn("MainMenuWindow.OnFriendsUpdated: list is null.");
                friendItems.Clear();
                pendingRequests = pending;
                UpdateFriendDrawerUi();
                return;
            }

            friendItems.Clear();

            foreach (var item in list)
            {
                if (item == null)
                {
                    continue;
                }

                friendItems.Add(item);
            }

            pendingRequests = pending;
            UpdateFriendDrawerUi();
        }

        private void UpdateFriendDrawerUi()
        {
            var isEmpty = friendItems.Count == 0;

            if (friendsEmptyPanel != null)
            {
                friendsEmptyPanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                Logger.Warn("MainMenuWindow.UpdateFriendDrawerUi: friendsEmptyPanel is null.");
            }

            if (lstFriends != null)
            {
                lstFriends.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                Logger.Warn("MainMenuWindow.UpdateFriendDrawerUi: lstFriends is null.");
            }

            if (txtRequestsCount != null)
            {
                txtRequestsCount.Text = pendingRequests.ToString();
            }
            else
            {
                Logger.Warn("MainMenuWindow.UpdateFriendDrawerUi: txtRequestsCount is null.");
            }
        }

        private async Task OpenFriendsDrawer()
        {
            if (friendsDrawerHost == null)
            {
                Logger.Warn("MainMenuWindow.OpenFriendsDrawer: friendsDrawerHost is null.");
                return;
            }

            if (friendsDrawerHost.Visibility == Visibility.Visible)
            {
                Logger.Info("MainMenuWindow.OpenFriendsDrawer: drawer already visible.");
                return;
            }

            if (grdMainArea != null)
            {
                grdMainArea.Effect = overlayBlur;
            }
            else
            {
                Logger.Warn("MainMenuWindow.OpenFriendsDrawer: grdMainArea is null.");
            }

            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlur.Radius, FRIENDS_DRAWER_BLUR_RADIUS, TimeSpan.FromMilliseconds(DRAWER_ANIM_IN_MS))
                {
                    EasingFunction = new QuadraticEase()
                });

            try
            {
                await AppServices.Friends.ManualRefreshAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn("MainMenuWindow.OpenFriendsDrawer: ManualRefreshAsync failed.", ex);
            }

            if (friendsDrawerTT != null)
            {
                friendsDrawerTT.X = FRIENDS_DRAWER_INITIAL_TRANSLATION_X;
            }
            else
            {
                Logger.Warn("MainMenuWindow.OpenFriendsDrawer: friendsDrawerTT is null.");
            }

            friendsDrawerHost.Opacity = 0;
            friendsDrawerHost.Visibility = Visibility.Visible;

            if (FindResource("sbOpenFriendsDrawer") is Storyboard openStoryboard)
            {
                openStoryboard.Begin(this, true);
                return;
            }

            Logger.Warn("MainMenuWindow.OpenFriendsDrawer: storyboard 'sbOpenFriendsDrawer' not found.");
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost == null)
            {
                Logger.Warn("MainMenuWindow.CloseFriendsDrawer: friendsDrawerHost is null.");
                return;
            }

            if (friendsDrawerHost.Visibility != Visibility.Visible)
            {
                Logger.Info("MainMenuWindow.CloseFriendsDrawer: drawer already hidden.");
                return;
            }

            if (FindResource("sbCloseFriendsDrawer") is Storyboard closeStoryboard)
            {
                EventHandler handler = null;
                handler = (_, __) =>
                {
                    try
                    {
                        closeStoryboard.Completed -= handler;

                        friendsDrawerHost.Visibility = Visibility.Collapsed;

                        if (pnlOverlayHost == null || pnlOverlayHost.Visibility != Visibility.Visible)
                        {
                            if (grdMainArea != null)
                            {
                                grdMainArea.Effect = null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("MainMenuWindow.CloseFriendsDrawer: close storyboard completion failed.", ex);
                    }
                };

                closeStoryboard.Completed += handler;
                closeStoryboard.Begin(this, true);
            }
            else
            {
                Logger.Warn("MainMenuWindow.CloseFriendsDrawer: storyboard 'sbCloseFriendsDrawer' not found.");
                friendsDrawerHost.Visibility = Visibility.Collapsed;

                if (pnlOverlayHost == null || pnlOverlayHost.Visibility != Visibility.Visible)
                {
                    if (grdMainArea != null)
                    {
                        grdMainArea.Effect = null;
                    }
                }
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
                Logger.Warn("MainMenuWindow.BtnSendFriendRequestClick: token missing.");
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new Pages.AddFriendPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);

            page.CloseRequested += (_, __) => { HideOverlay(); };

            page.LoginRequested += (_, __) =>
            {
                HideOverlay();
                OnLoggedOut(this, EventArgs.Empty);
            };

            ShowOverlay(page);
        }

        private void BtnViewRequestsClick(object sender, RoutedEventArgs e)
        {
            var token = GetCurrentToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Logger.Warn("MainMenuWindow.BtnViewRequestsClick: token missing.");
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
                Logger.Warn("MainMenuWindow.BtnModifyProfileClick: token missing.");
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

            try
            {
                SessionCleanup.Shutdown("MainMenuWindow.OnLoggedOut");
            }
            catch (Exception ex)
            {
                Logger.Warn("MainMenuWindow.OnLoggedOut: SessionCleanup failed.", ex);
            }

            try
            {
                var loginWindow = new LoginWindow();
                Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                Logger.Error("MainMenuWindow.OnLoggedOut: failed to show LoginWindow.", ex);
            }

            Close();
        }

        private async void BtnPersonalizationClick(object sender, RoutedEventArgs e)
        {
            if (UserAvatarControl == null)
            {
                Logger.Warn("MainMenuWindow.BtnPersonalizationClick: UserAvatarControl is null.");
                MessageBox.Show(Lang.profileLoadFailed, Lang.profileTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                Logger.Info("MainMenuWindow.BtnPersonalizationClick: dialog cancelled.");
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
                if (string.IsNullOrWhiteSpace(token))
                {
                    Logger.Warn("MainMenuWindow.SaveAvatarToServerAsync: token missing.");
                    return;
                }

                if (UserAvatarControl == null)
                {
                    Logger.Warn("MainMenuWindow.SaveAvatarToServerAsync: UserAvatarControl is null.");
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
                string faultCode = ex.Detail == null ? string.Empty : (ex.Detail.Code ?? string.Empty);
                string faultMessage = ex.Detail == null ? (ex.Message ?? string.Empty) : (ex.Detail.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        faultCode,
                        faultMessage,
                        CTX_SAVE_AVATAR,
                        Logger,
                        this))
                {
                    return;
                }

                Logger.Warn("MainMenuWindow.SaveAvatarToServerAsync: lobby fault while updating avatar.", ex);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("MainMenuWindow.SaveAvatarToServerAsync: communication error.", ex);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("MainMenuWindow.SaveAvatarToServerAsync: timeout.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("MainMenuWindow.SaveAvatarToServerAsync: unexpected error.", ex);
            }
        }
    }
}
