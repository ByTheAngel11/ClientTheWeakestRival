using log4net;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.ServiceModel;
using System.Threading;
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
    public partial class AddFriendPage : Page
    {
        private const int SEARCH_DEBOUNCE_MILLISECONDS = 300;
        private const int MAX_SEARCH_RESULTS = 20;
        private const int AVATAR_DECODE_WIDTH = 36;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(AddFriendPage));

        private readonly FriendServiceClient friendServiceClient;
        private readonly string authToken;

        private readonly ObservableCollection<FriendSearchResultVm> results =
            new ObservableCollection<FriendSearchResultVm>();

        private CancellationTokenSource debounceCancellation;
        private int lastSearchRequestId;

        public event EventHandler FriendsUpdated;
        public event EventHandler CloseRequested;

        public AddFriendPage(FriendServiceClient client, string token)
        {
            InitializeComponent();

            friendServiceClient = client ?? throw new ArgumentNullException(nameof(client));
            authToken = token ?? string.Empty;

            lstResults.ItemsSource = results;

            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelAndDisposeDebounce();
        }

        private void CancelAndDisposeDebounce()
        {
            var old = Interlocked.Exchange(ref debounceCancellation, null);
            if (old == null)
            {
                return;
            }

            try
            {
                old.Cancel();
            }
            catch (Exception ex)
            {
                Logger.Debug("AddFriendPage.CancelAndDisposeDebounce: cancel failed.", ex);
            }

            try
            {
                old.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug("AddFriendPage.CancelAndDisposeDebounce: dispose failed.", ex);
            }
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

        private static ImageSource DefaultAvatar()
        {
            return UiImageHelper.DefaultAvatar(AVATAR_DECODE_WIDTH);
        }

        private async void BtnSearchClick(object sender, RoutedEventArgs e)
        {
            var requestId = Interlocked.Increment(ref lastSearchRequestId);
            await SearchAsync(txtSearchQuery.Text, requestId);
        }

        private async void TxtSearchQueryTextChanged(object sender, TextChangedEventArgs e)
        {
            CancelAndDisposeDebounce();

            var cts = new CancellationTokenSource();
            debounceCancellation = cts;

            CancellationToken ct = cts.Token;
            var requestId = Interlocked.Increment(ref lastSearchRequestId);

            try
            {
                await Task.Delay(SEARCH_DEBOUNCE_MILLISECONDS, ct);

                if (!ct.IsCancellationRequested)
                {
                    await SearchAsync(txtSearchQuery.Text, requestId);
                }
            }
            catch (TaskCanceledException ex)
            {
                Logger.Debug("AddFriendPage.TxtSearchQueryTextChanged: debounce canceled.", ex);
            }
            catch (Exception ex)
            {
                Logger.Warn("AddFriendPage.TxtSearchQueryTextChanged: unexpected error.", ex);
            }
            finally
            {
                try
                {
                    cts.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Debug("AddFriendPage.TxtSearchQueryTextChanged: dispose failed.", ex);
                }

                if (ReferenceEquals(debounceCancellation, cts))
                {
                    debounceCancellation = null;
                }
            }
        }

        private async Task SearchAsync(string query, int requestId)
        {
            string trimmedQuery = NormalizeQuery(query);
            if (string.IsNullOrWhiteSpace(trimmedQuery))
            {
                ClearResultsAndStatus();
                return;
            }

            try
            {
                BeginSearchingUi();

                SearchAccountsResponse response = await PerformSearchAsync(trimmedQuery);

                if (IsStaleRequest(requestId))
                {
                    return;
                }

                PopulateResults(response);
                UpdateStatusFromResultsCount();
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                HandleSearchFault(ex);
            }
            catch (TimeoutException ex)
            {
                HandleSearchConnectivityError("AddFriendPage.SearchAsync: timeout.", ex);
            }
            catch (CommunicationException ex)
            {
                HandleSearchConnectivityError("AddFriendPage.SearchAsync: communication error.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("AddFriendPage.SearchAsync: unexpected error.", ex);

                MessageBox.Show(
                    Lang.addFriendServiceError,
                    Lang.addFriendSearchTooltip,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                lblStatus.Text = Lang.addFriendServiceError;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private static string NormalizeQuery(string query)
        {
            return (query ?? string.Empty).Trim();
        }

        private bool IsStaleRequest(int requestId)
        {
            return requestId != Volatile.Read(ref lastSearchRequestId);
        }

        private void ClearResultsAndStatus()
        {
            results.Clear();
            lblStatus.Text = string.Empty;
        }

        private void BeginSearchingUi()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            lblStatus.Text = Lang.searching;
        }

        private Task<SearchAccountsResponse> PerformSearchAsync(string trimmedQuery)
        {
            var request = new SearchAccountsRequest
            {
                Token = authToken,
                Query = trimmedQuery,
                MaxResults = MAX_SEARCH_RESULTS
            };

            return friendServiceClient.SearchAccountsAsync(request);
        }

        private void PopulateResults(SearchAccountsResponse response)
        {
            results.Clear();

            SearchAccountItem[] serviceResults = response?.Results ?? Array.Empty<SearchAccountItem>();
            foreach (SearchAccountItem item in serviceResults)
            {
                FriendSearchResultVm vm = MapToViewModel(item);
                results.Add(vm);

                TryQueueAvatarLoad(vm);
            }
        }

        private static FriendSearchResultVm MapToViewModel(SearchAccountItem item)
        {
            string displayName = string.IsNullOrWhiteSpace(item.DisplayName)
                ? (item.Email ?? string.Empty)
                : item.DisplayName;

            return new FriendSearchResultVm
            {
                AccountId = item.AccountId,
                DisplayName = displayName,
                Email = item.Email ?? string.Empty,
                HasProfileImage = item.HasProfileImage,
                ProfileImageCode = item.ProfileImageCode ?? string.Empty,
                Avatar = DefaultAvatar(),
                IsFriend = item.IsFriend,
                HasPendingOutgoing = item.HasPendingOutgoing,
                HasPendingIncoming = item.HasPendingIncoming,
                PendingIncomingRequestId = item.PendingIncomingRequestId
            };
        }

        private void TryQueueAvatarLoad(FriendSearchResultVm vm)
        {
            if (vm == null)
            {
                return;
            }

            if (!vm.HasProfileImage || string.IsNullOrWhiteSpace(vm.ProfileImageCode))
            {
                return;
            }

            _ = LoadAvatarAsync(vm);
        }

        private void UpdateStatusFromResultsCount()
        {
            lblStatus.Text = results.Count == 0
                ? Lang.noResults
                : string.Format(Lang.results, results.Count);
        }

        private void HandleSearchFault(FaultException<FriendService.ServiceFault> ex)
        {
            Logger.WarnFormat(
                "AddFriendPage.SearchAsync: service fault. Code={0}, Key={1}",
                ex.Detail == null ? string.Empty : ex.Detail.Code,
                ex.Detail == null ? string.Empty : ex.Detail.Message);

            MessageBox.Show(
                ResolveFaultMessage(ex.Detail),
                Lang.addFriendSearchTooltip,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            lblStatus.Text = Lang.addFriendServiceError;
        }

        private void HandleSearchConnectivityError(string logMessage, Exception ex)
        {
            Logger.Error(logMessage, ex);

            MessageBox.Show(
                Lang.noConnection,
                Lang.addFriendSearchTooltip,
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            lblStatus.Text = Lang.noConnection;
        }

        private async Task LoadAvatarAsync(FriendSearchResultVm vm)
        {
            if (vm == null || !vm.HasProfileImage || string.IsNullOrWhiteSpace(vm.ProfileImageCode))
            {
                return;
            }

            try
            {
                ImageSource image = await ProfileImageCache.Current.GetOrFetchAsync(
                    vm.AccountId,
                    vm.ProfileImageCode,
                    AVATAR_DECODE_WIDTH,
                    async () =>
                    {
                        var resp = await friendServiceClient.GetProfileImageAsync(new FriendService.GetProfileImageRequest
                        {
                            Token = authToken,
                            AccountId = vm.AccountId,
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
                Logger.Debug("AddFriendPage.LoadAvatarAsync: best-effort failed.", ex);
            }
        }

        private async void BtnAddClick(object sender, RoutedEventArgs e)
        {
            var viewModel = (sender as Button)?.DataContext as FriendSearchResultVm;
            if (viewModel == null || !viewModel.CanAdd)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var response = await friendServiceClient.SendFriendRequestAsync(new SendFriendRequestRequest
                {
                    Token = authToken,
                    TargetAccountId = viewModel.AccountId
                });

                if (response != null && response.Status == FriendRequestStatus.Accepted)
                {
                    viewModel.IsFriend = true;
                    viewModel.HasPendingOutgoing = false;
                    viewModel.HasPendingIncoming = false;
                    viewModel.PendingIncomingRequestId = null;
                    viewModel.NotifyStateChanged();

                    FriendsUpdated?.Invoke(this, EventArgs.Empty);

                    MessageBox.Show(
                        string.Format(Lang.nowFriends, viewModel.DisplayName),
                        Lang.addFriendTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                viewModel.HasPendingOutgoing = true;
                viewModel.NotifyStateChanged();
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "AddFriendPage.SendFriendRequest: service fault. Code={0}, Key={1}",
                    ex.Detail == null ? string.Empty : ex.Detail.Code,
                    ex.Detail == null ? string.Empty : ex.Detail.Message);

                MessageBox.Show(
                    ResolveFaultMessage(ex.Detail),
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("AddFriendPage.SendFriendRequest: timeout.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("AddFriendPage.SendFriendRequest: communication error.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("AddFriendPage.SendFriendRequest: unexpected error.", ex);

                MessageBox.Show(
                    Lang.addFriendServiceError,
                    Lang.addFriendTitle,
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
            var viewModel = (sender as Button)?.DataContext as FriendSearchResultVm;
            if (viewModel == null || !viewModel.HasPendingIncoming || !viewModel.PendingIncomingRequestId.HasValue)
            {
                return;
            }

            var friendRequestId = viewModel.PendingIncomingRequestId.Value;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await friendServiceClient.AcceptFriendRequestAsync(new AcceptFriendRequestRequest
                {
                    Token = authToken,
                    FriendRequestId = friendRequestId
                });

                viewModel.IsFriend = true;
                viewModel.HasPendingIncoming = false;
                viewModel.PendingIncomingRequestId = null;
                viewModel.HasPendingOutgoing = false;
                viewModel.NotifyStateChanged();

                FriendsUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "AddFriendPage.AcceptFriendRequest: service fault. Code={0}, Key={1}",
                    ex.Detail == null ? string.Empty : ex.Detail.Code,
                    ex.Detail == null ? string.Empty : ex.Detail.Message);

                MessageBox.Show(
                    ResolveFaultMessage(ex.Detail),
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("AddFriendPage.AcceptFriendRequest: timeout.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("AddFriendPage.AcceptFriendRequest: communication error.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("AddFriendPage.AcceptFriendRequest: unexpected error.", ex);

                MessageBox.Show(
                    Lang.addFriendServiceError,
                    Lang.addFriendTitle,
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
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Used via WPF binding.")]
        private sealed class FriendSearchResultVm : INotifyPropertyChanged
        {
            public int AccountId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;

            public bool HasProfileImage { get; set; }
            public string ProfileImageCode { get; set; } = string.Empty;

            public ImageSource Avatar { get; set; }

            public bool IsFriend { get; set; }
            public bool HasPendingOutgoing { get; set; }
            public bool HasPendingIncoming { get; set; }
            public int? PendingIncomingRequestId { get; set; }

            public bool CanAdd => !IsFriend && !HasPendingOutgoing && !HasPendingIncoming;
            public bool ShowAccept => HasPendingIncoming && PendingIncomingRequestId.HasValue;

            public string AddButtonText
            {
                get
                {
                    if (IsFriend) return Lang.btnFriends;
                    if (HasPendingOutgoing) return Lang.statusPendingOut;
                    if (HasPendingIncoming) return Lang.statusPendingIn;
                    return Lang.addFriendAccept;
                }
            }

            public string StateText
            {
                get
                {
                    if (IsFriend) return Lang.stateAlreadyFriends;
                    if (HasPendingOutgoing) return Lang.statusPendingOut;
                    if (HasPendingIncoming) return Lang.statusPendingIn;
                    return Lang.stateSuggested;
                }
            }

            public void NotifyStateChanged()
            {
                OnPropertyChanged(nameof(CanAdd));
                OnPropertyChanged(nameof(ShowAccept));
                OnPropertyChanged(nameof(AddButtonText));
                OnPropertyChanged(nameof(StateText));
            }

            public void NotifyAvatarChanged()
            {
                OnPropertyChanged(nameof(Avatar));
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}