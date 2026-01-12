using System;
using System.Collections.ObjectModel;
using System.Globalization;
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

        private const string FAULT_REPORT_COOLDOWN = "REPORT_COOLDOWN";

        private const byte ACCOUNT_STATUS_SUSPENDED = 3;
        private const byte ACCOUNT_STATUS_BANNED = 4;
        private const string SANCTION_END_TIME_FORMAT = "g";

        private const string FRIEND_ENDPOINT_CONFIGURATION_NAME = "WSHttpBinding_IFriendService";

        private const string INVITE_MENU_HEADER = "Invitar al lobby";
        private const string ERROR_INVITE_NO_CODE = "No hay un código de lobby disponible para invitar.";
        private const string ERROR_INVITE_GENERIC = "Ocurrió un error al enviar la invitación.";
        private const string INFO_INVITE_SENT = "Invitación enviada.";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyWindow));

        private readonly BlurEffect overlayBlur = new BlurEffect
        {
            Radius = OVERLAY_BLUR_INITIAL_RADIUS
        };

        private readonly ObservableCollection<LobbyPlayerItem> lobbyPlayers =
            new ObservableCollection<LobbyPlayerItem>();

        private Guid? currentLobbyId;
        private string currentAccessCode = string.Empty;

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
                lstFriends.PreviewMouseRightButtonDown += LstFriendsPreviewMouseRightButtonDown;
                lstFriends.ContextMenuOpening += LstFriendsContextMenuOpening;
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
            AppServices.Lobby.ForcedLogout += OnForcedLogoutFromHub;

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

            try
            {
                if (Dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                Dispatcher.BeginInvoke(action);
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyWindow.Ui error.", ex);
            }
        }

        public void InitializeExistingLobby(Guid lobbyId, string accessCode, string lobbyName)
        {
            currentLobbyId = lobbyId;
            currentAccessCode = accessCode ?? string.Empty;

            UpdateLobbyHeader(txtLobbyHeader, txtAccessCode, lobbyName, currentAccessCode);
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

            UpdateLobbyHeader(txtLobbyHeader, txtAccessCode, info.LobbyName, currentAccessCode);

            LobbyAvatarHelper.RebuildLobbyPlayers(
                lobbyPlayers,
                info.Players,
                MapPlayerToLobbyItem);
        }

        private void BtnSettingsClick(object sender, RoutedEventArgs e)
        {
            var defaults = new MatchSettingsDefaults(
                isPrivate,
                maxPlayers,
                startingScore,
                maxScore,
                pointsCorrect,
                pointsWrong,
                pointsEliminationGain,
                isTiebreakCoinflipAllowed);

            var page = new MatchSettingsPage(defaults);

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

            bool? dialogResult = settingsWindow.ShowDialog();
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
            string token = GetSessionTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            try
            {
                var page = new AddFriendPage(new FriendServiceClient(FRIEND_ENDPOINT_CONFIGURATION_NAME), token);

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
            string token = GetSessionTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            try
            {
                var page = new FriendRequestsPage(new FriendServiceClient(FRIEND_ENDPOINT_CONFIGURATION_NAME), token);

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
            string token = GetSessionTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
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
                    Lang.noConnection,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while opening ModifyProfilePage from lobby.", ex);

                MessageBox.Show(
                    Lang.UiGenericError,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void SetDefaultAvatar(Image avatarTarget)
        {
            var defaultAvatar = UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

            if (avatarTarget != null)
            {
                avatarTarget.Source = defaultAvatar;
            }
        }

        private static byte[] TryGetProfileBytes(object profile)
        {
            if (profile == null)
            {
                return Array.Empty<byte>();
            }

            try
            {
                var type = profile.GetType();

                var prop =
                    type.GetProperty("AvatarBytes") ??
                    type.GetProperty("ProfileImageBytes") ??
                    type.GetProperty("ProfilePhotoBytes") ??
                    type.GetProperty("PhotoBytes");

                if (prop == null || prop.PropertyType != typeof(byte[]))
                {
                    return Array.Empty<byte>();
                }

                var value = prop.GetValue(profile, null) as byte[];
                return value ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                Logger.Warn("TryGetProfileBytes error.", ex);
                return Array.Empty<byte>();
            }
        }

        private void RefreshProfileButtonAvatar()
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                SetDefaultAvatar(imgAvatar);
                return;
            }

            try
            {
                var myProfile = AppServices.Lobby.GetMyProfile(token);

                myDisplayName = string.IsNullOrWhiteSpace(myProfile?.DisplayName)
                    ? DEFAULT_DISPLAY_NAME
                    : myProfile.DisplayName;

                byte[] avatarBytes = TryGetProfileBytes(myProfile);

                var avatarImageSource =
                    UiImageHelper.TryCreateFromBytes(avatarBytes, DEFAULT_AVATAR_SIZE) ??
                    UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

                if (imgAvatar != null)
                {
                    imgAvatar.Source = avatarImageSource;
                }
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                Logger.Warn("Lobby fault while refreshing profile button avatar in lobby.", ex);
                SetDefaultAvatar(imgAvatar);
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("Communication error while refreshing profile button avatar in lobby.", ex);
                SetDefaultAvatar(imgAvatar);
            }
            catch (Exception ex)
            {
                Logger.Warn("Unexpected error refreshing profile button avatar in lobby.", ex);
                SetDefaultAvatar(imgAvatar);
            }
        }

        private async void BtnStartMatchClick(object sender, RoutedEventArgs e)
        {
            string token = GetSessionTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
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
                    Lang.noConnection,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al iniciar partida desde Lobby.", ex);

                MessageBox.Show(
                    ERROR_START_MATCH_GENERIC,
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

        private static void UpdateLobbyHeader(
            TextBlock lobbyHeaderText,
            TextBlock accessCodeText,
            string lobbyName,
            string accessCode)
        {
            if (lobbyHeaderText != null)
            {
                lobbyHeaderText.Text = string.IsNullOrWhiteSpace(lobbyName)
                    ? Lang.lobbyTitle
                    : lobbyName;
            }

            if (accessCodeText != null)
            {
                accessCodeText.Text = string.IsNullOrWhiteSpace(accessCode)
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

            string token = GetSessionTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
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

        private static LobbyPlayerItem MapPlayerToLobbyItem(LobbyContracts.AccountMini account)
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
                    UpdateLobbyHeader(txtLobbyHeader, txtAccessCode, info.LobbyName, currentAccessCode);

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
                txtRequestsCount.Text = pendingRequestsCount.ToString(CultureInfo.InvariantCulture);
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
                AppServices.Lobby.ForcedLogout -= OnForcedLogoutFromHub;

                AppServices.Friends.FriendsUpdated -= OnFriendsUpdated;

                if (lstFriends != null)
                {
                    lstFriends.PreviewMouseRightButtonDown -= LstFriendsPreviewMouseRightButtonDown;
                    lstFriends.ContextMenuOpening -= LstFriendsContextMenuOpening;
                }
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
                        var token = LoginWindow.AppSession.CurrentToken?.Token ?? string.Empty;
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

        private async void MenuItemReportPlayerClick(object sender, RoutedEventArgs e)
        {
            try
            {
                LobbyPlayerItem targetPlayer = TryGetReportTargetPlayer(sender);
                if (targetPlayer == null)
                {
                    return;
                }

                string token = GetSessionTokenOrShowMessage();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                var dialog = CreateReportDialog(targetPlayer.DisplayName);
                bool? dialogResult = dialog.ShowDialog();
                if (dialogResult != true)
                {
                    return;
                }

                await SubmitReportAsync(token, targetPlayer, dialog);
            }
            catch (FaultException<ReportService.ServiceFault> ex)
            {
                if (IsReportCooldownFault(ex))
                {
                    MessageBox.Show(
                        Lang.reportCooldown,
                        Lang.reportPlayer,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                Logger.Warn("Report fault in lobby.", ex);

                MessageBox.Show(
                    Lang.reportFailed,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while submitting report.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while submitting report.", ex);

                MessageBox.Show(
                    Lang.reportUnexpectedError,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static bool IsReportCooldownFault(FaultException<ReportService.ServiceFault> ex)
        {
            return ex != null &&
                ex.Detail != null &&
                string.Equals(ex.Detail.Code, FAULT_REPORT_COOLDOWN, StringComparison.Ordinal);
        }

        private static LobbyPlayerItem TryGetReportTargetPlayer(object sender)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null)
            {
                return null;
            }

            var targetPlayer = menuItem.CommandParameter as LobbyPlayerItem;
            if (targetPlayer == null || targetPlayer.IsMe)
            {
                return null;
            }

            return targetPlayer;
        }

        private WPFTheWeakestRival.Windows.ReportPlayerWindow CreateReportDialog(string displayName)
        {
            return new WPFTheWeakestRival.Windows.ReportPlayerWindow(displayName)
            {
                Owner = this
            };
        }

        private async Task SubmitReportAsync(
            string token,
            LobbyPlayerItem targetPlayer,
            WPFTheWeakestRival.Windows.ReportPlayerWindow dialog)
        {
            var client = new ReportService.ReportServiceClient("WSHttpBinding_IReportService");

            try
            {
                var request = BuildReportRequest(token, targetPlayer, dialog);

                ReportService.SubmitPlayerReportResponse response =
                    await Task.Run(() => client.SubmitPlayerReport(request));

                ShowReportResult(response);
            }
            finally
            {
                CloseClientSafely(client, "ReportServiceClient");
            }
        }

        private ReportService.SubmitPlayerReportRequest BuildReportRequest(
            string token,
            LobbyPlayerItem targetPlayer,
            WPFTheWeakestRival.Windows.ReportPlayerWindow dialog)
        {
            return new ReportService.SubmitPlayerReportRequest
            {
                Token = token,
                ReportedAccountId = targetPlayer.AccountId,
                LobbyId = currentLobbyId,
                ReasonCode = (ReportService.ReportReasonCode)dialog.SelectedReasonCode,
                Comment = string.IsNullOrWhiteSpace(dialog.Comment) ? string.Empty : dialog.Comment
            };
        }

        private void ShowReportResult(ReportService.SubmitPlayerReportResponse response)
        {
            if (response != null && response.SanctionApplied && TryShowSanctionMessage(response))
            {
                return;
            }

            MessageBox.Show(
                Lang.reportSent,
                Lang.reportPlayer,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static bool TryShowSanctionMessage(ReportService.SubmitPlayerReportResponse response)
        {
            if (response == null)
            {
                return false;
            }

            if (response.SanctionType == ACCOUNT_STATUS_SUSPENDED && response.SanctionEndAtUtc.HasValue)
            {
                string localEnd = response.SanctionEndAtUtc.Value
                    .ToLocalTime()
                    .ToString(SANCTION_END_TIME_FORMAT, CultureInfo.CurrentCulture);

                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Lang.reportSanctionTemporary, localEnd),
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return true;
            }

            if (response.SanctionType == ACCOUNT_STATUS_BANNED)
            {
                MessageBox.Show(
                    Lang.reportSanctionPermanent,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return true;
            }

            return false;
        }

        private void LobbyPlayerContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                var element = sender as FrameworkElement;
                if (element == null)
                {
                    return;
                }

                var player = element.DataContext as LobbyPlayerItem;
                if (player != null && player.IsMe)
                {
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error while opening lobby player context menu.", ex);
                e.Handled = true;
            }
        }

        private static void OnForcedLogoutFromHub(ForcedLogoutNotification notification)
        {
            Infraestructure.ForcedLogoutCoordinator.Handle(notification);
        }

        private void LstFriendsPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (lstFriends == null)
                {
                    return;
                }

                var origin = e.OriginalSource as DependencyObject;
                var container = FindAncestor<ListBoxItem>(origin);
                if (container == null)
                {
                    return;
                }

                lstFriends.SelectedItem = container.DataContext;
            }
            catch (Exception ex)
            {
                Logger.Error("Error selecting friend item on right click.", ex);
            }
        }

        private void LstFriendsContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                if (lstFriends == null)
                {
                    e.Handled = true;
                    return;
                }

                var friend = lstFriends.SelectedItem as FriendItem;
                if (friend == null)
                {
                    e.Handled = true;
                    return;
                }

                if (!currentLobbyId.HasValue || string.IsNullOrWhiteSpace(currentAccessCode))
                {
                    e.Handled = true;
                    return;
                }

                var menu = new ContextMenu();

                var inviteItem = new MenuItem
                {
                    Header = INVITE_MENU_HEADER,
                    CommandParameter = friend
                };

                inviteItem.Click += MenuItemInviteToLobbyClick;

                menu.Items.Add(inviteItem);

                lstFriends.ContextMenu = menu;
            }
            catch (Exception ex)
            {
                Logger.Error("Error building friends context menu.", ex);
                e.Handled = true;
            }
        }

        private async void MenuItemInviteToLobbyClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var friend = menuItem?.CommandParameter as FriendItem;
            if (friend == null)
            {
                return;
            }

            string token = GetSessionTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            if (!currentLobbyId.HasValue || string.IsNullOrWhiteSpace(currentAccessCode))
            {
                MessageBox.Show(
                    ERROR_INVITE_NO_CODE,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var client = new FriendServiceClient(FRIEND_ENDPOINT_CONFIGURATION_NAME);

            try
            {
                var request = new SendLobbyInviteEmailRequest
                {
                    Token = token,
                    TargetAccountId = friend.AccountId,
                    LobbyCode = currentAccessCode
                };

                await Task.Run(() => client.SendLobbyInviteEmail(request));

                MessageBox.Show(
                    INFO_INVITE_SENT,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.Warn("Friend fault while sending lobby invite email.", ex);

                MessageBox.Show(
                    ex.Detail != null ? (ex.Detail.Code + ": " + ex.Detail.Message) : ERROR_INVITE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error while sending lobby invite email.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while sending lobby invite email.", ex);

                MessageBox.Show(
                    ERROR_INVITE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                CloseClientSafely(client, "FriendServiceClient.SendLobbyInviteEmail");
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void FriendItemContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                var element = sender as FrameworkElement;
                var friend = element?.DataContext as FriendItem;

                if (friend == null)
                {
                    e.Handled = true;
                    return;
                }

                if (element.ContextMenu == null)
                {
                    element.ContextMenu = new ContextMenu();
                }

                element.ContextMenu.Items.Clear();

                var inviteItem = new MenuItem
                {
                    Header = INVITE_MENU_HEADER,
                    CommandParameter = friend,
                    IsEnabled =
                        !string.IsNullOrWhiteSpace(currentAccessCode) &&
                        friend.AccountId > 0
                };

                inviteItem.Click += MenuItemInviteLobbyByEmailClick;

                element.ContextMenu.Items.Add(inviteItem);
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening friend context menu.", ex);
                e.Handled = true;
            }
        }

        private async void MenuItemInviteLobbyByEmailClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var friend = menuItem?.CommandParameter as FriendItem;
            if (friend == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(currentAccessCode))
            {
                MessageBox.Show(
                    ERROR_INVITE_NO_CODE,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string token = GetSessionTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            if (friend.AccountId <= 0)
            {
                MessageBox.Show(
                    ERROR_INVITE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var client = new FriendServiceClient(FRIEND_ENDPOINT_CONFIGURATION_NAME);

            try
            {
                var request = new SendLobbyInviteEmailRequest
                {
                    Token = token,
                    TargetAccountId = friend.AccountId,
                    LobbyCode = currentAccessCode
                };

                var response = await Task.Run(() => client.SendLobbyInviteEmail(request));

                MessageBox.Show(
                    response != null && response.Sent ? INFO_INVITE_SENT : ERROR_INVITE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.Warn("Friend invite fault.", ex);

                MessageBox.Show(
                    ex.Detail != null ? ex.Detail.Message : ERROR_INVITE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Communication error sending lobby invite.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error sending lobby invite.", ex);

                MessageBox.Show(
                    ERROR_INVITE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                CloseClientSafely(client, "FriendServiceClient.SendLobbyInviteEmail");
            }
        }

        private static string GetSessionTokenOrShowMessage()
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;

            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return string.Empty;
            }

            return token;
        }

        private static void CloseClientSafely(ICommunicationObject client, string context)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    TryAbort(client, context);
                    return;
                }

                client.Close();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(
                    string.Format(CultureInfo.InvariantCulture, "CloseClientSafely communication error. Context={0}", context),
                    ex);

                TryAbort(client, context);
            }
            catch (TimeoutException ex)
            {
                Logger.Warn(
                    string.Format(CultureInfo.InvariantCulture, "CloseClientSafely timeout. Context={0}", context),
                    ex);

                TryAbort(client, context);
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    string.Format(CultureInfo.InvariantCulture, "CloseClientSafely unexpected error. Context={0}", context),
                    ex);

                TryAbort(client, context);
            }
        }

        private static void TryAbort(ICommunicationObject client, string context)
        {
            try
            {
                client.Abort();
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    string.Format(CultureInfo.InvariantCulture, "Abort failed. Context={0}", context),
                    ex);
            }
        }
    }
}