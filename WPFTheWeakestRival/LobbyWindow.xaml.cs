using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Navigation;
using log4net;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;
using WPFTheWeakestRival.Pages;

using LobbyContracts = WPFTheWeakestRival.LobbyService;
using MatchContracts = WPFTheWeakestRival.MatchmakingService;

namespace WPFTheWeakestRival
{
    public partial class LobbyWindow : Window
    {
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyWindow));

        private readonly BlurEffect overlayBlur = new BlurEffect { Radius = 0 };

        private Guid? currentLobbyId;
        private string currentAccessCode;
        private string myDisplayName = "Yo";

        private readonly ObservableCollection<ChatLine> chatLines = new ObservableCollection<ChatLine>();
        private string lastSentText = string.Empty;
        private DateTime lastSentUtc = DateTime.MinValue;

        private readonly ObservableCollection<FriendItem> friendItems = new ObservableCollection<FriendItem>();
        private int pendingRequests;

        private bool _isPrivate = true;
        private int _maxPlayers = 4;
        private decimal _startingScore = 0m;
        private decimal _maxScore = 100m;
        private decimal _pointsCorrect = 10m;
        private decimal _pointsWrong = -5m;
        private decimal _pointsEliminationGain = 5m;
        private bool _allowCoinflip = true;


        private sealed class ChatLine
        {
            public string Author { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public string Time { get; set; } = string.Empty;
        }

        private sealed class MatchmakingClientCallback : MatchContracts.IMatchmakingServiceCallback
        {
            public void OnMatchCreated(MatchContracts.MatchInfo match)
            {
                // Por ahora no hacemos nada aquí; la creación se maneja en la respuesta del CreateMatchAsync.
            }

            public void OnMatchStarted(MatchContracts.MatchInfo match)
            {
                // Futuro: podrías avisar al usuario cuando el server arranque la match.
            }

            public void OnMatchPlayerJoined(Guid matchId, MatchContracts.PlayerSummary player)
            {
                // Futuro: actualizar UI cuando alguien se una a la match.
            }

            public void OnMatchPlayerLeft(Guid matchId, Guid playerId)
            {
                // Futuro: actualizar UI cuando alguien salga de la match.
            }

            public void OnMatchCancelled(Guid matchId, string reason)
            {
                // Futuro: avisar que la match fue cancelada.
            }
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
            AppServices.Lobby.MatchStarted += OnMatchStartedFromHub;

            AppServices.Friends.FriendsUpdated += OnFriendsUpdated;
            AppServices.Friends.Start();

            RefreshProfileButtonAvatar();

            Loaded += async (_, __) =>
            {
                try
                {
                    await AppServices.Friends.ManualRefreshAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error refreshing friends list on lobby window load.", ex);
                }
            };

            Unloaded += OnWindowUnloaded;
        }

        public void InitializeExistingLobby(Guid lobbyId, string accessCode, string lobbyName)
        {
            currentLobbyId = lobbyId;
            currentAccessCode = accessCode;
            UpdateLobbyHeader(lobbyName, currentAccessCode);
        }

        private void BtnSettingsClick(object sender, RoutedEventArgs e)
        {
            var page = new MatchSettingsPage(
                _isPrivate,
                _maxPlayers,
                _startingScore,
                _maxScore,
                _pointsCorrect,
                _pointsWrong,
                _pointsEliminationGain,
                _allowCoinflip);

            var win = new Window
            {
                Title = Lang.lblSettings,
                Content = new Frame
                {
                    Content = page,
                    NavigationUIVisibility = NavigationUIVisibility.Hidden
                },
                Width = 480,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var result = win.ShowDialog();
            if (result == true)
            {
                _isPrivate = page.IsPrivate;
                _maxPlayers = page.MaxPlayers;

                _startingScore = page.StartingScore;
                _maxScore = page.MaxScore;
                _pointsCorrect = page.PointsPerCorrect;
                _pointsWrong = page.PointsPerWrong;
                _pointsEliminationGain = page.PointsPerEliminationGain;
                _allowCoinflip = page.AllowTiebreakCoinflip;
            }
        }


        private void BtnFriendsClick(object sender, RoutedEventArgs e)
        {
            _ = OpenFriendsDrawerAsync();
        }

        private void FriendsDimmerMouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseFriendsDrawer();
        }

        private void BtnCloseFriendsClick(object sender, RoutedEventArgs e)
        {
            CloseFriendsDrawer();
        }

        private async Task OpenFriendsDrawerAsync()
        {
            if (friendsDrawerHost.Visibility == Visibility.Visible)
            {
                return;
            }

            if (grdMainArea != null)
            {
                grdMainArea.Effect = overlayBlur;
            }

            overlayBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(overlayBlur.Radius, 3.0, TimeSpan.FromMilliseconds(DRAWER_ANIM_IN_MS))
                {
                    EasingFunction = new QuadraticEase()
                });

            try
            {
                await AppServices.Friends.ManualRefreshAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Error refreshing friends list while opening friends drawer.", ex);
            }

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
            if (friendsDrawerHost.Visibility != Visibility.Visible)
            {
                return;
            }

            if (FindResource("sbCloseFriendsDrawer") is Storyboard close)
            {
                close.Completed += (_, __) =>
                {
                    friendsDrawerHost.Visibility = Visibility.Collapsed;

                    if (grdMainArea != null)
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

        private void BtnSendFriendRequestClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            try
            {
                var page = new Pages.AddFriendPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);
                var win = new Window
                {
                    Title = Lang.btnSendRequests,
                    Content = new Frame
                    {
                        Content = page,
                        NavigationUIVisibility = NavigationUIVisibility.Hidden
                    },
                    Width = 600,
                    Height = 420,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening AddFriendPage dialog from lobby.", ex);
                MessageBox.Show(Lang.addFriendServiceError, Lang.addFriendTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnViewRequestsClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            try
            {
                var page = new Pages.FriendRequestsPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);
                var win = new Window
                {
                    Title = Lang.btnRequests,
                    Content = new Frame
                    {
                        Content = page,
                        NavigationUIVisibility = NavigationUIVisibility.Hidden
                    },
                    Width = 600,
                    Height = 420,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening FriendRequestsPage dialog from lobby.", ex);
                MessageBox.Show(Lang.addFriendServiceError, Lang.btnRequests, MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            Close();
        }

        private void BtnModifyProfileClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            try
            {
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
                        NavigationUIVisibility = NavigationUIVisibility.Hidden
                    },
                    Width = 720,
                    Height = 460,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                win.ShowDialog();

                RefreshProfileButtonAvatar();
            }
            catch (FaultException<AuthService.ServiceFault> ex)
            {
                Logger.Warn("Auth fault while opening ModifyProfilePage from lobby.", ex);
                MessageBox.Show(
                    ex.Detail.Code + ": " + ex.Detail.Message,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while opening ModifyProfilePage from lobby.", ex);
                MessageBox.Show(
                    Lang.noConnection + Environment.NewLine + ex.Message,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while opening ModifyProfilePage from lobby.", ex);
                MessageBox.Show(
                    Lang.profileTitle,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                var myProfile = AppServices.Lobby.GetMyProfile(token);
                myDisplayName = string.IsNullOrWhiteSpace(myProfile?.DisplayName) ? "Yo" : myProfile.DisplayName;

                var avatarSource =
                    UiImageHelper.TryCreateFromUrlOrPath(myProfile?.ProfileImageUrl) ??
                    UiImageHelper.DefaultAvatar(40);

                if (imgAvatar != null)
                {
                    imgAvatar.Source = avatarSource;
                }
            }
            catch (FaultException<AuthService.ServiceFault> ex)
            {
                Logger.Warn("Auth fault while refreshing profile button avatar in lobby.", ex);
                if (imgAvatar != null)
                {
                    imgAvatar.Source = UiImageHelper.DefaultAvatar(40);
                }
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while refreshing profile button avatar in lobby.", ex);
                if (imgAvatar != null)
                {
                    imgAvatar.Source = UiImageHelper.DefaultAvatar(40);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while refreshing profile button avatar in lobby.", ex);
                if (imgAvatar != null)
                {
                    imgAvatar.Source = UiImageHelper.DefaultAvatar(40);
                }
            }
        }

        private async void BtnStartMatchClick(object sender, RoutedEventArgs e)
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return;
            }

            if (btnStart != null)
            {
                btnStart.IsEnabled = false;
            }

            try
            {
                var client = AppServices.Lobby.RawClient;

                var request = new LobbyService.StartLobbyMatchRequest
                {
                    Token = token,
                    MaxPlayers = _maxPlayers,      // lo que elegiste en MatchSettingsPage
                    IsPrivate = _isPrivate,

                    Config = new LobbyService.MatchConfigDto
                    {
                        StartingScore = _startingScore,
                        MaxScore = _maxScore,
                        PointsPerCorrect = _pointsCorrect,
                        PointsPerWrong = _pointsWrong,
                        PointsPerEliminationGain = _pointsEliminationGain,
                        AllowTiebreakCoinflip = _allowCoinflip
                    }

                };

                await Task.Run(() => client.StartLobbyMatch(request));
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                Logger.Warn("Fault al iniciar partida desde Lobby.", ex);
                MessageBox.Show(
                    ex.Detail.Code + ": " + ex.Detail.Message,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al iniciar partida desde Lobby.", ex);
                MessageBox.Show(
                    Lang.noConnection + Environment.NewLine + ex.Message,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al iniciar partida desde Lobby.", ex);
                MessageBox.Show(
                    "Ocurrió un error al iniciar la partida." + Environment.NewLine + ex.Message,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (btnStart != null)
                {
                    btnStart.IsEnabled = true;
                }
            }
        }


        private void UpdateLobbyHeader(string lobbyName, string accessCode)
        {
            if (txtLobbyHeader != null)
            {
                txtLobbyHeader.Text = string.IsNullOrWhiteSpace(lobbyName) ? Lang.lobbyTitle : lobbyName;
            }

            if (txtAccessCode != null)
            {
                txtAccessCode.Text = string.IsNullOrWhiteSpace(accessCode)
                    ? string.Empty
                    : Lang.lobbyCodePrefix + accessCode;
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
                lstChatMessages.ScrollIntoView(
                    lstChatMessages.Items[lstChatMessages.Items.Count - 1]);
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
                Logger.Error("Communication error while sending chat message in lobby.", ex);
                MessageBox.Show(
                    Lang.chatSendFailed + Environment.NewLine + ex.Message,
                    Lang.chatTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while sending chat message in lobby.", ex);
                MessageBox.Show(
                    Lang.chatSendFailed + " " + ex.Message,
                    Lang.chatTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                Dispatcher.Invoke(
                    () => UpdateLobbyHeader(info.LobbyName, currentAccessCode));
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling lobby update from hub.", ex);
            }
        }

        private void OnPlayerJoinedFromHub(LobbyContracts.PlayerSummary _)
        {
            try
            {
                Dispatcher.Invoke(() => AppendSystemLine(Lang.lobbyPlayerJoined));
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling player-joined event from hub.", ex);
            }
        }

        private void OnPlayerLeftFromHub(Guid _)
        {
            try
            {
                Dispatcher.Invoke(() => AppendSystemLine(Lang.lobbyPlayerLeft));
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling player-left event from hub.", ex);
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
                    var author = string.IsNullOrWhiteSpace(chat.FromPlayerName)
                        ? Lang.player
                        : chat.FromPlayerName;

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
            catch (Exception ex)
            {
                Logger.Error("Error handling chat message received from hub.", ex);
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
                friendsEmptyPanel.Visibility =
                    friendItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (lstFriends != null)
            {
                lstFriends.Visibility =
                    friendItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
                AppServices.Lobby.MatchStarted -= OnMatchStartedFromHub;
                AppServices.Friends.FriendsUpdated -= OnFriendsUpdated;
            }
            catch (Exception ex)
            {
                Logger.Warn("Error detaching lobby and friends event handlers on window unload.", ex);
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
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while leaving lobby on window close.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while leaving lobby on window close.", ex);
            }

            base.OnClosed(e);
        }

        private void BtnCopyCodeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentAccessCode))
                {
                    MessageBox.Show(
                        "No hay un código de lobby para copiar.",
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                Clipboard.SetText(currentAccessCode);
            }
            catch (Exception ex)
            {
                Logger.Error("Error copying lobby access code to clipboard.", ex);
                MessageBox.Show(
                    "Ocurrió un error al copiar el código al portapapeles.",
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnMatchStartedFromHub(LobbyService.MatchInfo match)
        {
            try
            {
                if (match == null)
                {
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    var token = LoginWindow.AppSession.CurrentToken?.Token;
                    var session = LoginWindow.AppSession.CurrentToken;
                    var myUserId = session != null ? session.UserId : 0;

                    // Desde el hub normalmente eres invitado, así que marcamos isHost = false
                    var isHost = false;

                    var win = new MatchWindow(match, token, myUserId, isHost, this);
                    this.Hide();
                    win.Show();
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling OnMatchStartedFromHub in LobbyWindow.", ex);
            }
        }
    }
}
