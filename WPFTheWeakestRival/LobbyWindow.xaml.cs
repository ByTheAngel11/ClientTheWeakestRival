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

            // Lobby (duplex)
            var serviceContext = new InstanceContext(this);
            lobbyServiceClient = new LobbyServiceClient(serviceContext, "WSDualHttpBinding_ILobbyService");

            // Friends (no duplex)
            friendsClient = new FriendServiceClient("WSHttpBinding_IFriendService");

            // Chat binding
            lstChatMessages.ItemsSource = chatLines;
            txtChatInput.Text = string.Empty;

            RefreshProfileButtonAvatar();
            UpdateFriendDrawerUI();

            Unloaded += OnWindowUnloaded;
        }

        // ======== Eventos de UI existentes ========
        private void btnModifyProfile_Click(object sender, RoutedEventArgs e) => OnProfileClick(sender, e);
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) => OnCloseOverlayClick(sender, e);
        private void Window_KeyDown(object sender, KeyEventArgs e) => OnWindowKeyDown(sender, e);
        private void btnSettings_Click(object sender, RoutedEventArgs e) { /* TODO */ }

        // ======== Top bar: Perfil ========
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
            pnlOverlayHost.BeginAnimation(OpacityProperty, fadeOut);

            var currentBlur = grdMainArea.Effect as BlurEffect;
            if (currentBlur != null)
            {
                var blurOut = new DoubleAnimation { From = currentBlur.Radius, To = 0, Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs), EasingFunction = new QuadraticEase() };
                currentBlur.BeginAnimation(BlurEffect.RadiusProperty, blurOut);
            }
        }

        // ======== Drawer de Amigos ========
        private void btnFriends_Click(object sender, RoutedEventArgs e) { OpenFriendsDrawer(); }
        private void btnCloseFriends_Click(object sender, RoutedEventArgs e) { CloseFriendsDrawer(); }
        private void friendsDimmer_MouseDown(object sender, MouseButtonEventArgs e) { CloseFriendsDrawer(); }

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
                MessageBox.Show("Sesión no válida.");
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

                var items = (response.Friends ?? new FriendSummary[0])
                    .Select(f =>
                    {
                        var name = string.IsNullOrWhiteSpace(f.DisplayName) ? f.Username : f.DisplayName;
                        var img = UiImageHelper.TryCreateFromUrlOrPath(f.AvatarUrl, decodePixelWidth: 36)
                                  ?? UiImageHelper.DefaultAvatar(36);

                        var it = new FriendItem();
                        it.DisplayName = name ?? string.Empty;
                        it.StatusText = f.IsOnline ? "Disponible" : "Desconectado";
                        it.Presence = f.IsOnline ? "Online" : "Offline";
                        it.Avatar = img;
                        return it;
                    });

                foreach (var it in items) friends.Add(it);

                pendingRequestsCount = Math.Max(0, (response.PendingIncoming != null ? response.PendingIncoming.Length : 0));

                UpdateFriendDrawerUI();
            }
            catch (FaultException<LobbyService.ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Amigos",
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

        // ===============================
        //       LOBBY + CHAT
        // ===============================

        private async void btnCreateLobby_Click(object sender, RoutedEventArgs e)
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

                var resp = await Task.Run(() =>
                {
                    var req = new CreateLobbyRequest
                    {
                        Token = token,
                        LobbyName = "Lobby",
                        MaxPlayers = 8
                    };
                    return lobbyServiceClient.CreateLobby(req);
                });

                if (resp == null || resp.Lobby == null)
                {
                    MessageBox.Show("No se pudo crear el lobby.");
                    return;
                }

                currentLobbyId = resp.Lobby.LobbyId;
                currentAccessCode = resp.Lobby.AccessCode; // ← propiedad fuerte

                UpdateLobbyHeader(resp.Lobby.LobbyName, currentLobbyId, currentAccessCode);
                AppendSystemLine("Lobby creado.");
            }
            catch (FaultException<LobbyService.ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Lobby",
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
            var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
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
                    return lobbyServiceClient.JoinByCode(req);
                });

                if (resp == null || resp.Lobby == null)
                {
                    MessageBox.Show("No se pudo unir al lobby.");
                    return;
                }

                currentLobbyId = resp.Lobby.LobbyId;
                currentAccessCode = string.IsNullOrWhiteSpace(resp.Lobby.AccessCode)
                    ? code
                    : resp.Lobby.AccessCode;

                UpdateLobbyHeader(resp.Lobby.LobbyName, currentLobbyId, currentAccessCode);
                AppendSystemLine("Te uniste al lobby.");
            }
            catch (FaultException<LobbyService.ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Lobby",
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

        public void OnChatMessageReceived(ChatMessage chatMessage)
        {
            if (chatMessage == null) return;
            Dispatcher.Invoke(delegate
            {
                var who = string.IsNullOrWhiteSpace(chatMessage.FromPlayerName) ? "Jugador" : chatMessage.FromPlayerName;
                AppendLine(who, chatMessage.Message ?? "");
            });
        }

        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
            // Salir del lobby en server para limpiar callback
            try
            {
                var token = LoginWindow.AppSession.CurrentToken != null ? LoginWindow.AppSession.CurrentToken.Token : null;
                if (!string.IsNullOrWhiteSpace(token) && currentLobbyId.HasValue)
                {
                    var req = new LeaveLobbyRequest { Token = token, LobbyId = currentLobbyId.Value };
                    lobbyServiceClient.LeaveLobby(req);
                }
            }
            catch { /* ignore */ }

            try
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
