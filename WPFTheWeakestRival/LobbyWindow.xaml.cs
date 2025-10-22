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
using WPFTheWeakestRival.LobbyService;    // Lobby (duplex)
using WPFTheWeakestRival.FriendService;  // Friends (no duplex)
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    /// <summary>
    /// Ventana principal del lobby. Hospeda overlays, drawer de amigos y callbacks de lobby/chat.
    /// </summary>
    public partial class LobbyWindow : Window, ILobbyServiceCallback
    {
        // ===== Animaciones / UI =====
        private const double OverlayBlurMaxRadius = 6.0;
        private const int OverlayFadeInDurationMs = 180;
        private const int OverlayFadeOutDurationMs = 160;
        private const int DrawerAnimInMs = 180;
        private const int DrawerAnimOutMs = 160;

        // ===== Presencia / refrescos =====
        private const int HeartbeatIntervalSeconds = 30;
        private const int FriendsRefreshIntervalSeconds = 45;

        private readonly LobbyServiceClient _lobby;     // duplex
        private readonly FriendServiceClient _friends;   // no duplex
        private readonly BlurEffect _overlayBlur = new BlurEffect { Radius = 0 };

        private readonly DispatcherTimer _presenceTimer;
        private readonly DispatcherTimer _friendsTimer;

        // ===== Amigos (drawer) =====
        private readonly ObservableCollection<FriendItem> _friendsItems = new ObservableCollection<FriendItem>();
        private int _pendingRequestsCount = 0;

        // ===== Lobby + Chat =====
        private Guid? _currentLobbyId = null;
        private string _currentAccessCode = null;

        private readonly ObservableCollection<ChatLine> _chatLines = new ObservableCollection<ChatLine>();
        private sealed class ChatLine
        {
            public string Author { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public string Time { get; set; } = string.Empty;
        }

        public LobbyWindow()
        {
            InitializeComponent();

            // WCF
            var ctx = new InstanceContext(this);
            _lobby = new LobbyServiceClient(ctx, "WSDualHttpBinding_ILobbyService");
            _friends = new FriendServiceClient("WSHttpBinding_IFriendService");

            // Chat
            lstChatMessages.ItemsSource = _chatLines;
            txtChatInput.Text = string.Empty;

            // Amigos
            lstFriends.ItemsSource = _friendsItems;
            UpdateFriendDrawerUI();

            // Avatar
            RefreshProfileButtonAvatar();

            // Timers
            _presenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HeartbeatIntervalSeconds) };
            _presenceTimer.Tick += async (_, __) => await SendHeartbeatAsync();
            _presenceTimer.Start();
            _ = SendHeartbeatAsync(); // primer latido

            _friendsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(FriendsRefreshIntervalSeconds) };
            _friendsTimer.Tick += async (_, __) =>
            {
                if (friendsDrawerHost.Visibility == Visibility.Visible)
                    await RefreshFriendsAsync();
            };
            _friendsTimer.Start();

            Unloaded += OnWindowUnloaded;
        }

        // ========================= Top bar / Perfil =========================
        private void btnModifyProfile_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new ModifyProfilePage(_lobby, token) { Title = "Perfil" };
            page.Closed += (_, __) => RefreshProfileButtonAvatar();
            ShowOverlayPage(page);
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Settings
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
                HideOverlayPanel();
        }

        // ========================= Overlay central =========================
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) => HideOverlayPanel();

        private void ShowOverlayPage(Page page)
        {
            frmOverlayFrame.Content = page;
            grdMainArea.Effect = _overlayBlur;
            pnlOverlayHost.Visibility = Visibility.Visible;

            pnlOverlayHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(OverlayFadeInDurationMs))
                { EasingFunction = new QuadraticEase() });

            _overlayBlur.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(0, OverlayBlurMaxRadius, TimeSpan.FromMilliseconds(OverlayFadeInDurationMs))
                { EasingFunction = new QuadraticEase() });
        }

        private void HideOverlayPanel()
        {
            var fadeOut = new DoubleAnimation(pnlOverlayHost.Opacity, 0, TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs))
            {
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

            if (grdMainArea.Effect is BlurEffect currentBlur)
            {
                currentBlur.BeginAnimation(BlurEffect.RadiusProperty,
                    new DoubleAnimation(currentBlur.Radius, 0, TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs))
                    { EasingFunction = new QuadraticEase() });
            }
        }

        private void RefreshProfileButtonAvatar()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token)) return;

                var me = _lobby.GetMyProfile(token);

                int px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch { }

                var src = UiImageHelper.TryCreateFromUrlOrPath(me.ProfileImageUrl, decodePixelWidth: px)
                          ?? UiImageHelper.DefaultAvatar(px);

                if (FindName("brushAvatar") is ImageBrush brush) brush.ImageSource = src;
                if (FindName("imgAvatar") is Image img) img.Source = src;
            }
            catch
            {
                int px = 28;
                try { px = (int)(double)FindResource("AvatarSize"); } catch { }
                var fallback = UiImageHelper.DefaultAvatar(px);

                if (FindName("brushAvatar") is ImageBrush brush) brush.ImageSource = fallback;
                if (FindName("imgProfile") is Image legacyImg) legacyImg.Source = fallback;
            }
        }

        // ========================= Drawer de Amigos =========================
        private void btnFriends_Click(object sender, RoutedEventArgs e) => OpenFriendsDrawer();
        private void btnCloseFriends_Click(object sender, RoutedEventArgs e) => CloseFriendsDrawer();
        private void friendsDimmer_MouseDown(object sender, MouseButtonEventArgs e) => CloseFriendsDrawer();

        private async void OpenFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible) return;

            grdMainArea.Effect = _overlayBlur;
            _overlayBlur.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(_overlayBlur.Radius, 3.0, TimeSpan.FromMilliseconds(DrawerAnimInMs))
                { EasingFunction = new QuadraticEase() });

            await RefreshFriendsAsync();

            friendsDrawerTT.X = 340;
            friendsDrawerHost.Opacity = 0;
            friendsDrawerHost.Visibility = Visibility.Visible;

            if (FindResource("sbOpenFriendsDrawer") is Storyboard sb) sb.Begin(this, true);
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility != Visibility.Visible) return;

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

            _overlayBlur.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(_overlayBlur.Radius, 0.0, TimeSpan.FromMilliseconds(DrawerAnimOutMs))
                { EasingFunction = new QuadraticEase() });
        }

        public void SetFriends(IEnumerable<FriendItem> list, int pendingRequestCount)
        {
            _friendsItems.Clear();
            if (list != null)
                foreach (var f in list) _friendsItems.Add(f);

            _pendingRequestsCount = Math.Max(0, pendingRequestCount);
            UpdateFriendDrawerUI();
        }

        private void UpdateFriendDrawerUI()
        {
            friendsEmptyPanel.Visibility = _friendsItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            lstFriends.Visibility = _friendsItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            txtRequestsCount.Text = _pendingRequestsCount.ToString();
        }

        private async Task RefreshFriendsAsync()
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
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
                    return _friends.ListFriends(req);
                });

                _friendsItems.Clear();

                foreach (var f in (response.Friends ?? Array.Empty<FriendSummary>()))
                {
                    var name = string.IsNullOrWhiteSpace(f.DisplayName) ? f.Username : f.DisplayName;
                    var img = UiImageHelper.TryCreateFromUrlOrPath(f.AvatarUrl, decodePixelWidth: 36)
                              ?? UiImageHelper.DefaultAvatar(36);

                    _friendsItems.Add(new FriendItem
                    {
                        DisplayName = name ?? string.Empty,
                        StatusText = f.IsOnline ? "Disponible" : "Desconectado",
                        Presence = f.IsOnline ? "Online" : "Offline",
                        Avatar = img,
                        IsOnline = f.IsOnline
                    });
                }

                _pendingRequestsCount = Math.Max(0, response.PendingIncoming?.Length ?? 0);
                UpdateFriendDrawerUI();
            }
            catch (FaultException<FriendService.ServiceFault> fx)
            {
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Amigos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con FriendService.\n" + cx.Message, "Amigos",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

        // === Handlers del drawer (restaurados para XAML) ===
        private void btnSendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new WPFTheWeakestRival.Pages.AddFriendPage(_friends, token);
            page.FriendsUpdated += async (_, __) => await RefreshFriendsAsync();
            ShowOverlayPage(page);
        }

        private void btnViewRequests_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new WPFTheWeakestRival.Pages.FriendRequestsPage(_friends, token);
            page.FriendsChanged += async (_, __) => await RefreshFriendsAsync();
            ShowOverlayPage(page);
        }

        // ========================= Lobby + Chat =========================
        private async void btnCreateLobby_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var resp = await Task.Run(() =>
                {
                    var req = new CreateLobbyRequest
                    {
                        Token = token,
                        LobbyName = "Lobby",
                        MaxPlayers = 8
                    };
                    return _lobby.CreateLobby(req);
                });

                if (resp?.Lobby == null)
                {
                    MessageBox.Show("No se pudo crear el lobby.");
                    return;
                }

                _currentLobbyId = resp.Lobby.LobbyId;
                _currentAccessCode = resp.Lobby.AccessCode;

                UpdateLobbyHeader(resp.Lobby.LobbyName, _currentLobbyId, _currentAccessCode);
                AppendSystemLine("Lobby creado.");
            }
            catch (FaultException<LobbyService.ServiceFault> fx)
            {
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Lobby",
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
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void btnJoinByCode_Click(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var code = (txtJoinCode.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                MessageBox.Show("Ingresa un código de acceso.");
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var resp = await Task.Run(() =>
                {
                    var req = new JoinByCodeRequest { Token = token, AccessCode = code };
                    return _lobby.JoinByCode(req);
                });

                if (resp?.Lobby == null)
                {
                    MessageBox.Show("No se pudo unir al lobby.");
                    return;
                }

                _currentLobbyId = resp.Lobby.LobbyId;
                _currentAccessCode = string.IsNullOrWhiteSpace(resp.Lobby.AccessCode) ? code : resp.Lobby.AccessCode;

                UpdateLobbyHeader(resp.Lobby.LobbyName, _currentLobbyId, _currentAccessCode);
                AppendSystemLine("Te uniste al lobby.");
            }
            catch (FaultException<LobbyService.ServiceFault> fx)
            {
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Lobby",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con LobbyService.\n" + cx.Message, "Lobby",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
            txtLobbyId.Text = lobbyId.HasValue ? "LobbyId: " + lobbyId.Value : string.Empty;
            txtAccessCode.Text = string.IsNullOrWhiteSpace(accessCode) ? string.Empty : "Código: " + accessCode;
        }

        private async void txtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var text = (txtChatInput.Text ?? string.Empty).Trim();
            if (text.Length == 0) return;

            if (!_currentLobbyId.HasValue)
            {
                MessageBox.Show("Primero crea o únete a un lobby.");
                return;
            }

            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            try
            {
                var lobbyId = _currentLobbyId.Value;

                await Task.Run(() =>
                {
                    var req = new SendLobbyMessageRequest
                    {
                        Token = token,
                        LobbyId = lobbyId,
                        Message = text
                    };
                    _lobby.SendChatMessage(req);
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
            _chatLines.Add(new ChatLine
            {
                Author = string.IsNullOrWhiteSpace(author) ? "?" : author,
                Text = text ?? string.Empty,
                Time = DateTime.Now.ToString("HH:mm")
            });

            if (lstChatMessages.Items.Count > 0)
                lstChatMessages.ScrollIntoView(lstChatMessages.Items[lstChatMessages.Items.Count - 1]);
        }

        private void AppendSystemLine(string text) => AppendLine("Sistema", text);

        // ========================= Callbacks (duplex) =========================
        public void OnLobbyUpdated(LobbyInfo lobbyInfo)
        {
            if (lobbyInfo == null) return;
            if (_currentLobbyId.HasValue && lobbyInfo.LobbyId != _currentLobbyId.Value) return;

            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrWhiteSpace(lobbyInfo.AccessCode))
                    _currentAccessCode = lobbyInfo.AccessCode;

                UpdateLobbyHeader(lobbyInfo.LobbyName, lobbyInfo.LobbyId, _currentAccessCode);
            });
        }

        // PlayerSummary no expone DisplayName/Username en tus contratos actuales
        public void OnPlayerJoined(PlayerSummary playerSummary)
        {
            Dispatcher.Invoke(() => AppendSystemLine("Un jugador se unió al lobby."));
        }

        public void OnPlayerLeft(Guid playerId)
        {
            Dispatcher.Invoke(() => AppendSystemLine("Un jugador salió del lobby."));
        }

        public void OnChatMessageReceived(ChatMessage chatMessage)
        {
            if (chatMessage == null) return;
            Dispatcher.Invoke(() =>
            {
                var who = string.IsNullOrWhiteSpace(chatMessage.FromPlayerName) ? "Jugador" : chatMessage.FromPlayerName;
                AppendLine(who, chatMessage.Message ?? string.Empty);
            });
        }

        // ========================= Presencia =========================
        private async Task SendHeartbeatAsync()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token)) return;

                await _friends.PresenceHeartbeatAsync(new HeartbeatRequest
                {
                    Token = token,
                    Device = "WPF"
                });
            }
            catch
            {
                // best-effort; se reintenta en el siguiente tick
            }
        }

        // ========================= Cleanup =========================
        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
            try { _presenceTimer?.Stop(); } catch { }
            try { _friendsTimer?.Stop(); } catch { }

            // Intentar abandonar lobby (limpia callback del lado server)
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (!string.IsNullOrWhiteSpace(token) && _currentLobbyId.HasValue)
                {
                    try { _lobby.LeaveLobby(new LeaveLobbyRequest { Token = token, LobbyId = _currentLobbyId.Value }); }
                    catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }

            // Cerrar clientes WCF
            try
            {
                if (_lobby.State == CommunicationState.Faulted) _lobby.Abort();
                else _lobby.Close();
            }
            catch { try { _lobby.Abort(); } catch { } }

            try
            {
                if (_friends.State == CommunicationState.Faulted) _friends.Abort();
                else _friends.Close();
            }
            catch { try { _friends.Abort(); } catch { } }
        }
    }

    // ========================= ViewModel para el drawer =========================
    public class FriendItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string Presence { get; set; } = "Offline";
        public ImageSource Avatar { get; set; }
        public bool IsOnline { get; set; } = false;
    }
}
