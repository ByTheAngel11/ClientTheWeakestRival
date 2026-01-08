using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using log4net;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
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

        private static readonly ILog Logger = LogManager.GetLogger(typeof(FriendRequestsPage));

        private readonly FriendServiceClient friendServiceClient;
        private readonly string authToken;

        private readonly ObservableCollection<RequestViewModel> incomingRequests = new ObservableCollection<RequestViewModel>();
        private readonly ObservableCollection<RequestViewModel> outgoingRequests = new ObservableCollection<RequestViewModel>();

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

                var incoming = response.PendingIncoming ?? Array.Empty<FriendRequestSummary>();
                var outgoing = response.PendingOutgoing ?? Array.Empty<FriendRequestSummary>();

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
                    var minis = info.Accounts ?? Array.Empty<AccountMini>();
                    for (var i = 0; i < minis.Length; i++)
                    {
                        accountsById[minis[i].AccountId] = minis[i];
                    }
                }

                for (var i = 0; i < incoming.Length; i++)
                {
                    var summary = incoming[i];
                    accountsById.TryGetValue(summary.FromAccountId, out AccountMini mini);

                    var displayName = mini != null && !string.IsNullOrWhiteSpace(mini.DisplayName)
                        ? mini.DisplayName
                        : "Cuenta #" + summary.FromAccountId;

                    var avatar = UiImageHelper.TryCreateFromUrlOrPath(mini != null ? mini.AvatarUrl : null, 36)
                                 ?? UiImageHelper.DefaultAvatar(36);

                    incomingRequests.Add(new RequestViewModel
                    {
                        FriendRequestId = summary.FriendRequestId,
                        OtherAccountId = summary.FromAccountId,
                        DisplayName = displayName,
                        Subtitle = "Te ha enviado una solicitud",
                        Avatar = avatar,
                        IsIncoming = true
                    });
                }

                for (var i = 0; i < outgoing.Length; i++)
                {
                    var summary = outgoing[i];
                    accountsById.TryGetValue(summary.ToAccountId, out AccountMini mini);

                    var displayName = mini != null && !string.IsNullOrWhiteSpace(mini.DisplayName)
                        ? mini.DisplayName
                        : "Cuenta #" + summary.ToAccountId;

                    var avatar = UiImageHelper.TryCreateFromUrlOrPath(mini != null ? mini.AvatarUrl : null, 36)
                                 ?? UiImageHelper.DefaultAvatar(36);

                    outgoingRequests.Add(new RequestViewModel
                    {
                        FriendRequestId = summary.FriendRequestId,
                        OtherAccountId = summary.ToAccountId,
                        DisplayName = displayName,
                        Subtitle = "Solicitud enviada",
                        Avatar = avatar,
                        IsIncoming = false
                    });
                }

                Logger.InfoFormat(
                    "{0}: completed. Incoming={1}, Outgoing={2}.",
                    LOG_CTX_REFRESH,
                    incomingRequests.Count,
                    outgoingRequests.Count);
            }
            catch (FaultException<ServiceFault> ex)
            {
                string faultCode = GetFaultCode(ex.Detail);
                string faultKey = GetFaultKey(ex.Detail);

                Logger.WarnFormat(
                    "{0}: service fault. Code={1}, Key={2}.",
                    LOG_CTX_REFRESH,
                    faultCode,
                    faultKey);

                string uiMessage = ResolveFaultMessage(ex.Detail);

                MessageBox.Show(
                    uiMessage,
                    Localize(KEY_LBL_FRIEND_REQUESTS_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            finally
            {
                Mouse.OverrideCursor = null;
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

                var handler = FriendsChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                string faultCode = GetFaultCode(ex.Detail);
                string faultKey = GetFaultKey(ex.Detail);

                Logger.WarnFormat(
                    "{0}: service fault. FriendRequestId={1}, Code={2}, Key={3}.",
                    LOG_CTX_ACCEPT,
                    viewModel.FriendRequestId,
                    faultCode,
                    faultKey);

                string uiMessage = ResolveFaultMessage(ex.Detail);

                MessageBox.Show(
                    uiMessage,
                    Localize(KEY_BTN_FRIEND_REQUESTS_ACCEPT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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

                var handler = FriendsChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                string faultCode = GetFaultCode(ex.Detail);
                string faultKey = GetFaultKey(ex.Detail);

                Logger.WarnFormat(
                    "{0}: service fault. FriendRequestId={1}, Code={2}, Key={3}.",
                    LOG_CTX_REJECT,
                    viewModel.FriendRequestId,
                    faultCode,
                    faultKey);

                string uiMessage = ResolveFaultMessage(ex.Detail);

                MessageBox.Show(
                    uiMessage,
                    Localize(KEY_BTN_FRIEND_REQUESTS_REJECT_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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

                var handler = FriendsChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                string faultCode = GetFaultCode(ex.Detail);
                string faultKey = GetFaultKey(ex.Detail);

                Logger.WarnFormat(
                    "{0}: service fault. FriendRequestId={1}, Code={2}, Key={3}.",
                    LOG_CTX_CANCEL,
                    viewModel.FriendRequestId,
                    faultCode,
                    faultKey);

                string uiMessage = ResolveFaultMessage(ex.Detail);

                MessageBox.Show(
                    uiMessage,
                    Localize(KEY_BTN_FRIEND_REQUESTS_CANCEL_TITLE),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            Logger.InfoFormat("{0}: close requested.", LOG_CTX_CLOSE);

            var handler = CloseRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private class RequestViewModel : INotifyPropertyChanged
        {
            public int FriendRequestId { get; set; }
            public int OtherAccountId { get; set; }
            public string DisplayName { get; set; }
            public string Subtitle { get; set; }
            public ImageSource Avatar { get; set; }
            public bool IsIncoming { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private static string Localize(string key)
        {
            string safeKey = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(safeKey))
            {
                return string.Empty;
            }

            string value = Lang.ResourceManager.GetString(safeKey, Lang.Culture);
            return string.IsNullOrWhiteSpace(value) ? safeKey : value;
        }

        private static string ResolveFaultMessage(ServiceFault fault)
        {
            string key = fault == null ? string.Empty : fault.Message;
            return FaultKeyMessageResolver.Resolve(key, Localize);
        }

        private static string GetFaultCode(ServiceFault fault)
        {
            return fault == null ? string.Empty : (fault.Code ?? string.Empty);
        }

        private static string GetFaultKey(ServiceFault fault)
        {
            return fault == null ? string.Empty : (fault.Message ?? string.Empty);
        }
    }
}
