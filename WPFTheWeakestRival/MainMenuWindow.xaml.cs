using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;


namespace WPFTheWeakestRival
{
    public partial class MainMenuWindow : Window
    {
        private const double OVERLAY_BLUR_MAX_RADIUS = 6.0;
        private const int OVERLAY_FADE_IN_DURATION_MS = 180;
        private const int OVERLAY_FADE_OUT_DURATION_MS = 160;
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;


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
            try { AppServices.Friends.FriendsUpdated -= OnFriendsUpdated; } catch (Exception ex) { }
            try { AppServices.DisposeAll(); } catch (Exception ex) { }
        }

        private void RefreshProfileButtonAvatar()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                var myProfile = AppServices.Lobby.GetMyProfile(token);
                var px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch (Exception ex) { }
                var src = UiImageHelper.TryCreateFromUrlOrPath(myProfile?.ProfileImageUrl) ?? UiImageHelper.DefaultAvatar(px);
                if (FindName("imgAvatar") is Image img)
                {
                    img.Source = src;
                }
            }
            catch (Exception ex)
            {
                var px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch (Exception ex2) { }
                if (FindName("imgAvatar") is Image img)
                {
                    img.Source = UiImageHelper.DefaultAvatar(px);
                }
            }
        }

        private void BtnSettingsClick(object sender, RoutedEventArgs e)
        {
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

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
            var anim = new DoubleAnimation(pnlOverlayHost.Opacity, 0, TimeSpan.FromMilliseconds(OVERLAY_FADE_OUT_DURATION_MS))
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
            var page = new Pages.PlayOptionsPage { Title = Lang.playOptionsTitle };

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
                    var res = await AppServices.Lobby.CreateLobbyAsync(token, Lang.lobbyTitle, 8);
                    if (res?.Lobby == null)
                    {
                        MessageBox.Show(Lang.lobbyCreateFailed);
                        return;
                    }

                    OpenLobbyWindow(res.Lobby.LobbyName, res.Lobby.LobbyId, res.Lobby.AccessCode);
                }
                catch (FaultException<LobbyService.ServiceFault> ex)
                {
                    MessageBox.Show($"{ex.Detail.Code}: {ex.Detail.Message}", Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (CommunicationException ex)
                {
                    MessageBox.Show(Lang.noConnection + Environment.NewLine + ex.Message, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Lang.lobbyCreateFailed + " " + ex.Message, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var res = await AppServices.Lobby.JoinByCodeAsync(token, code);
                    if (res?.Lobby == null)
                    {
                        MessageBox.Show(Lang.lobbyJoinFailed);
                        return;
                    }

                    var ac = string.IsNullOrWhiteSpace(res.Lobby.AccessCode) ? code : res.Lobby.AccessCode;
                    OpenLobbyWindow(res.Lobby.LobbyName, res.Lobby.LobbyId, ac);
                }
                catch (FaultException<LobbyService.ServiceFault> ex)
                {
                    MessageBox.Show($"{ex.Detail.Code}: {ex.Detail.Message}", Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (CommunicationException ex)
                {
                    MessageBox.Show(Lang.noConnection + Environment.NewLine + ex.Message, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Lang.lobbyJoinFailed + " " + ex.Message, Lang.lobbyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            ShowOverlay(page);
        }

        private void OpenLobbyWindow(string lobbyName, Guid lobbyId, string accessCode)
        {
            var win = new LobbyWindow
            {
                Owner = this
            };
            win.InitializeExistingLobby(lobbyId, accessCode, lobbyName);
            win.Show();
            Hide();

            win.Closed += (_, __) =>
            {
                try { Show(); } catch (Exception ex) { }
            };
        }

        private void OnFriendsUpdated(System.Collections.Generic.IReadOnlyList<FriendItem> list, int pending)
        {
            friendItems.Clear();
            foreach (var f in list)
            {
                friendItems.Add(f);
            }
            pendingRequests = pending;
            UpdateFriendDrawerUi();
        }

        private void UpdateFriendDrawerUi()
        {
            friendsEmptyPanel.Visibility = friendItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            lstFriends.Visibility = friendItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            txtRequestsCount.Text = pendingRequests.ToString();
        }

        private async void OpenFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible) return;

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

            if (FindResource("sbOpenFriendsDrawer") is Storyboard open)
            {
                open.Begin(this, true);
            }
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility != Visibility.Visible) return;

            if (FindResource("sbCloseFriendsDrawer") is Storyboard close)
            {
                close.Completed += (_, __) =>
                {
                    friendsDrawerHost.Visibility = Visibility.Collapsed;
                    if (pnlOverlayHost.Visibility != Visibility.Visible)
                    {
                        grdMainArea.Effect = null;
                    }
                };
                close.Begin(this, true);
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
            OpenFriendsDrawer();
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
            try { LoginWindow.AppSession.CurrentToken = null; } catch (Exception ex) { }
            HideOverlay();
            var loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        }
    }
}
