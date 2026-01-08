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

namespace WPFTheWeakestRival
{
    public partial class LobbyWindow : Window
    {
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;

        private const double OVERLAY_BLUR_INITIAL_RADIUS = 0.0;
        private const double OVERLAY_BLUR_TARGET_RADIUS = 3.0;

        private const int FRIENDS_DRAWER_INITIAL_TRANSLATE_X = 340;

        private const int RECENT_ECHO_WINDOW_SECONDS = 2;

        private const int DEFAULT_AVATAR_SIZE = 80;
        private const string DEFAULT_DISPLAY_NAME = "Yo";
        private const string UNKNOWN_AUTHOR_DISPLAY_NAME = "?";
        private const string CHAT_TIME_FORMAT = "HH:mm";

        private const double SETTINGS_WINDOW_WIDTH = 480;
        private const double SETTINGS_WINDOW_HEIGHT = 420;

        private const double FRIEND_DIALOG_WINDOW_WIDTH = 600;
        private const double FRIEND_DIALOG_WINDOW_HEIGHT = 420;

        private const double PROFILE_WINDOW_WIDTH = 720;
        private const double PROFILE_WINDOW_HEIGHT = 460;

        private const string ERROR_COPY_CODE_NO_CODE = "No hay un código de lobby para copiar.";
        private const string ERROR_COPY_CODE_GENERIC = "Ocurrió un error al copiar el código al portapapeles.";
        private const string ERROR_START_MATCH_GENERIC = "Ocurrió un error al iniciar la partida.";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyWindow));

        private readonly BlurEffect overlayBlur = new BlurEffect
        {
            Radius = OVERLAY_BLUR_INITIAL_RADIUS
        };

        private readonly ObservableCollection<LobbyPlayerItem> lobbyPlayers =
            new ObservableCollection<LobbyPlayerItem>();

        private Guid? currentLobbyId;
        private string currentAccessCode;
        private string myDisplayName = DEFAULT_DISPLAY_NAME;

        private readonly ObservableCollection<ChatLine> chatLines = new ObservableCollection<ChatLine>();
        private string lastSentText = string.Empty;
        private DateTime lastSentUtc = DateTime.MinValue;

        private readonly ObservableCollection<FriendItem> friendItems = new ObservableCollection<FriendItem>();
        private int pendingRequestsCount;

        private bool isPrivate = true;
        private int maxPlayers = 4;
        private decimal startingScore = 0m;
        private decimal maxScore = 100m;
        private decimal pointsCorrect = 10m;
        private decimal pointsWrong = -5m;
        private decimal pointsEliminationGain = 5m;
        private bool isTiebreakCoinflipAllowed = true;

        private bool isOpeningMatchWindow;

        private sealed class ChatLine
        {
            public string Author { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public string Time { get; set; } = string.Empty;
        }

        public LobbyWindow()
        {
            InitializeComponent();

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

            if (imgAvatar != null)
            {
                RenderOptions.SetBitmapScalingMode(imgAvatar, BitmapScalingMode.HighQuality);
            }

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

            if (lstLobbyPlayers != null)
            {
                lstLobbyPlayers.ItemsSource = lobbyPlayers;
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

        private void Ui(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            Dispatcher.BeginInvoke(action);
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
                isPrivate,
                maxPlayers,
                startingScore,
                maxScore,
                pointsCorrect,
                pointsWrong,
                pointsEliminationGain,
                isTiebreakCoinflipAllowed);

            var settingsFrame = new Frame
            {
                Content = page,
                NavigationUIVisibility = NavigationUIVisibility.Hidden
            };

            var settingsWindow = new Window
            {
                Title = Lang.lblSettings,
                Content = settingsFrame,
                Width = SETTINGS_WINDOW_WIDTH,
                Height = SETTINGS_WINDOW_HEIGHT,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var dialogResult = settingsWindow.ShowDialog();
            if (dialogResult == true)
            {
                isPrivate = page.IsPrivate;
                maxPlayers = page.MaxPlayers;

                startingScore = page.StartingScore;
                maxScore = page.MaxScore;
                pointsCorrect = page.PointsPerCorrect;
                pointsWrong = page.PointsPerWrong;
                pointsEliminationGain = page.PointsPerEliminationGain;
                isTiebreakCoinflipAllowed = page.AllowTiebreakCoinflip;
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

            var blurAnimation = new DoubleAnimation(
                overlayBlur.Radius,
                OVERLAY_BLUR_TARGET_RADIUS,
                TimeSpan.FromMilliseconds(DRAWER_ANIM_IN_MS))
            {
                EasingFunction = new QuadraticEase()
            };

            overlayBlur.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);

            try
            {
                await AppServices.Friends.ManualRefreshAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Error refreshing friends list while opening friends drawer.", ex);
            }

            friendsDrawerTT.X = FRIENDS_DRAWER_INITIAL_TRANSLATE_X;
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

                    if (grdMainArea != null)
                    {
                        grdMainArea.Effect = null;
                    }
                };

                storyboard.Begin(this, true);
            }

            var blurAnimation = new DoubleAnimation(
                overlayBlur.Radius,
                OVERLAY_BLUR_INITIAL_RADIUS,
                TimeSpan.FromMilliseconds(DRAWER_ANIM_OUT_MS))
            {
                EasingFunction = new QuadraticEase()
            };

            overlayBlur.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);
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
                var page = new AddFriendPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);

                var friendFrame = new Frame
                {
                    Content = page,
                    NavigationUIVisibility = NavigationUIVisibility.Hidden
                };

                var friendWindow = new Window
                {
                    Title = Lang.btnSendRequests,
                    Content = friendFrame,
                    Width = FRIEND_DIALOG_WINDOW_WIDTH,
                    Height = FRIEND_DIALOG_WINDOW_HEIGHT,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                friendWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening AddFriendPage dialog from lobby.", ex);
                MessageBox.Show(
                    Lang.addFriendServiceError,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                var page = new FriendRequestsPage(new FriendServiceClient("WSHttpBinding_IFriendService"), token);

                var friendRequestsFrame = new Frame
                {
                    Content = page,
                    NavigationUIVisibility = NavigationUIVisibility.Hidden
                };

                var friendRequestsWindow = new Window
                {
                    Title = Lang.btnRequests,
                    Content = friendRequestsFrame,
                    Width = FRIEND_DIALOG_WINDOW_WIDTH,
                    Height = FRIEND_DIALOG_WINDOW_HEIGHT,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                friendRequestsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening FriendRequestsPage dialog from lobby.", ex);
                MessageBox.Show(
                    Lang.addFriendServiceError,
                    Lang.btnRequests,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                var profileFrame = new Frame
                {
                    Content = page,
                    NavigationUIVisibility = NavigationUIVisibility.Hidden
                };

                var profileWindow = new Window
                {
                    Title = Lang.profileTitle,
                    Content = profileFrame,
                    Width = PROFILE_WINDOW_WIDTH,
                    Height = PROFILE_WINDOW_HEIGHT,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                profileWindow.ShowDialog();

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

        private void SetDefaultAvatar()
        {
            var defaultAvatar = UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

            if (imgAvatar != null)
            {
                imgAvatar.Source = defaultAvatar;
            }
        }

        private void RefreshProfileButtonAvatar()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    SetDefaultAvatar();
                    return;
                }

                var myProfile = AppServices.Lobby.GetMyProfile(token);

                myDisplayName = string.IsNullOrWhiteSpace(myProfile?.DisplayName)
                    ? DEFAULT_DISPLAY_NAME
                    : myProfile.DisplayName;

                var avatarImageSource =
                    UiImageHelper.TryCreateFromUrlOrPath(myProfile?.ProfileImageUrl) ??
                    UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

                if (imgAvatar != null)
                {
                    imgAvatar.Source = avatarImageSource;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error refreshing profile button avatar in lobby.", ex);
                SetDefaultAvatar();
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

                var request = new StartLobbyMatchRequest
                {
                    Token = token
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
                    ERROR_START_MATCH_GENERIC + Environment.NewLine + ex.Message,
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
                txtLobbyHeader.Text = string.IsNullOrWhiteSpace(lobbyName)
                    ? Lang.lobbyTitle
                    : lobbyName;
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
            chatLines.Add(
                new ChatLine
                {
                    Author = string.IsNullOrWhiteSpace(author) ? UNKNOWN_AUTHOR_DISPLAY_NAME : author,
                    Text = text ?? string.Empty,
                    Time = DateTime.Now.ToString(CHAT_TIME_FORMAT)
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
            catch (Exception ex)
            {
                Logger.Error("Error sending chat message in lobby.", ex);
                MessageBox.Show(
                    Lang.chatSendFailed,
                    Lang.chatTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private LobbyPlayerItem MapPlayerToLobbyItem(LobbyContracts.AccountMini account)
        {
            var item = LobbyAvatarHelper.BuildFromAccountMini(account);
            if (item == null)
            {
                return null;
            }

            var session = LoginWindow.AppSession.CurrentToken;
            if (session != null && session.UserId == item.AccountId)
            {
                item.IsMe = true;
            }

            return item;
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

                Ui(() =>
                {
                    UpdateLobbyHeader(info.LobbyName, currentAccessCode);

                    LobbyAvatarHelper.RebuildLobbyPlayers(
                        lobbyPlayers,
                        info.Players,
                        MapPlayerToLobbyItem);
                });
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
                Ui(() => AppendSystemLine(Lang.lobbyPlayerJoined));
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
                Ui(() => AppendSystemLine(Lang.lobbyPlayerLeft));
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

                Ui(() =>
                {
                    var author = string.IsNullOrWhiteSpace(chat.FromPlayerName)
                        ? Lang.player
                        : chat.FromPlayerName;

                    var isMyRecentEcho =
                        string.Equals(author, myDisplayName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(chat.Message ?? string.Empty, lastSentText, StringComparison.Ordinal) &&
                        (DateTime.UtcNow - lastSentUtc) < TimeSpan.FromSeconds(RECENT_ECHO_WINDOW_SECONDS);

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

            foreach (var friend in list)
            {
                friendItems.Add(friend);
            }

            pendingRequestsCount = pending;
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
                txtRequestsCount.Text = pendingRequestsCount.ToString();
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
            catch (Exception ex)
            {
                Logger.Error("Error leaving lobby on window close.", ex);
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
                        ERROR_COPY_CODE_NO_CODE,
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
                    ERROR_COPY_CODE_GENERIC,
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

                Ui(() =>
                {
                    if (isOpeningMatchWindow)
                    {
                        Logger.Warn("OnMatchStartedFromHub ignored because match window is already opening.");
                        return;
                    }

                    isOpeningMatchWindow = true;

                    try
                    {
                        var token = LoginWindow.AppSession.CurrentToken?.Token;
                        var session = LoginWindow.AppSession.CurrentToken;
                        var myUserId = session != null ? session.UserId : 0;

                        var isHost = false;

                        var matchWindow = new MatchWindow(match, token, myUserId, isHost, this);

                        matchWindow.Closed += (_, __) =>
                        {
                            Ui(() =>
                            {
                                try
                                {
                                    Show();
                                    Activate();
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warn("Error restoring lobby after match window closed.", ex);
                                }
                                finally
                                {
                                    isOpeningMatchWindow = false;
                                }
                            });
                        };

                        Hide();
                        matchWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error opening MatchWindow from lobby.", ex);

                        try
                        {
                            Show();
                            Activate();
                        }
                        catch (Exception showEx)
                        {
                            Logger.Warn("Error restoring lobby after MatchWindow open failure.", showEx);
                        }
                        finally
                        {
                            isOpeningMatchWindow = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling OnMatchStartedFromHub in LobbyWindow.", ex);
            }
        }

        public void InitializeExistingLobby(LobbyContracts.LobbyInfo info)
        {
            if (info == null)
            {
                return;
            }

            currentLobbyId = info.LobbyId;
            currentAccessCode = string.IsNullOrWhiteSpace(info.AccessCode)
                ? string.Empty
                : info.AccessCode;

            UpdateLobbyHeader(info.LobbyName, currentAccessCode);

            LobbyAvatarHelper.RebuildLobbyPlayers(
                lobbyPlayers,
                info.Players,
                MapPlayerToLobbyItem);
        }
    }
}
