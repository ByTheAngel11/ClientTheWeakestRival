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

using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.LobbyService;       // Lobby (duplex)
using WPFTheWeakestRival.FriendService;     // FriendService (wsHttp, no duplex)

namespace WPFTheWeakestRival
{
    /// <summary>
    /// Main lobby window. Hosts overlay pages and handles lobby callbacks.
    /// </summary>
    public partial class LobbyWindow : Window, ILobbyServiceCallback
    {
        private const double OverlayBlurMaxRadius = 6.0;
        private const int OverlayFadeInDurationMs = 180;
        private const int OverlayFadeOutDurationMs = 160;

        // Drawer de amigos
        private const int DrawerAnimInMs = 180;
        private const int DrawerAnimOutMs = 160;

        private readonly LobbyServiceClient lobbyServiceClient;     // duplex
        private readonly FriendServiceClient friendsClient;          // simple
        private readonly BlurEffect overlayBlurEffect = new BlurEffect { Radius = 0 };

        // Datos visuales del drawer
        private readonly ObservableCollection<FriendItem> friends = new ObservableCollection<FriendItem>();
        private int pendingRequestsCount = 0;

        public LobbyWindow()
        {
            InitializeComponent();

            // Lobby (duplex)
            var serviceContext = new InstanceContext(this);
            lobbyServiceClient = new LobbyServiceClient(serviceContext, "WSDualHttpBinding_ILobbyService");

            // Friends (no duplex)
            friendsClient = new FriendServiceClient("WSHttpBinding_IFriendService");

            RefreshProfileButtonAvatar();

            // Estado inicial (vacío)
            UpdateFriendDrawerUI();

            Unloaded += OnWindowUnloaded;
        }

        // ======== Eventos de UI existentes ========
        private void btnModifyProfile_Click(object sender, RoutedEventArgs e) => OnProfileClick(sender, e);
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) => OnCloseOverlayClick(sender, e);
        private void Window_KeyDown(object sender, KeyEventArgs e) => OnWindowKeyDown(sender, e);
        private void btnSettings_Click(object sender, RoutedEventArgs e) { /* TODO: abrir settings */ }

        // ======== Top bar: Perfil ========
        private void OnProfileClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            var profilePage = new ModifyProfilePage(lobbyServiceClient, token)
            {
                Title = "Perfil"
            };

            // Cuando se cierre (guardar o cancelar), vuelve a cargar el avatar
            profilePage.Closed += (_, __) => RefreshProfileButtonAvatar();

