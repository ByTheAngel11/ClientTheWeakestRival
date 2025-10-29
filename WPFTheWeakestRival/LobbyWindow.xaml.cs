using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class LobbyWindow : Window, ILobbyServiceCallback
    {
        private const double OVERLAY_BLUR_MAX_RADIUS = 6.0;
        private const int OVERLAY_FADE_IN_DURATION_MS = 180;
        private const int OVERLAY_FADE_OUT_DURATION_MS = 160;
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;
        private const int HEARTBEAT_INTERVAL_SECONDS = 30;
        private const int FRIENDS_REFRESH_INTERVAL_SECONDS = 45;

        private readonly LobbyServiceClient lobbyClient;
        private readonly FriendServiceClient friendServiceClient;
        private readonly BlurEffect overlayBlur = new BlurEffect { Radius = 0 };

        private readonly DispatcherTimer presenceTimer;
        private readonly DispatcherTimer friendsTimer;

        private readonly ObservableCollection<FriendItem> friendItems = new ObservableCollection<FriendItem>();
        private int pendingRequestsCount = 0;

        private Guid? currentLobbyId = null;
        private string currentAccessCode = null;

        private readonly ObservableCollection<ChatLine> chatLines = new ObservableCollection<ChatLine>();
        private string myDisplayName = "Yo";

        private sealed class ChatLine
        {
            public string Author { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public string Time { get; set; } = string.Empty;
        }

        public LobbyWindow()
        {
            InitializeComponent();

            var instanceContext = new InstanceContext(this);
            lobbyClient = new LobbyServiceClient(instanceContext, "WSDualHttpBinding_ILobbyService");
            friendServiceClient = new FriendServiceClient("WSHttpBinding_IFriendService");

            lstChatMessages.ItemsSource = chatLines;
            txtChatInput.Text = string.Empty;

            lstFriends.ItemsSource = friendItems;
            UpdateFriendDrawerUi();

            RefreshProfileButtonAvatar();

            presenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HEARTBEAT_INTERVAL_SECONDS) };
            presenceTimer.Tick += async (_, __) => await SendHeartbeatAsync();
            presenceTimer.Start();
            _ = SendHeartbeatAsync();

            friendsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(FRIENDS_REFRESH_INTERVAL_SECONDS) };
            friendsTimer.Tick += async (_, __) =>
            {
                if (friendsDrawerHost.Visibility == Visibility.Visible)
                {
                    await RefreshFriendsAsync();
                }
            };
            friendsTimer.Start();

            Unloaded += OnWindowUnloaded;
        }

        private void BtnModifyProfileClick(object sender, RoutedEventArgs args)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var modifyProfilePage = new ModifyProfilePage(
                lobbyClient,
                new AuthServiceClient("WSHttpBinding_IAuthService"),
                token)
            {
                Title = Lang.profileTitle
            };

            modifyProfilePage.Closed += (_, __) => RefreshProfileButtonAvatar();
            modifyProfilePage.LoggedOut += OnLoggedOut;

            ShowOverlayPage(modifyProfilePage);
        }

        private void OnLoggedOut(object sender, EventArgs args)
        {
            try { LoginWindow.AppSession.CurrentToken = null; } catch { }
            try { HideOverlayPanel(); } catch { }

            var loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        }

        private void BtnSettingsClick(object sender, RoutedEventArgs args)
        {
        }

        private void Window_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Escape)
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
                HideOverlayPanel();
            }
        }

        private void CloseOverlayClick(object sender, RoutedEventArgs args)
        {
            HideOverlayPanel();
        }

        private void ShowOverlayPage(Page page)
        {
            frmOverlayFrame.Content = page;
            grdMainArea.Effect = overlayBlur;
            pnlOverlayHost.Visibility = Visibility.Visible;

            pnlOverlayHost.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(OVERLAY_FADE_IN_DURATION_MS))
                { EasingFunction = new QuadraticEase() });

            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(0, OVERLAY_BLUR_MAX_RADIUS, TimeSpan.FromMilliseconds(OVERLAY_FADE_IN_DURATION_MS))
                { EasingFunction = new QuadraticEase() });
        }

        private void HideOverlayPanel()
        {
            var fadeOut = new DoubleAnimation(pnlOverlayHost.Opacity, 0, TimeSpan.FromMilliseconds(OVERLAY_FADE_OUT_DURATION_MS))
            { EasingFunction = new QuadraticEase() };

            fadeOut.Completed += (_, __) =>
            {
                pnlOverlayHost.Visibility = Visibility.Collapsed;
                frmOverlayFrame.Content = null;
                if (friendsDrawerHost.Visibility != Visibility.Visible)
                {
                    grdMainArea.Effect = null;
                }
            };

            pnlOverlayHost.BeginAnimation(OpacityProperty, fadeOut);

            if (grdMainArea.Effect is BlurEffect currentBlur)
            {
                currentBlur.BeginAnimation(
                    BlurEffect.RadiusProperty,
                    new DoubleAnimation(currentBlur.Radius, 0, TimeSpan.FromMilliseconds(OVERLAY_FADE_OUT_DURATION_MS))
                    { EasingFunction = new QuadraticEase() });
            }
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

                var myProfile = lobbyClient.GetMyProfile(token);

                myDisplayName = string.IsNullOrWhiteSpace(myProfile?.DisplayName) ? "Yo" : myProfile.DisplayName;

                int avatarSizePx = 28;
                try { avatarSizePx = (int)(double)FindResource("AvatarSize"); } catch { }

                var avatarSource = UiImageHelper.TryCreateFromUrlOrPath(myProfile.ProfileImageUrl)
                                   ?? UiImageHelper.DefaultAvatar(avatarSizePx);

                if (FindName("brushAvatar") is ImageBrush brushAvatar)
                {
                    brushAvatar.ImageSource = avatarSource;
                }

                if (FindName("imgAvatar") is Image imgAvatar)
                {
                    imgAvatar.Source = avatarSource;
                }
            }
            catch
            {
                int avatarSizePx = 28;
                try { avatarSizePx = (int)(double)FindResource("AvatarSize"); } catch { }
                var fallback = UiImageHelper.DefaultAvatar(avatarSizePx);

                if (FindName("brushAvatar") is ImageBrush brushAvatar)
                {
                    brushAvatar.ImageSource = fallback;
                }

                if (FindName("imgProfile") is Image legacyImg)
                {
                    legacyImg.Source = fallback;
                }
            }
        }

        private void BtnFriendsClick(object sender, RoutedEventArgs args)
        {
            OpenFriendsDrawer();
        }

        private void BtnCloseFriendsClick(object sender, RoutedEventArgs args)
        {
            CloseFriendsDrawer();
        }

        private void FriendsDimmerMouseDown(object sender, MouseButtonEventArgs args)
        {
            CloseFriendsDrawer();
        }

        private async void OpenFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible)
            {
                return;
            }

            grdMainArea.Effect = overlayBlur;
            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlur.Radius, 3.0, TimeSpan.FromMilliseconds(DRAWER_ANIM_IN_MS))
                { EasingFunction = new QuadraticEase() });

            await RefreshFriendsAsync();

            friendsDrawerTT.X = 340;
            friendsDrawerHost.Opacity = 0;
            friendsDrawerHost.Visibility = Visibility.Visible;

            if (FindResource("sbOpenFriendsDrawer") is Storyboard storyboard)
            {
                storyboard.Begin(this, true);
            }
        }

        private void CloseFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility != Visibility.Visible)
            {
                return;
            }

            if (FindResource("sbCloseFriendsDrawer") is Storyboard storyboard)
            {
                storyboard.Completed += (_, __) =>
                {
                    friendsDrawerHost.Visibility = Visibility.Collapsed;
                    if (pnlOverlayHost.Visibility != Visibility.Visible)
                    {
                        grdMainArea.Effect = null;
                    }
                };
                storyboard.Begin(this, true);
            }

            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlur.Radius, 0.0, TimeSpan.FromMilliseconds(DRAWER_ANIM_OUT_MS))
                { EasingFunction = new QuadraticEase() });
        }

        public void SetFriends(IEnumerable<FriendItem> friends, int pendingIncomingCount)
        {
            friendItems.Clear();
            if (friends != null)
            {
                foreach (var friend in friends)
                {
                    friendItems.Add(friend);
                }
            }

            pendingRequestsCount = Math.Max(0, pendingIncomingCount);
            UpdateFriendDrawerUi();
        }

        private void UpdateFriendDrawerUi()
        {
            friendsEmptyPanel.Visibility = friendItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            lstFriends.Visibility = friendItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            txtRequestsCount.Text = pendingRequestsCount.ToString();
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
                    var request = new ListFriendsRequest
                    {
                        Token = token,
                        IncludePendingIncoming = true,
                        IncludePendingOutgoing = false
                    };
                    return friendServiceClient.ListFriends(request);
                });

                friendItems.Clear();

                foreach (var friend in (response.Friends ?? Array.Empty<FriendSummary>()))
                {
                    var displayName = string.IsNullOrWhiteSpace(friend.DisplayName) ? friend.Username : friend.DisplayName;
                    var avatarImage = UiImageHelper.TryCreateFromUrlOrPath(friend.AvatarUrl)
                                      ?? UiImageHelper.DefaultAvatar(36);

                    friendItems.Add(new FriendItem
                    {
                        DisplayName = displayName ?? string.Empty,
                        StatusText = friend.IsOnline ? Lang.statusAvailable : Lang.statusOffline,
                        Presence = friend.IsOnline ? "Online" : "Offline",
                        Avatar = avatarImage,
                        IsOnline = friend.IsOnline
                    });
                }

                pendingRequestsCount = Math.Max(0, response.PendingIncoming?.Length ?? 0);
                UpdateFriendDrawerUi();
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                MessageBox.Show($"{ex.Detail.Code}: {ex.Detail.Message}", Lang.btnFriends,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(Lang.noConnection + Environment.NewLine + ex.Message, Lang.btnFriends,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.errorLoadFriends + " " + ex.Message, Lang.btnFriends,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void BtnSendFriendRequestClick(object sender, RoutedEventArgs args)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var addFriendPage = new Pages.AddFriendPage(friendServiceClient, token);
            addFriendPage.FriendsUpdated += async (_, __) => await RefreshFriendsAsync();
            ShowOverlayPage(addFriendPage);
        }

        private void BtnViewRequestsClick(object sender, RoutedEventArgs args)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var friendRequestsPage = new Pages.FriendRequestsPage(friendServiceClient, token);
            friendRequestsPage.FriendsChanged += async (_, __) => await RefreshFriendsAsync();
            ShowOverlayPage(friendRequestsPage);
        }

        private async void BtnCreateLobbyClick(object sender, RoutedEventArgs args)
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
                    var request = new CreateLobbyRequest
                    {
                        Token = token,
                        LobbyName = Lang.lobbyTitle,
                        MaxPlayers = 8
                    };
                    return lobbyClient.CreateLobby(request);
                });

                if (response?.Lobby == null)
                {
                    MessageBox.Show(Lang.lobbyCreateFailed);
                    return;
                }

                currentLobbyId = response.Lobby.LobbyId;
                currentAccessCode = response.Lobby.AccessCode;

                UpdateLobbyHeader(response.Lobby.LobbyName, currentLobbyId, currentAccessCode);
                AppendSystemLine(Lang.lobbyCreated);
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                MessageBox.Show($"{ex.Detail.Code}: {ex.Detail.Message}", Lang.lobbyTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(Lang.noConnection + Environment.NewLine + ex.Message, Lang.lobbyTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.lobbyCreateFailed + " " + ex.Message, Lang.lobbyTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void BtnJoinByCodeClick(object sender, RoutedEventArgs args)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var accessCode = (txtJoinCode.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (accessCode.Length == 0)
            {
                MessageBox.Show(Lang.lobbyEnterAccessCode);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (currentLobbyId.HasValue)
                {
                    try
                    {
                        var previousLobbyId = currentLobbyId.Value;
                        await Task.Run(() =>
                            lobbyClient.LeaveLobby(new LeaveLobbyRequest { Token = token, LobbyId = previousLobbyId }));
                    }
                    catch
                    {
                    }

                    currentLobbyId = null;
                    currentAccessCode = null;
                    UpdateLobbyHeader(Lang.lobbyTitle, null, null);
                }

                var response = await Task.Run(() =>
                    lobbyClient.JoinByCode(new JoinByCodeRequest { Token = token, AccessCode = accessCode }));

                if (response?.Lobby == null)
                {
                    MessageBox.Show(Lang.lobbyJoinFailed);
                    return;
                }

                currentLobbyId = response.Lobby.LobbyId;
                currentAccessCode = string.IsNullOrWhiteSpace(response.Lobby.AccessCode) ? accessCode : response.Lobby.AccessCode;

                UpdateLobbyHeader(response.Lobby.LobbyName, currentLobbyId, currentAccessCode);
                AppendSystemLine(Lang.lobbyJoined);
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                MessageBox.Show($"{ex.Detail.Code}: {ex.Detail.Message}", Lang.lobbyTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(Lang.noConnection + Environment.NewLine + ex.Message, Lang.lobbyTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.lobbyJoinFailed + " " + ex.Message, Lang.lobbyTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void UpdateLobbyHeader(string lobbyName, Guid? lobbyId, string accessCode)
        {
            txtLobbyHeader.Text = string.IsNullOrWhiteSpace(lobbyName) ? Lang.lobbyTitle : lobbyName;
            txtLobbyId.Text = lobbyId.HasValue ? $"{Lang.lobbyIdPrefix}{lobbyId.Value}" : string.Empty;
            txtAccessCode.Text = string.IsNullOrWhiteSpace(accessCode) ? string.Empty : $"{Lang.lobbyCodePrefix}{accessCode}";
        }

        private async void TxtChatInputKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter)
            {
                return;
            }

            var messageText = (txtChatInput.Text ?? string.Empty).Trim();
            if (messageText.Length == 0)
            {
                return;
            }

            if (!currentLobbyId.HasValue)
            {
                MessageBox.Show(Lang.chatCreateOrJoinFirst);
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
                var lobbyId = currentLobbyId.Value;

                await Task.Run(() =>
                {
                    var request = new SendLobbyMessageRequest
                    {
                        Token = token,
                        LobbyId = lobbyId,
                        Message = messageText
                    };
                    lobbyClient.SendChatMessage(request);
                });

                txtChatInput.Text = string.Empty;
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(Lang.chatSendFailed + Environment.NewLine + ex.Message, Lang.chatTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.chatSendFailed + " " + ex.Message, Lang.chatTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AppendLine(string author, string text)
        {
            chatLines.Add(new ChatLine
            {
                Author = string.IsNullOrWhiteSpace(author) ? "?" : author,
                Text = text ?? string.Empty,
                Time = DateTime.Now.ToString("HH:mm")
            });

            if (lstChatMessages.Items.Count > 0)
            {
                lstChatMessages.ScrollIntoView(lstChatMessages.Items[lstChatMessages.Items.Count - 1]);
            }
        }

        private void AppendSystemLine(string text)
        {
            AppendLine(Lang.system, text);
        }

        public void OnLobbyUpdated(LobbyInfo lobbyInfo)
        {
            if (lobbyInfo == null)
            {
                return;
            }

            if (currentLobbyId.HasValue && lobbyInfo.LobbyId != currentLobbyId.Value)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrWhiteSpace(lobbyInfo.AccessCode))
                {
                    currentAccessCode = lobbyInfo.AccessCode;
                }

                UpdateLobbyHeader(lobbyInfo.LobbyName, lobbyInfo.LobbyId, currentAccessCode);
            });
        }

        public void OnPlayerJoined(PlayerSummary playerSummary)
        {
            Dispatcher.Invoke(() => AppendSystemLine(Lang.lobbyPlayerJoined));
        }

        public void OnPlayerLeft(Guid playerId)
        {
            Dispatcher.Invoke(() => AppendSystemLine(Lang.lobbyPlayerLeft));
        }

        public void OnChatMessageReceived(ChatMessage chatMessage)
        {
            if (chatMessage == null)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                var author = string.IsNullOrWhiteSpace(chatMessage.FromPlayerName) ? Lang.player : chatMessage.FromPlayerName;
                AppendLine(author, chatMessage.Message ?? string.Empty);
            });
        }

        private async Task SendHeartbeatAsync()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                await friendServiceClient.PresenceHeartbeatAsync(new HeartbeatRequest
                {
                    Token = token,
                    Device = "WPF"
                });
            }
            catch
            {
            }
        }

        private void OnWindowUnloaded(object sender, RoutedEventArgs args)
        {
            try { presenceTimer?.Stop(); } catch { }
            try { friendsTimer?.Stop(); } catch { }

            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (!string.IsNullOrWhiteSpace(token) && currentLobbyId.HasValue)
                {
                    try { lobbyClient.LeaveLobby(new LeaveLobbyRequest { Token = token, LobbyId = currentLobbyId.Value }); }
                    catch { }
                }
            }
            catch
            {
            }

            try
            {
                if (lobbyClient.State == CommunicationState.Faulted) lobbyClient.Abort();
                else lobbyClient.Close();
            }
            catch
            {
                try { lobbyClient.Abort(); } catch { }
            }

            try
            {
                if (friendServiceClient.State == CommunicationState.Faulted) friendServiceClient.Abort();
                else friendServiceClient.Close();
            }
            catch
            {
                try { friendServiceClient.Abort(); } catch { }
            }
        }
    }

    public class FriendItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string Presence { get; set; } = "Offline";
        public ImageSource Avatar { get; set; }
        public bool IsOnline { get; set; } = false;
    }
}
