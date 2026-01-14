using log4net;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WPFTheWeakestRival.Infraestructure.Lobby;
using WPFTheWeakestRival.Infrastructure;

namespace WPFTheWeakestRival
{
    public partial class LobbyWindow : Window
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyWindow));

        private readonly LobbyRuntimeState runtimeState;
        private readonly LobbyChatController chatController;
        private readonly LobbyFriendsController friendsController;
        private readonly LobbyPlayersController playersController;
        private readonly LobbyProfileController profileController;
        private readonly LobbyMatchController matchController;
        private readonly LobbyReconnectController reconnectController;

        private bool isDisposed;

        public LobbyWindow()
        {
            InitializeComponent();

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

            if (imgAvatar != null)
            {
                RenderOptions.SetBitmapScalingMode(imgAvatar, BitmapScalingMode.HighQuality);
            }

            runtimeState = new LobbyRuntimeState();

            var ui = new LobbyUiDispatcher(this, Logger);

            chatController = new LobbyChatController(
                ui,
                runtimeState,
                lstChatMessages,
                txtChatInput);

            friendsController = new LobbyFriendsController(
                ui,
                runtimeState,
                this,
                grdMainArea,
                friendsDrawerHost,
                friendsDrawerTT,
                friendsEmptyPanel,
                lstFriends,
                txtRequestsCount,
                Logger);

            playersController = new LobbyPlayersController(
                ui,
                runtimeState,
                this,
                txtLobbyHeader,
                txtAccessCode,
                lstLobbyPlayers,
                Logger);

            profileController = new LobbyProfileController(
                ui,
                runtimeState,
                imgAvatar,
                Logger);

            matchController = new LobbyMatchController(
                ui,
                runtimeState,
                this,
                btnStart,
                profileController,
                Logger);

            reconnectController = new LobbyReconnectController(
                ui,
                runtimeState,
                grdReconnectOverlay,
                txtReconnectStatus,
                new LoginNavigator(Logger),
                chatController,
                Logger);

            Loaded += LobbyWindowLoaded;
            Unloaded += LobbyWindowUnloaded;
        }

        public void InitializeExistingLobby(Guid lobbyId, string accessCode, string lobbyName)
        {
            playersController.InitializeExistingLobby(lobbyId, accessCode, lobbyName);
        }

        public void InitializeExistingLobby(LobbyService.LobbyInfo info)
        {
            playersController.InitializeExistingLobby(info);
        }

        protected override void OnClosed(EventArgs e)
        {
            TryLeaveLobby();
            DisposeControllers();

            base.OnClosed(e);
        }

        private void LobbyWindowLoaded(object sender, RoutedEventArgs e)
        {
            friendsController.OnLoaded();
            profileController.RefreshAvatar();
        }

        private void LobbyWindowUnloaded(object sender, RoutedEventArgs e)
        {
            DisposeControllers();
        }

        private void DisposeControllers()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            try
            {
                reconnectController.Dispose();
                matchController.Dispose();
                playersController.Dispose();
                friendsController.Dispose();
                chatController.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyWindow dispose error.", ex);
            }
        }

        private static void TryLeaveLobby()
        {
            try
            {
                string token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                Task leaveTask = AppServices.Lobby.LeaveLobbyAsync(token);

                _ = leaveTask.ContinueWith(
                    t => Logger.Warn("Error leaving lobby on close.", t.Exception),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                Logger.Warn("Unexpected error leaving lobby on close.", ex);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            if (friendsController.IsDrawerOpen)
            {
                friendsController.CloseDrawer();
                return;
            }

            Close();
        }

        private void BtnFriendsClick(object sender, RoutedEventArgs e)
        {
            friendsController.OpenDrawerAsync();
        }

        private void BtnCloseFriendsClick(object sender, RoutedEventArgs e)
        {
            friendsController.CloseDrawer();
        }

        private void FriendsDimmerMouseDown(object sender, MouseButtonEventArgs e)
        {
            friendsController.CloseDrawer();
        }

        private void BtnSendFriendRequestClick(object sender, RoutedEventArgs e)
        {
            friendsController.OpenAddFriendDialog();
        }

        private void BtnViewRequestsClick(object sender, RoutedEventArgs e)
        {
            friendsController.OpenFriendRequestsDialog();
        }

        private void BtnCopyCodeClick(object sender, RoutedEventArgs e)
        {
            playersController.CopyCodeToClipboard();
        }

        private void BtnSettingsClick(object sender, RoutedEventArgs e)
        {
            matchController.OpenSettings();
        }

        private void BtnStartMatchClick(object sender, RoutedEventArgs e)
        {
            matchController.StartMatchAsync();
        }

        private void BtnModifyProfileClick(object sender, RoutedEventArgs e)
        {
            matchController.OpenProfileDialog();
        }

        private void TxtChatInputKeyDown(object sender, KeyEventArgs e)
        {
            chatController.HandleChatInputKeyDown(e);
        }

        private void LobbyPlayerContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                if (sender is Grid grid && grid.DataContext is WPFTheWeakestRival.Models.LobbyPlayerItem player)
                {
                    lstLobbyPlayers.SelectedItem = player;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyWindow context menu selection error.", ex);
            }

            playersController.LobbyPlayerContextMenuOpening(sender, e);
        }


        private void MenuItemReportPlayerClick(object sender, RoutedEventArgs e)
        {
            playersController.MenuItemReportPlayerClick(sender, e);
        }


    }
}
