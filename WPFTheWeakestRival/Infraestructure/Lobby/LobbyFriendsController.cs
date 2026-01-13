using log4net;
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
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Pages;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyFriendsController : IDisposable
    {
        private const int DRAWER_ANIM_IN_MS = 180;
        private const int DRAWER_ANIM_OUT_MS = 160;

        private const double OVERLAY_BLUR_INITIAL_RADIUS = 0.0;
        private const double OVERLAY_BLUR_TARGET_RADIUS = 3.0;

        private const int FRIENDS_DRAWER_INITIAL_TRANSLATE_X = 340;

        private const double FRIEND_DIALOG_WINDOW_WIDTH = 600;
        private const double FRIEND_DIALOG_WINDOW_HEIGHT = 420;

        private const string FRIEND_ENDPOINT_CONFIGURATION_NAME = "WSHttpBinding_IFriendService";

        private const string INVITE_MENU_HEADER = "Invitar al lobby";

        private const string ERROR_INVITE_NO_CODE = "No hay un código de lobby disponible para invitar.";
        private const string ERROR_INVITE_GENERIC = "Ocurrió un error al enviar la invitación.";
        private const string INFO_INVITE_SENT = "Invitación enviada.";

        private readonly LobbyUiDispatcher ui;
        private readonly LobbyRuntimeState state;

        private readonly Window hostWindow;
        private readonly FrameworkElement mainArea;
        private readonly FrameworkElement drawerHost;
        private readonly TranslateTransform drawerTranslate;
        private readonly FrameworkElement emptyPanel;
        private readonly ListBox friendsList;
        private readonly TextBlock requestsCountText;

        private readonly ILog logger;

        private readonly BlurEffect overlayBlur;

        private readonly ObservableCollection<FriendItem> friendItems;
        private int pendingRequestsCount;

        internal bool IsDrawerOpen
        {
            get
            {
                return drawerHost != null && drawerHost.Visibility == Visibility.Visible;
            }
        }

        internal LobbyFriendsController(
            LobbyUiDispatcher ui,
            LobbyRuntimeState state,
            Window hostWindow,
            FrameworkElement mainArea,
            FrameworkElement drawerHost,
            TranslateTransform drawerTranslate,
            FrameworkElement emptyPanel,
            ListBox friendsList,
            TextBlock requestsCountText,
            ILog logger)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.hostWindow = hostWindow;

            this.mainArea = mainArea;
            this.drawerHost = drawerHost;
            this.drawerTranslate = drawerTranslate;
            this.emptyPanel = emptyPanel;
            this.friendsList = friendsList;
            this.requestsCountText = requestsCountText;

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            overlayBlur = new BlurEffect
            {
                Radius = OVERLAY_BLUR_INITIAL_RADIUS
            };

            friendItems = new ObservableCollection<FriendItem>();

            if (this.friendsList != null)
            {
                this.friendsList.ItemsSource = friendItems;
                this.friendsList.PreviewMouseRightButtonDown += FriendsListPreviewMouseRightButtonDown;
                this.friendsList.ContextMenuOpening += FriendsListContextMenuOpening;
            }

            AppServices.Friends.FriendsUpdated += OnFriendsUpdated;
        }

        internal void OnLoaded()
        {
            _ = RefreshFriendsSafeAsync();
        }

        internal void OpenDrawerAsync()
        {
            _ = OpenDrawerInternalAsync();
        }

        internal void CloseDrawer()
        {
            CloseDrawerInternal();
        }

        internal void OpenAddFriendDialog()
        {
            string token = SessionTokenProvider.GetTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            try
            {
                var page = new AddFriendPage(new FriendServiceClient(FRIEND_ENDPOINT_CONFIGURATION_NAME), token);

                var window = BuildPageDialog(
                    page,
                    Lang.btnSendRequests,
                    FRIEND_DIALOG_WINDOW_WIDTH,
                    FRIEND_DIALOG_WINDOW_HEIGHT);

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                logger.Error("Error opening AddFriendPage dialog from lobby.", ex);

                MessageBox.Show(
                    Lang.addFriendServiceError,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        internal void OpenFriendRequestsDialog()
        {
            string token = SessionTokenProvider.GetTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            try
            {
                var page = new FriendRequestsPage(new FriendServiceClient(FRIEND_ENDPOINT_CONFIGURATION_NAME), token);

                var window = BuildPageDialog(
                    page,
                    Lang.btnRequests,
                    FRIEND_DIALOG_WINDOW_WIDTH,
                    FRIEND_DIALOG_WINDOW_HEIGHT);

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                logger.Error("Error opening FriendRequestsPage dialog from lobby.", ex);

                MessageBox.Show(
                    Lang.addFriendServiceError,
                    Lang.btnRequests,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private Window BuildPageDialog(Page page, string title, double width, double height)
        {
            var frame = new Frame
            {
                Content = page,
                NavigationUIVisibility = NavigationUIVisibility.Hidden
            };

            return new Window
            {
                Title = title,
                Content = frame,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = hostWindow
            };
        }

        private async Task OpenDrawerInternalAsync()
        {
            if (IsDrawerOpen)
            {
                return;
            }

            if (mainArea != null)
            {
                mainArea.Effect = overlayBlur;
            }

            var blurAnimation = new DoubleAnimation(
                overlayBlur.Radius,
                OVERLAY_BLUR_TARGET_RADIUS,
                TimeSpan.FromMilliseconds(DRAWER_ANIM_IN_MS))
            {
                EasingFunction = new QuadraticEase()
            };

            overlayBlur.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);

            await RefreshFriendsSafeAsync();

            if (drawerTranslate != null)
            {
                drawerTranslate.X = FRIENDS_DRAWER_INITIAL_TRANSLATE_X;
            }

            if (drawerHost != null)
            {
                drawerHost.Opacity = 0;
                drawerHost.Visibility = Visibility.Visible;
            }

            TryBeginStoryboard("sbOpenFriendsDrawer");
        }

        private void CloseDrawerInternal()
        {
            if (!IsDrawerOpen)
            {
                return;
            }

            TryBeginStoryboardWithCompleted("sbCloseFriendsDrawer", OnDrawerClosedStoryboardCompleted);

            var blurAnimation = new DoubleAnimation(
                overlayBlur.Radius,
                OVERLAY_BLUR_INITIAL_RADIUS,
                TimeSpan.FromMilliseconds(DRAWER_ANIM_OUT_MS))
            {
                EasingFunction = new QuadraticEase()
            };

            overlayBlur.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);
        }

        private void OnDrawerClosedStoryboardCompleted()
        {
            if (drawerHost != null)
            {
                drawerHost.Visibility = Visibility.Collapsed;
            }

            if (mainArea != null)
            {
                mainArea.Effect = null;
            }
        }

        private void TryBeginStoryboard(string key)
        {
            try
            {
                if (hostWindow == null)
                {
                    return;
                }

                var storyboard = hostWindow.FindResource(key) as Storyboard;
                storyboard?.Begin(hostWindow, true);
            }
            catch (Exception ex)
            {
                logger.Warn("TryBeginStoryboard error.", ex);
            }
        }

        private void TryBeginStoryboardWithCompleted(string key, Action onCompleted)
        {
            try
            {
                if (hostWindow == null)
                {
                    onCompleted?.Invoke();
                    return;
                }

                var storyboard = hostWindow.FindResource(key) as Storyboard;
                if (storyboard == null)
                {
                    onCompleted?.Invoke();
                    return;
                }

                void Handler(object s, EventArgs e)
                {
                    storyboard.Completed -= Handler;
                    onCompleted?.Invoke();
                }

                storyboard.Completed += Handler;
                storyboard.Begin(hostWindow, true);
            }
            catch (Exception ex)
            {
                logger.Warn("TryBeginStoryboardWithCompleted error.", ex);
                onCompleted?.Invoke();
            }
        }

        private async Task RefreshFriendsSafeAsync()
        {
            try
            {
                await AppServices.Friends.ManualRefreshAsync();
            }
            catch (Exception ex)
            {
                logger.Error("Error refreshing friends list.", ex);
            }
        }

        private void OnFriendsUpdated(System.Collections.Generic.IReadOnlyList<FriendItem> list, int pending)
        {
            ui.Ui(() =>
            {
                friendItems.Clear();

                foreach (var friend in list)
                {
                    friendItems.Add(friend);
                }

                pendingRequestsCount = pending;
                UpdateDrawerUi();
            });
        }

        private void UpdateDrawerUi()
        {
            if (emptyPanel != null)
            {
                emptyPanel.Visibility = friendItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (friendsList != null)
            {
                friendsList.Visibility = friendItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            if (requestsCountText != null)
            {
                requestsCountText.Text = pendingRequestsCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void FriendsListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (friendsList == null)
                {
                    return;
                }

                var origin = e.OriginalSource as DependencyObject;
                var container = FindAncestor<ListBoxItem>(origin);
                if (container == null)
                {
                    return;
                }

                friendsList.SelectedItem = container.DataContext;
            }
            catch (Exception ex)
            {
                logger.Error("Error selecting friend item on right click.", ex);
            }
        }

        private void FriendsListContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                if (friendsList == null)
                {
                    e.Handled = true;
                    return;
                }

                var friend = friendsList.SelectedItem as FriendItem;
                if (friend == null)
                {
                    e.Handled = true;
                    return;
                }

                if (!state.CurrentLobbyId.HasValue || string.IsNullOrWhiteSpace(state.CurrentAccessCode))
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

                inviteItem.Click += InviteMenuItemClick;

                menu.Items.Add(inviteItem);

                friendsList.ContextMenu = menu;
            }
            catch (Exception ex)
            {
                logger.Error("Error building friends context menu.", ex);
                e.Handled = true;
            }
        }

        private async void InviteMenuItemClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var friend = menuItem?.CommandParameter as FriendItem;
            if (friend == null)
            {
                return;
            }

            string token = SessionTokenProvider.GetTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            if (!state.CurrentLobbyId.HasValue || string.IsNullOrWhiteSpace(state.CurrentAccessCode))
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
                    LobbyCode = state.CurrentAccessCode
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
                logger.Warn("Friend fault while sending lobby invite email.", ex);

                MessageBox.Show(
                    ex.Detail != null ? (ex.Detail.Code + ": " + ex.Detail.Message) : ERROR_INVITE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                logger.Error("Communication error while sending lobby invite email.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                logger.Error("Unexpected error while sending lobby invite email.", ex);

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

        private void CloseClientSafely(ICommunicationObject client, string context)
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
                logger.Warn(string.Format(CultureInfo.InvariantCulture, "CloseClientSafely communication error. Context={0}", context), ex);
                TryAbort(client, context);
            }
            catch (TimeoutException ex)
            {
                logger.Warn(string.Format(CultureInfo.InvariantCulture, "CloseClientSafely timeout. Context={0}", context), ex);
                TryAbort(client, context);
            }
            catch (Exception ex)
            {
                logger.Warn(string.Format(CultureInfo.InvariantCulture, "CloseClientSafely unexpected error. Context={0}", context), ex);
                TryAbort(client, context);
            }
        }

        private void TryAbort(ICommunicationObject client, string context)
        {
            try
            {
                client.Abort();
            }
            catch (Exception ex)
            {
                logger.Warn(string.Format(CultureInfo.InvariantCulture, "Abort failed. Context={0}", context), ex);
            }
        }

        public void Dispose()
        {
            AppServices.Friends.FriendsUpdated -= OnFriendsUpdated;

            if (friendsList != null)
            {
                friendsList.PreviewMouseRightButtonDown -= FriendsListPreviewMouseRightButtonDown;
                friendsList.ContextMenuOpening -= FriendsListContextMenuOpening;
            }
        }
    }
}
