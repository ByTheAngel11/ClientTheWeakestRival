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

        // ====== LOBBY + CHAT ======
        private Guid? currentLobbyId = null;
        private string currentAccessCode = null;

        private readonly ObservableCollection<ChatLine> chatLines = new ObservableCollection<ChatLine>();
        private sealed class ChatLine
        {
            public string Author { get; set; }
            public string Text { get; set; }
            public string Time { get; set; }
            public ChatLine() { Author = ""; Text = ""; Time = ""; }
        }

        public LobbyWindow()
        {
            InitializeComponent();

            // WCF: lobby (dúplex) y amigos
            var serviceContext = new InstanceContext(this);
            lobbyServiceClient = new LobbyServiceClient(serviceContext, "WSDualHttpBinding_ILobbyService");
            friendsClient = new FriendServiceClient("WSHttpBinding_IFriendService");

            // Chat binding
            lstChatMessages.ItemsSource = chatLines;
            txtChatInput.Text = string.Empty;

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
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) => OnCloseOverlayClick(sender, e);
        private void Window_KeyDown(object sender, KeyEventArgs e) => OnWindowKeyDown(sender, e);
        private void btnSettings_Click(object sender, RoutedEventArgs e) { /* TODO */ }

        private void OnProfileClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            var profilePage = new ModifyProfilePage(lobbyServiceClient, token) { Title = "Perfil" };
            profilePage.Closed += delegate { RefreshProfileButtonAvatar(); };
            ShowOverlayPage(profilePage);
        }

        // ======== Overlay central ========
        private void OnCloseOverlayClick(object sender, RoutedEventArgs e) => HideOverlayPanel();

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            if (friendsDrawerHost.Visibility == Visibility.Visible)
            {
                CloseFriendsDrawer();
                return;
            }

            if (pnlOverlayHost.Visibility == Visibility.Visible)
                HideOverlayPanel();
        }

        private void RefreshProfileButtonAvatar()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
                if (string.IsNullOrWhiteSpace(token)) return;

                var me = lobbyServiceClient.GetMyProfile(token);

                int px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch { }

                ImageSource src =
                    UiImageHelper.TryCreateFromUrlOrPath(me.ProfileImageUrl, decodePixelWidth: px)
                    ?? UiImageHelper.DefaultAvatar(px);

                var brushObj = FindName("brushAvatar") as ImageBrush;
                if (brushObj != null) brushObj.ImageSource = src;

                var imgObj = FindName("imgAvatar") as Image;
                if (imgObj != null) imgObj.Source = src;
            }
            catch
            {
                int px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch { }
                var fallback = UiImageHelper.DefaultAvatar(px);

                var brushObj = FindName("brushAvatar") as ImageBrush;
                if (brushObj != null) brushObj.ImageSource = fallback;

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

            var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs), EasingFunction = new QuadraticEase() };
            pnlOverlayHost.BeginAnimation(OpacityProperty, fadeIn);

            var blurIn = new DoubleAnimation { From = 0, To = OverlayBlurMaxRadius, Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs), EasingFunction = new QuadraticEase() };
            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty, blurIn);
        }

        private void HideOverlayPanel()
        {
            var fadeOut = new DoubleAnimation { From = pnlOverlayHost.Opacity, To = 0, Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs), EasingFunction = new QuadraticEase() };
            fadeOut.Completed += delegate
            {
                pnlOverlayHost.Visibility = Visibility.Collapsed;
                frmOverlayFrame.Content = null;
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
            if (friendsDrawerHost.Visibility == Visibility.Visible) return;

            grdMainArea.Effect = overlayBlurEffect;
            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlurEffect.Radius, 3.0, TimeSpan.FromMilliseconds(DrawerAnimInMs)) { EasingFunction = new QuadraticEase() });

            await RefreshFriendsAsync();

            friendsDrawerTT.X = 340;
            friendsDrawerHost.Opacity = 0;
            friendsDrawerHost.Visibility = Visibility.Visible;

            var sb = FindResource("sbOpenFriendsDrawer") as Storyboard;
            if (sb != null) sb.Begin(this, true);
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility != Visibility.Visible) return;

            var sb = FindResource("sbCloseFriendsDrawer") as Storyboard;
            if (sb != null)
            {
                sb.Completed += delegate
                {
                    friendsDrawerHost.Visibility = Visibility.Collapsed;
                    if (pnlOverlayHost.Visibility != Visibility.Visible)
                        grdMainArea.Effect = null;
                };
                sb.Begin(this, true);
            }

            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlurEffect.Radius, 0.0, TimeSpan.FromMilliseconds(DrawerAnimOutMs)) { EasingFunction = new QuadraticEase() });
        }

        private void btnSendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new WPFTheWeakestRival.Pages.AddFriendPage(friendsClient, token);
            page.FriendsUpdated += async delegate { await RefreshFriendsAsync(); };
            ShowOverlayPage(page);
        }

        private void btnViewRequests_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            var page = new WPFTheWeakestRival.Pages.FriendRequestsPage(friendsClient, token);
            page.FriendsChanged += async delegate { await RefreshFriendsAsync(); };
            ShowOverlayPage(page);
        }

        public void SetFriends(IEnumerable<FriendItem> list, int pendingRequestCount)
        {
            friends.Clear();
            if (list != null) foreach (var f in list) friends.Add(f);
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
            var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
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

                if (resp == null || resp.Lobby == null)
                {
                    MessageBox.Show("No se pudo crear el lobby.");
                    return;
                }

                pendingRequestsCount = Math.Max(0, (response.PendingIncoming?.Length ?? 0));

                UpdateLobbyHeader(resp.Lobby.LobbyName, currentLobbyId, currentAccessCode);
                AppendSystemLine("Lobby creado.");
            }
            catch (FaultException<LobbyService.ServiceFault> fx)
            {
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Amigos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con LobbyService.\n" + cx.Message, "Lobby",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al crear lobby: " + ex.Message, "Lobby",
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
                MessageBox.Show("Error al unirse: " + ex.Message, "Lobby",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void UpdateLobbyHeader(string lobbyName, Guid? lobbyId, string accessCode)
        {
            txtLobbyHeader.Text = string.IsNullOrWhiteSpace(lobbyName) ? "Lobby" : lobbyName;
            txtLobbyId.Text = lobbyId.HasValue ? "LobbyId: " + lobbyId.Value.ToString() : "";
            txtAccessCode.Text = string.IsNullOrWhiteSpace(accessCode) ? "" : "Código: " + accessCode;
        }

        private async void txtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var text = (txtChatInput.Text ?? string.Empty).Trim();
            if (text.Length == 0) return;

            if (!currentLobbyId.HasValue)
            {
                MessageBox.Show("Primero crea o únete a un lobby.");
                return;
            }

            var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            try
            {
                var lobbyId = currentLobbyId.Value;

                await Task.Run(() =>
                {
                    var req = new SendLobbyMessageRequest
                    {
                        Token = token,
                        LobbyId = lobbyId,
                        Message = text
                    };
                    lobbyServiceClient.SendChatMessage(req);
                });
                txtChatInput.Text = string.Empty;
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo enviar el mensaje.\n" + cx.Message, "Chat",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al enviar: " + ex.Message, "Chat",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AppendLine(string author, string text)
        {
            chatLines.Add(new ChatLine
            {
                Author = string.IsNullOrWhiteSpace(author) ? "?" : author,
                Text = text ?? string.Empty,
                Time = DateTime.Now.ToShortTimeString()
            });

            if (lstChatMessages.Items.Count > 0)
                lstChatMessages.ScrollIntoView(lstChatMessages.Items[lstChatMessages.Items.Count - 1]);
        }

        private void AppendSystemLine(string text) => AppendLine("Sistema", text);

        // ====== Callbacks (duplex) ======
        public void OnLobbyUpdated(LobbyInfo lobbyInfo)
        {
            if (lobbyInfo == null) return;
            if (currentLobbyId.HasValue && lobbyInfo.LobbyId != currentLobbyId.Value) return;

            Dispatcher.Invoke(delegate
            {
                // Si el server envía el AccessCode en el update, úsalo
                if (!string.IsNullOrWhiteSpace(lobbyInfo.AccessCode))
                    currentAccessCode = lobbyInfo.AccessCode;

                UpdateLobbyHeader(lobbyInfo.LobbyName, lobbyInfo.LobbyId, currentAccessCode);
            });
        }

        public void OnPlayerJoined(PlayerSummary playerSummary)
        {
            Dispatcher.Invoke(delegate { AppendSystemLine("Un jugador se unió al lobby."); });
        }

        public void OnPlayerLeft(Guid playerId)
        {
            Dispatcher.Invoke(delegate { AppendSystemLine("Un jugador salió del lobby."); });
        }

        public void OnLobbyUpdated(LobbyInfo lobbyInfo) { }
        public void OnPlayerJoined(PlayerSummary playerSummary) { }
        public void OnPlayerLeft(Guid playerId) { }
        public void OnChatMessageReceived(ChatMessage chatMessage) { }

        // ========================= Cleanup =========================
        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
            try { presenceTimer?.Stop(); } catch { }
            try { friendsRefreshTimer?.Stop(); } catch { }

        {
            // Salir del lobby en server para limpiar callback
            try
            {
                if (lobbyServiceClient.State == CommunicationState.Faulted)
                    lobbyServiceClient.Abort();
                else
                    lobbyServiceClient.Close();
            }
            catch
            {
                if (lobbyServiceClient.State == CommunicationState.Faulted) lobbyServiceClient.Abort();
                else lobbyServiceClient.Close();
            }
            catch { lobbyServiceClient.Abort(); }

            try
            {
                if (friendsClient.State == CommunicationState.Faulted) friendsClient.Abort();
                else friendsClient.Close();
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
        public string DisplayName { get; set; }
        public string StatusText { get; set; }
        public string Presence { get; set; }
        public ImageSource Avatar { get; set; }

        public FriendItem()
        {
            DisplayName = "";
            StatusText = "";
            Presence = "Online";
        }
    }
}
