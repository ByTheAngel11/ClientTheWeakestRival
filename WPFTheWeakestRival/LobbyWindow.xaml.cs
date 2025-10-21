using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    /// <summary>
    /// Ventana principal del lobby. Hospeda overlays y maneja callbacks del lobby.
    /// </summary>
    public partial class LobbyWindow : Window, ILobbyServiceCallback
    {
        private const double OverlayBlurMaxRadius = 6.0;
        private const int OverlayFadeInDurationMs = 180;
        private const int OverlayFadeOutDurationMs = 160;

        private const int DrawerAnimInMs = 180;
        private const int DrawerAnimOutMs = 160;

        // Presencia
        private const int HeartbeatIntervalSeconds = 30;
        private const int FriendsRefreshIntervalSeconds = 45;
        private DispatcherTimer presenceTimer;
        private DispatcherTimer friendsRefreshTimer;

        private readonly LobbyServiceClient lobbyServiceClient;
        private readonly FriendServiceClient friendsClient;
        private readonly BlurEffect overlayBlurEffect = new BlurEffect { Radius = 0 };

        private readonly ObservableCollection<FriendItem> friends = new ObservableCollection<FriendItem>();
        private int pendingRequestsCount = 0;

        public LobbyWindow()
        {
            InitializeComponent();

            // WCF: lobby (dúplex) y amigos
            var serviceContext = new InstanceContext(this);
            lobbyServiceClient = new LobbyServiceClient(serviceContext, "WSDualHttpBinding_ILobbyService");
            friendsClient = new FriendServiceClient("WSHttpBinding_IFriendService");

            RefreshProfileButtonAvatar();
            UpdateFriendDrawerUI();

            // Timers de presencia / refresco
            presenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HeartbeatIntervalSeconds) };
            presenceTimer.Tick += async (_, __) => await SendHeartbeatAsync();
            presenceTimer.Start();
            _ = SendHeartbeatAsync(); // primer latido

            friendsRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(FriendsRefreshIntervalSeconds) };
            friendsRefreshTimer.Tick += async (_, __) =>
            {
                if (friendsDrawerHost.Visibility == Visibility.Visible)
                    await RefreshFriendsAsync();
            };
            friendsRefreshTimer.Start();

            Unloaded += OnWindowUnloaded;
        }

        // ========================= Top bar: Perfil =========================
        private void btnModifyProfile_Click(object sender, RoutedEventArgs e) => OnProfileClick(sender, e);
        private void btnSettings_Click(object sender, RoutedEventArgs e) { /* TODO: abrir settings */ }

        private void OnProfileClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            var profilePage = new ModifyProfilePage(lobbyServiceClient, token) { Title = "Perfil" };
            profilePage.Closed += (_, __) => RefreshProfileButtonAvatar();
            ShowOverlayPage(profilePage);
        }

        private void RefreshProfileButtonAvatar()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token)) return;

                var me = lobbyServiceClient.GetMyProfile(token);

                int px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch { /* ignore */ }

                ImageSource src =
                    UiImageHelper.TryCreateFromUrlOrPath(me.ProfileImageUrl, decodePixelWidth: px)
                    ?? UiImageHelper.DefaultAvatar(px);

                var brush = FindName("brushAvatar") as ImageBrush;
                if (brush != null) brush.ImageSource = src;

                var img = FindName("imgAvatar") as Image;
                if (img != null) img.Source = src;
            }
            catch
            {
                int px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch { /* ignore */ }
                var fallback = UiImageHelper.DefaultAvatar(px);

                var brush = FindName("brushAvatar") as ImageBrush;
                if (brush != null) brush.ImageSource = fallback;

                var legacyImg = FindName("imgProfile") as Image;
                if (legacyImg != null) legacyImg.Source = fallback;
            }
        }

        // ========================= Overlay central =========================
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) => OnCloseOverlayClick(sender, e);

        private void OnCloseOverlayClick(object sender, RoutedEventArgs e)
        {
            HideOverlayPanel();
        }

        private void ShowOverlayPage(Page page)
        {
            frmOverlayFrame.Content = page;
            grdMainArea.Effect = overlayBlurEffect;
            pnlOverlayHost.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs),
                EasingFunction = new QuadraticEase()
            };
            pnlOverlayHost.BeginAnimation(OpacityProperty, fadeIn);

            var blurIn = new DoubleAnimation
            {
                From = 0,
                To = OverlayBlurMaxRadius,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs),
                EasingFunction = new QuadraticEase()
            };
            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty, blurIn);
        }

        private void HideOverlayPanel()
        {
            var fadeOut = new DoubleAnimation
            {
                From = pnlOverlayHost.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs),
                EasingFunction = new QuadraticEase()
            };

            fadeOut.Completed += (_, __) =>
            {
                pnlOverlayHost.Visibility = Visibility.Collapsed;
                frmOverlayFrame.Content = null;

                if (friendsDrawerHost.Visibility != Visibility.Visible)
                    grdMainArea.Effect = null;
            };

            pnlOverlayHost.BeginAnimation(OpacityProperty, fadeOut);

            var currentBlur = grdMainArea.Effect as BlurEffect;
            if (currentBlur != null)
            {
                var blurOut = new DoubleAnimation
                {
                    From = currentBlur.Radius,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs),
                    EasingFunction = new QuadraticEase()
                };
                currentBlur.BeginAnimation(BlurEffect.RadiusProperty, blurOut);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (friendsDrawerHost.Visibility == Visibility.Visible)
                {
                    CloseFriendsDrawer();
                    return;
                }

                if (pnlOverlayHost.Visibility == Visibility.Visible)
                {
                    HideOverlayPanel();
                }
            }
        }

        // ========================= Drawer de amigos =========================
        private void btnFriends_Click(object sender, RoutedEventArgs e) => OpenFriendsDrawer();
        private void btnCloseFriends_Click(object sender, RoutedEventArgs e) => CloseFriendsDrawer();
        private void friendsDimmer_MouseDown(object sender, MouseButtonEventArgs e) => CloseFriendsDrawer();

        private async void OpenFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible)
                return;

            grdMainArea.Effect = overlayBlurEffect;
            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlurEffect.Radius, 3.0, TimeSpan.FromMilliseconds(DrawerAnimInMs))
                { EasingFunction = new QuadraticEase() });

            await RefreshFriendsAsync();

            friendsDrawerTT.X = 340;
            friendsDrawerHost.Opacity = 0;
            friendsDrawerHost.Visibility = Visibility.Visible;

            var sb = FindResource("sbOpenFriendsDrawer") as Storyboard;
            if (sb != null) sb.Begin(this, true);
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility != Visibility.Visible)
                return;

            var sb = FindResource("sbCloseFriendsDrawer") as Storyboard;
            if (sb != null)
            {
                sb.Completed += (_, __) =>
                {
                    friendsDrawerHost.Visibility = Visibility.Collapsed;

                    if (pnlOverlayHost.Visibility != Visibility.Visible)
                        grdMainArea.Effect = null;
                };
                sb.Begin(this, true);
            }

            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlurEffect.Radius, 0.0, TimeSpan.FromMilliseconds(DrawerAnimOutMs))
                { EasingFunction = new QuadraticEase() });
        }

        private void btnSendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new WPFTheWeakestRival.Pages.AddFriendPage(friendsClient, token);
            page.FriendsUpdated += async (_, __) => await RefreshFriendsAsync();
            ShowOverlayPage(page);
        }

        private void btnViewRequests_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            var page = new WPFTheWeakestRival.Pages.FriendRequestsPage(friendsClient, token);
            page.FriendsChanged += async (_, __) => await RefreshFriendsAsync();
            ShowOverlayPage(page);
        }

        // ========================= Datos del drawer =========================
        public void SetFriends(IEnumerable<FriendItem> list, int pendingRequestCount)
        {
            friends.Clear();
            if (list != null)
            {
                foreach (var f in list) friends.Add(f);
            }
            pendingRequestsCount = Math.Max(0, pendingRequestCount);
            UpdateFriendDrawerUI();
        }

        private void UpdateFriendDrawerUI()
        {
            lstFriends.ItemsSource = friends;
            friendsEmptyPanel.Visibility = friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            lstFriends.Visibility = friends.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            txtRequestsCount.Text = pendingRequestsCount.ToString();
        }

        private async Task RefreshFriendsAsync()
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var response = await friendsClient.ListFriendsAsync(new ListFriendsRequest
                {
                    Token = token,
                    IncludePendingIncoming = true,
                    IncludePendingOutgoing = false
                });

                friends.Clear();

                var arr = response.Friends ?? new FriendSummary[0];
                for (int i = 0; i < arr.Length; i++)
                {
                    var f = arr[i];
                    var name = string.IsNullOrWhiteSpace(f.DisplayName) ? f.Username : f.DisplayName;
                    var img = UiImageHelper.TryCreateFromUrlOrPath(f.AvatarUrl, 36)
                              ?? UiImageHelper.DefaultAvatar(36);

                    friends.Add(new FriendItem
                    {
                        DisplayName = name ?? string.Empty,
                        StatusText = f.IsOnline ? "Disponible" : "Desconectado",
                        Presence = f.IsOnline ? "Online" : "Offline",
                        Avatar = img
                    });
                }

                pendingRequestsCount = Math.Max(0, (response.PendingIncoming != null ? response.PendingIncoming.Length : 0));
                UpdateFriendDrawerUI();
            }
            catch (FaultException<FriendService.ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Amigos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con FriendService.\n" + cx.Message, "Amigos",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                try
                {
                    if (friendsClient.State == CommunicationState.Faulted)
                        friendsClient.Abort();
                }
                catch { /* ignore */ }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar amigos: " + ex.Message, "Amigos",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ========================= Presencia =========================
        private async Task SendHeartbeatAsync()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token)) return;

                await friendsClient.PresenceHeartbeatAsync(new HeartbeatRequest
                {
                    Token = token,
                    Device = "WPF"
                });
            }
            catch (CommunicationException)
            {
                // Ignorar latidos fallidos por red
            }
            catch
            {
                // Silencioso
            }
        }

        // ========================= Callbacks lobby =========================
        public void OnLobbyUpdated(LobbyInfo lobbyInfo) { }
        public void OnPlayerJoined(PlayerSummary playerSummary) { }
        public void OnPlayerLeft(Guid playerId) { }
        public void OnChatMessageReceived(ChatMessage chatMessage) { }

        // ========================= Cleanup =========================
        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
            try { presenceTimer?.Stop(); } catch { }
            try { friendsRefreshTimer?.Stop(); } catch { }

            try
            {
                if (lobbyServiceClient.State == CommunicationState.Faulted)
                    lobbyServiceClient.Abort();
                else
                    lobbyServiceClient.Close();
            }
            catch
            {
                lobbyServiceClient.Abort();
            }

            try
            {
                if (friendsClient.State == CommunicationState.Faulted)
                    friendsClient.Abort();
                else
                    friendsClient.Close();
            }
            catch
            {
                try { friendsClient.Abort(); } catch { }
            }
        }
    }

    // ========================= ViewModel simple del drawer =========================
    public class FriendItem
    {
        public string DisplayName { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string Presence { get; set; } = "Online";
        public ImageSource Avatar { get; set; }
    }
}
