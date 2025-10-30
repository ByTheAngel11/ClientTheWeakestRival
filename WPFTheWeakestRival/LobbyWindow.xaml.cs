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
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class LobbyWindow : Window
    {
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;

        private readonly BlurEffect overlayBlur = new BlurEffect { Radius = 0 };

        private Guid? currentLobbyId;
        private string currentAccessCode;
        private string myDisplayName = "Yo";

        private readonly ObservableCollection<ChatLine> chatLines = new ObservableCollection<ChatLine>();
        private string lastSentText = string.Empty;
        private DateTime lastSentUtc = DateTime.MinValue;

        private readonly ObservableCollection<FriendItem> friendItems = new ObservableCollection<FriendItem>();
        private int pendingRequests;

        private sealed class ChatLine
        {
            public string Author { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public string Time { get; set; } = string.Empty;
        }

        public LobbyWindow()
        {
            InitializeComponent();

            if (lstChatMessages != null)
            {
                lstChatMessages.ItemsSource = chatLines;
            }

            if (txtChatInput != null)
            {
                txtChatInput.Text = string.Empty;
            }

            if (lstFriends != null)
            {
                lstFriends.ItemsSource = friendItems;
                UpdateFriendDrawerUi();
            }

            AppServices.Lobby.LobbyUpdated += OnLobbyUpdatedFromHub;
            AppServices.Lobby.PlayerJoined += OnPlayerJoinedFromHub;
            AppServices.Lobby.PlayerLeft += OnPlayerLeftFromHub;
            AppServices.Lobby.ChatMessageReceived += OnChatMessageReceivedFromHub;

            AppServices.Friends.FriendsUpdated += OnFriendsUpdated;
            AppServices.Friends.Start();

            RefreshProfileButtonAvatar();

            Loaded += async (_, __) => await AppServices.Friends.ManualRefreshAsync();
            Unloaded += OnWindowUnloaded;
        }

        public void InitializeExistingLobby(Guid lobbyId, string accessCode, string lobbyName)
        {
            currentLobbyId = lobbyId;
            currentAccessCode = accessCode;
            UpdateLobbyHeader(lobbyName, currentLobbyId, currentAccessCode);
        }

        private void BtnSettingsClick(object sender, RoutedEventArgs e)
        {
        }

        private void BtnFriendsClick(object sender, RoutedEventArgs e)
        {
            OpenFriendsDrawer();
        }

        private void FriendsDimmerMouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseFriendsDrawer();
        }

        private void BtnCloseFriendsClick(object sender, RoutedEventArgs e)
        {
            CloseFriendsDrawer();
        }

        private async void OpenFriendsDrawer()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible) return;

            this.Effect = overlayBlur;
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
                    this.Effect = null;
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

        private void BtnSendFriendRequestClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            var page = new Pages.AddFriendPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);
            var win = new Window
            {
                Title = Lang.btnSendRequests,
                Content = new Frame
                {
                    Content = page,
                    NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden
                },
                Width = 600,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            win.ShowDialog();
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
            var win = new Window
            {
                Title = Lang.btnRequests,
                Content = new Frame
                {
                    Content = page,
                    NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden
                },
                Width = 600,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            win.ShowDialog();
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
                Close();
            }
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

            var win = new Window
            {
                Title = Lang.profileTitle,
                Content = new Frame
                {
                    Content = page,
                    NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden
                },
                Width = 720,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            win.ShowDialog();

            RefreshProfileButtonAvatar();
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
                myDisplayName = string.IsNullOrWhiteSpace(myProfile?.DisplayName) ? "Yo" : myProfile.DisplayName;

                var avatarSource = UiImageHelper.TryCreateFromUrlOrPath(myProfile?.ProfileImageUrl) ?? UiImageHelper.DefaultAvatar(40);
                if (imgAvatar != null)
                {
                    imgAvatar.Source = avatarSource;
                }
            }
            catch
            {
                var fallback = UiImageHelper.DefaultAvatar(40);
                if (imgAvatar != null)
                {
                    imgAvatar.Source = fallback;
                }
            }
        }

        private void BtnStartMatchClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Lang.matchStartingSoon);
        }

        private void UpdateLobbyHeader(string lobbyName, Guid? lobbyId, string accessCode)
        {
            if (txtLobbyHeader != null)
            {
                txtLobbyHeader.Text = string.IsNullOrWhiteSpace(lobbyName) ? Lang.lobbyTitle : lobbyName;
            }

            if (txtAccessCode != null)
            {
                txtAccessCode.Text = string.IsNullOrWhiteSpace(accessCode) ? string.Empty : $"{Lang.lobbyCodePrefix}{accessCode}";
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

            if (lstChatMessages != null && lstChatMessages.Items.Count > 0)
            {
                lstChatMessages.ScrollIntoView(lstChatMessages.Items[lstChatMessages.Items.Count - 1]);
            }
        }

        private void AppendSystemLine(string text)
        {
            AppendLine(Lang.system, text);
        }

        private async void TxtChatInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;

            var messageText = (txtChatInput?.Text ?? string.Empty).Trim();
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
                AppendLine(myDisplayName, messageText);
                lastSentText = messageText;
                lastSentUtc = DateTime.UtcNow;

                await AppServices.Lobby.SendMessageAsync(token, currentLobbyId.Value, messageText);

                if (txtChatInput != null)
                {
                    txtChatInput.Text = string.Empty;
                }
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(Lang.chatSendFailed + Environment.NewLine + ex.Message, Lang.chatTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.chatSendFailed + " " + ex.Message, Lang.chatTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnLobbyUpdatedFromHub(LobbyInfo info)
        {
            try
            {
                if (info == null)
                {
                    return;
                }

                currentLobbyId = info.LobbyId;
                if (!string.IsNullOrWhiteSpace(info.AccessCode))
                {
                    currentAccessCode = info.AccessCode;
                }

                Dispatcher.Invoke(() => UpdateLobbyHeader(info.LobbyName, info.LobbyId, currentAccessCode));
            }
            catch
            {
            }
        }

        private void OnPlayerJoinedFromHub(PlayerSummary _)
        {
            try
            {
                Dispatcher.Invoke(() => AppendSystemLine(Lang.lobbyPlayerJoined));
            }
            catch
            {
            }
        }

        private void OnPlayerLeftFromHub(Guid _)
        {
            try
            {
                Dispatcher.Invoke(() => AppendSystemLine(Lang.lobbyPlayerLeft));
            }
            catch
            {
            }
        }

        private void OnChatMessageReceivedFromHub(ChatMessage chat)
        {
            try
            {
                if (chat == null)
                {
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    var author = string.IsNullOrWhiteSpace(chat.FromPlayerName) ? Lang.player : chat.FromPlayerName;

                    var isMyRecentEcho =
                        string.Equals(author, myDisplayName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(chat.Message ?? string.Empty, lastSentText, StringComparison.Ordinal) &&
                        (DateTime.UtcNow - lastSentUtc) < TimeSpan.FromSeconds(2);

                    if (!isMyRecentEcho)
                    {
                        AppendLine(author, chat.Message ?? string.Empty);
                    }
                });
            }
            catch
            {
            }
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
            if (friendsEmptyPanel != null)
            {
                friendsEmptyPanel.Visibility = friendItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (lstFriends != null)
            {
                lstFriends.Visibility = friendItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            if (txtRequestsCount != null)
            {
                txtRequestsCount.Text = pendingRequests.ToString();
            }
        }

        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AppServices.Lobby.LobbyUpdated -= OnLobbyUpdatedFromHub;
                AppServices.Lobby.PlayerJoined -= OnPlayerJoinedFromHub;
                AppServices.Lobby.PlayerLeft -= OnPlayerLeftFromHub;
                AppServices.Lobby.ChatMessageReceived -= OnChatMessageReceivedFromHub;
                AppServices.Friends.FriendsUpdated -= OnFriendsUpdated;
            }
            catch
            {
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (!string.IsNullOrWhiteSpace(token) && currentLobbyId.HasValue)
                {
                    await AppServices.Lobby.LeaveLobbyAsync(token);
                }
            }
            catch
            {
            }
            base.OnClosed(e);
        }
    }
}