            ShowOverlayPage(profilePage);
        }

        // ======== Overlay central ========
        private void OnCloseOverlayClick(object sender, RoutedEventArgs e)
        {
            HideOverlayPanel();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
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

        // ================= FIX: pintar el nuevo botón-avatar =================
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

                if (FindName("brushAvatar") is ImageBrush brush)
                    brush.ImageSource = src;

                if (FindName("imgAvatar") is Image img)
                    img.Source = src;
            }
            catch
            {
                int px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch { /* ignore */ }
                var fallback = UiImageHelper.DefaultAvatar(px);

                if (FindName("brushAvatar") is ImageBrush brush)
                    brush.ImageSource = fallback;

                if (FindName("imgProfile") is Image legacyImg)
                    legacyImg.Source = fallback;
            }
        }

        private void ShowOverlayPage(Page page)
        {
            frmOverlayFrame.Content = page;
            grdMainArea.Effect = overlayBlurEffect;
            pnlOverlayHost.Visibility = Visibility.Visible;

            var overlayFadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs),
                EasingFunction = new QuadraticEase()
            };
            pnlOverlayHost.BeginAnimation(OpacityProperty, overlayFadeInAnimation);

            var overlayBlurInAnimation = new DoubleAnimation
            {
                From = 0,
                To = OverlayBlurMaxRadius,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs),
                EasingFunction = new QuadraticEase()
            };
            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty, overlayBlurInAnimation);
        }

        private void HideOverlayPanel()
        {
            var overlayFadeOutAnimation = new DoubleAnimation
            {
                From = pnlOverlayHost.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs),
                EasingFunction = new QuadraticEase()
            };

            overlayFadeOutAnimation.Completed += (_, __) =>
            {
                pnlOverlayHost.Visibility = Visibility.Collapsed;
                frmOverlayFrame.Content = null;

                // Mantén blur si el drawer de amigos sigue abierto
                if (friendsDrawerHost.Visibility != Visibility.Visible)
                    grdMainArea.Effect = null;
            };

            pnlOverlayHost.BeginAnimation(OpacityProperty, overlayFadeOutAnimation);

            if (grdMainArea.Effect is BlurEffect currentBlurEffect)
            {
                var overlayBlurOutAnimation = new DoubleAnimation
                {
                    From = currentBlurEffect.Radius,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs),
                    EasingFunction = new QuadraticEase()
                };
                currentBlurEffect.BeginAnimation(BlurEffect.RadiusProperty, overlayBlurOutAnimation);
            }
        }

        // ======== Drawer de Amigos ========
        private void btnFriends_Click(object sender, RoutedEventArgs e) => OpenFriendsDrawer();
        private void btnCloseFriends_Click(object sender, RoutedEventArgs e) => CloseFriendsDrawer();
        private void friendsDimmer_MouseDown(object sender, MouseButtonEventArgs e) => CloseFriendsDrawer();

        private async void OpenFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible)
                return;

            // Pequeño blur al contenido principal
            grdMainArea.Effect = overlayBlurEffect;
            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlurEffect.Radius, 3.0, TimeSpan.FromMilliseconds(DrawerAnimInMs))
                { EasingFunction = new QuadraticEase() });

            await RefreshFriendsAsync();

            friendsDrawerTT.X = 340;
            friendsDrawerHost.Opacity = 0;
            friendsDrawerHost.Visibility = Visibility.Visible;

            if (FindResource("sbOpenFriendsDrawer") is Storyboard sb)
                sb.Begin(this, true);
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility != Visibility.Visible)
                return;

            if (FindResource("sbCloseFriendsDrawer") is Storyboard sb)
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
                MessageBox.Show("Sesión no válida.");
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

            // Badge
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

                var response = await Task.Run(() =>
                {
                    var req = new ListFriendsRequest
                    {
                        Token = token,
                        IncludePendingIncoming = true,
                        IncludePendingOutgoing = false
                    };
                    return friendsClient.ListFriends(req);
                });

                friends.Clear();

                var items = (response.Friends ?? Array.Empty<FriendSummary>())
                    .Select(f =>
                    {
                        var name = string.IsNullOrWhiteSpace(f.DisplayName) ? f.Username : f.DisplayName;
                        var img = UiImageHelper.TryCreateFromUrlOrPath(f.AvatarUrl, decodePixelWidth: 36)
                                  ?? UiImageHelper.DefaultAvatar(36);

                        return new FriendItem
                        {
                            DisplayName = name ?? string.Empty,
                            StatusText = f.IsOnline ? "Disponible" : "Desconectado",
                            Presence = f.IsOnline ? "Online" : "Offline",
                            Avatar = img
                        };
                    });

                foreach (var it in items) friends.Add(it);

                pendingRequestsCount = Math.Max(0, (response.PendingIncoming?.Length ?? 0));

                UpdateFriendDrawerUI();
            }
            catch (FaultException<LobbyService.ServiceFault> fx)
            {
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Amigos",
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

        public void OnLobbyUpdated(LobbyInfo lobbyInfo) { }
        public void OnPlayerJoined(PlayerSummary playerSummary) { }
        public void OnPlayerLeft(Guid playerId) { }
        public void OnChatMessageReceived(ChatMessage chatMessage) { }

        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
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
                try { friendsClient.Abort(); } catch { /* ignore */ }
            }
        }
    }

    public class FriendItem
    {
        public string DisplayName { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string Presence { get; set; } = "Online";
        public ImageSource Avatar { get; set; }
    }
}
