using log4net;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.Infrastructure.Faults;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Pages
{
    public partial class FriendRequestsPage : Page
    {
        private const string LOG_CTX_REFRESH = "FriendRequestsPage.RefreshAsync";
        private const string LOG_CTX_ACCEPT = "FriendRequestsPage.Accept";
        private const string LOG_CTX_REJECT = "FriendRequestsPage.Reject";
        private const string LOG_CTX_CANCEL = "FriendRequestsPage.Cancel";
        private const string LOG_CTX_CLOSE = "FriendRequestsPage.Close";

        private const string KEY_LBL_FRIEND_REQUESTS_TITLE = "lblFriendRequestsTitle";
        private const string KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR = "lblFriendRequestsConnectionError";

        private const string KEY_BTN_FRIEND_REQUESTS_ACCEPT_TITLE = "btnFriendRequestsAccept";
        private const string KEY_BTN_FRIEND_REQUESTS_REJECT_TITLE = "btnFriendRequestsReject";
        private const string KEY_BTN_FRIEND_REQUESTS_CANCEL_TITLE = "btnFriendRequestsCancel";

        private const int AVATAR_DECODE_WIDTH = 36;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(FriendRequestsPage));

        private readonly FriendServiceClient friendServiceClient;
        private readonly string authToken;

        private readonly ObservableCollection<RequestViewModel> incomingRequests =
            new ObservableCollection<RequestViewModel>();

        private readonly ObservableCollection<RequestViewModel> outgoingRequests =
            new ObservableCollection<RequestViewModel>();

        public event EventHandler FriendsChanged;
        public event EventHandler CloseRequested;

        public FriendRequestsPage(FriendServiceClient client, string token)
        {
            InitializeComponent();

            friendServiceClient = client ?? throw new ArgumentNullException(nameof(client));
            authToken = token ?? string.Empty;

            lstIncoming.ItemsSource = incomingRequests;
            lstOutgoing.ItemsSource = outgoingRequests;

            Loaded += async (_, __) => await RefreshAsync();
        }

        private static string Localize(string key)
        {
            var safeKey = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(safeKey))
            {
                return string.Empty;
            }

            var value = Lang.ResourceManager.GetString(safeKey, Lang.Culture);
            return string.IsNullOrWhiteSpace(value) ? safeKey : value;
        }

        private static string ResolveFaultMessage(FriendService.ServiceFault fault)
        {
            var key = fault == null ? string.Empty : (fault.Message ?? string.Empty);
            return FaultKeyMessageResolver.Resolve(key, Localize);
        }

        private static string GetFaultCode(FriendService.ServiceFault fault) =>
            fault == null ? string.Empty : (fault.Code ?? string.Empty);

        private static string GetFaultKey(FriendService.ServiceFault fault) =>
            fault == null ? string.Empty : (fault.Message ?? string.Empty);

        private static ImageSource DefaultAvatar()
        {
            return UiImageHelper.DefaultAvatar(AVATAR_DECODE_WIDTH);
        }

        private static string ResolveDisplayName(AccountMini mini, int fallbackAccountId)
        {
            if (!string.IsNullOrWhiteSpace(mini?.DisplayName))
            {
                return mini.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(mini?.Email))
            {
                return mini.Email;
            }

            return "Cuenta #" + fallbackAccountId;
        }

        private async Task RefreshAsync()
        {
            Logger.InfoFormat("{0}: start.", LOG_CTX_REFRESH);

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var response = await friendServiceClient.ListFriendsAsync(new ListFriendsRequest
                {
                    Token = authToken,
                    IncludePendingIncoming = true,
                    IncludePendingOutgoing = true
                });

                incomingRequests.Clear();
                outgoingRequests.Clear();

                var incoming = response?.PendingIncoming ?? Array.Empty<FriendRequestSummary>();
                var outgoing = response?.PendingOutgoing ?? Array.Empty<FriendRequestSummary>();

                var accountIds = new System.Collections.Generic.HashSet<int>();
                for (var i = 0; i < incoming.Length; i++) accountIds.Add(incoming[i].FromAccountId);
                for (var i = 0; i < outgoing.Length; i++) accountIds.Add(outgoing[i].ToAccountId);

                var accountsById = new System.Collections.Generic.Dictionary<int, AccountMini>();
                if (accountIds.Count > 0)
                {
                    var getReq = new GetAccountsByIdsRequest
                    {
                        Token = authToken,
                        AccountIds = new int[accountIds.Count]
                    };

                    accountIds.CopyTo(getReq.AccountIds, 0);

                    var info = await friendServiceClient.GetAccountsByIdsAsync(getReq);
                    var minis = info?.Accounts ?? Array.Empty<AccountMini>();

                    for (var i = 0; i < minis.Length; i++)
                    {
                        accountsById[minis[i].AccountId] = minis[i];
                    }
                }

                for (var i = 0; i < incoming.Length; i++)
                {
                    var summary = incoming[i];
                    accountsById.TryGetValue(summary.FromAccountId, out var mini);

                    var vm = new RequestViewModel
                    {
                        FriendRequestId = summary.FriendRequestId,
                        OtherAccountId = summary.FromAccountId,
                        DisplayName = ResolveDisplayName(mini, summary.FromAccountId),
                        Subtitle = Lang.statusPendingIn,
                        Avatar = DefaultAvatar(),
                        IsIncoming = true,
                        HasProfileImage = mini != null && mini.HasProfileImage,
                        ProfileImageCode = mini == null ? string.Empty : (mini.ProfileImageCode ?? string.Empty)
                    };

                    incomingRequests.Add(vm);

                    if (vm.HasProfileImage && !string.IsNullOrWhiteSpace(vm.ProfileImageCode))
                    {
                        _ = LoadAvatarAsync(vm);
                    }
                }

                for (var i = 0; i < outgoing.Length; i++)
                {
                    var summary = outgoing[i];
                    accountsById.TryGetValue(summary.ToAccountId, out var mini);

                    var vm = new RequestViewModel
                    {
                        FriendRequestId = summary.FriendRequestId,
                        OtherAccountId = summary.ToAccountId,
                        DisplayName = ResolveDisplayName(mini, summary.ToAccountId),
                        Subtitle = Lang.statusPendingOut,
                        Avatar = DefaultAvatar(),
                        IsIncoming = false,
                        HasProfileImage = mini != null && mini.HasProfileImage,
                        ProfileImageCode = mini == null ? string.Empty : (mini.ProfileImageCode ?? string.Empty)
                    };

                    outgoingRequests.Add(vm);

                    if (vm.HasProfileImage && !string.IsNullOrWhiteSpace(vm.ProfileImageCode))
                    {
                        _ = LoadAvatarAsync(vm);
                    }
                }

                Logger.InfoFormat(
                    "{0}: completed. Incoming={1}, Outgoing={2}.",
                    LOG_CTX_REFRESH,
                    incomingRequests.Count,
                    outgoingRequests.Count);
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "{0}: service fault. Code={1}, Key={2}.",
                    LOG_CTX_REFRESH,
                    GetFaultCode(ex.Detail),
                    GetFaultKey(ex.Detail));

                MessageBox.Show(
                    ResolveFaultMessage(ex.Detail),
                    Localize(KEY_LBL_FRIEND_REQUESTS_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (TimeoutException ex)
            {
                Logger.Error(LOG_CTX_REFRESH, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_LBL_FRIEND_REQUESTS_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                Logger.Error(LOG_CTX_REFRESH, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_LBL_FRIEND_REQUESTS_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_CTX_REFRESH, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_LBL_FRIEND_REQUESTS_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async Task LoadAvatarAsync(RequestViewModel vm)
        {
            if (vm == null || !vm.HasProfileImage || string.IsNullOrWhiteSpace(vm.ProfileImageCode))
            {
                return;
            }

            try
            {
                ImageSource image = await ProfileImageCache.Current.GetOrFetchAsync(
                    vm.OtherAccountId,
                    vm.ProfileImageCode,
                    AVATAR_DECODE_WIDTH,
                    async () =>
                    {
                        var resp = await friendServiceClient.GetProfileImageAsync(
                            new FriendService.GetProfileImageRequest
                            {
                                Token = authToken,
                                AccountId = vm.OtherAccountId,
                                ProfileImageCode = vm.ProfileImageCode
                            });

                        return resp?.ImageBytes ?? Array.Empty<byte>();
                    });

                if (image != null)
                {
                    vm.Avatar = image;
                    vm.NotifyAvatarChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("FriendRequestsPage.LoadAvatarAsync: best-effort failed.", ex);
            }
        }

        private async void BtnAcceptClick(object sender, RoutedEventArgs e)
        {
            var viewModel = (sender as Button)?.DataContext as RequestViewModel;
            if (viewModel == null)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await friendServiceClient.AcceptFriendRequestAsync(new AcceptFriendRequestRequest
                {
                    Token = authToken,
                    FriendRequestId = viewModel.FriendRequestId
                });

                incomingRequests.Remove(viewModel);

                Logger.InfoFormat(
                    "{0}: accepted. FriendRequestId={1}, OtherAccountId={2}.",
                    LOG_CTX_ACCEPT,
                    viewModel.FriendRequestId,
                    viewModel.OtherAccountId);

                FriendsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "{0}: service fault. FriendRequestId={1}, Code={2}, Key={3}.",
                    LOG_CTX_ACCEPT,
                    viewModel.FriendRequestId,
                    GetFaultCode(ex.Detail),
                    GetFaultKey(ex.Detail));

                MessageBox.Show(
                    ResolveFaultMessage(ex.Detail),
                    Localize(KEY_BTN_FRIEND_REQUESTS_ACCEPT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (TimeoutException ex)
            {
                Logger.Error(LOG_CTX_ACCEPT, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_ACCEPT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                Logger.Error(LOG_CTX_ACCEPT, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_ACCEPT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_CTX_ACCEPT, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_ACCEPT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void BtnRejectClick(object sender, RoutedEventArgs e)
        {
            var viewModel = (sender as Button)?.DataContext as RequestViewModel;
            if (viewModel == null)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await friendServiceClient.RejectFriendRequestAsync(new RejectFriendRequestRequest
                {
                    Token = authToken,
                    FriendRequestId = viewModel.FriendRequestId
                });

                incomingRequests.Remove(viewModel);

                Logger.InfoFormat(
                    "{0}: rejected. FriendRequestId={1}, OtherAccountId={2}.",
                    LOG_CTX_REJECT,
                    viewModel.FriendRequestId,
                    viewModel.OtherAccountId);

                FriendsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "{0}: service fault. FriendRequestId={1}, Code={2}, Key={3}.",
                    LOG_CTX_REJECT,
                    viewModel.FriendRequestId,
                    GetFaultCode(ex.Detail),
                    GetFaultKey(ex.Detail));

                MessageBox.Show(
                    ResolveFaultMessage(ex.Detail),
                    Localize(KEY_BTN_FRIEND_REQUESTS_REJECT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (TimeoutException ex)
            {
                Logger.Error(LOG_CTX_REJECT, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_REJECT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                Logger.Error(LOG_CTX_REJECT, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_REJECT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_CTX_REJECT, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_REJECT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            var viewModel = (sender as Button)?.DataContext as RequestViewModel;
            if (viewModel == null)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await friendServiceClient.RejectFriendRequestAsync(new RejectFriendRequestRequest
                {
                    Token = authToken,
                    FriendRequestId = viewModel.FriendRequestId
                });

                outgoingRequests.Remove(viewModel);

                Logger.InfoFormat(
                    "{0}: cancelled. FriendRequestId={1}, OtherAccountId={2}.",
                    LOG_CTX_CANCEL,
                    viewModel.FriendRequestId,
                    viewModel.OtherAccountId);

                FriendsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "{0}: service fault. FriendRequestId={1}, Code={2}, Key={3}.",
                    LOG_CTX_CANCEL,
                    viewModel.FriendRequestId,
                    GetFaultCode(ex.Detail),
                    GetFaultKey(ex.Detail));

                MessageBox.Show(
                    ResolveFaultMessage(ex.Detail),
                    Localize(KEY_BTN_FRIEND_REQUESTS_CANCEL_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (TimeoutException ex)
            {
                Logger.Error(LOG_CTX_CANCEL, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_CANCEL_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                Logger.Error(LOG_CTX_CANCEL, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_CANCEL_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_CTX_CANCEL, ex);

                MessageBox.Show(
                    Localize(KEY_LBL_FRIEND_REQUESTS_CONNECTION_ERROR),
                    Localize(KEY_BTN_FRIEND_REQUESTS_CANCEL_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            Logger.InfoFormat("{0}: close requested.", LOG_CTX_CLOSE);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private sealed class RequestViewModel : INotifyPropertyChanged
        {
            private ImageSource avatar;

            public int FriendRequestId { get; set; }
            public int OtherAccountId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string Subtitle { get; set; } = string.Empty;

            public bool HasProfileImage { get; set; }
            public string ProfileImageCode { get; set; } = string.Empty;

            public ImageSource Avatar
            {
                get => avatar;
                set
                {
                    avatar = value;
                    OnPropertyChanged(nameof(Avatar));
                }
            }

            public bool IsIncoming { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;

            public void NotifyAvatarChanged()
            {
                OnPropertyChanged(nameof(Avatar));
            }

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
