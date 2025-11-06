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
using log4net;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Pages
{
    public partial class AddFriendPage : Page
    {
        private const int SearchDebounceMilliseconds = 300;
        private const int MaxSearchResults = 20;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(AddFriendPage));

        private readonly FriendServiceClient friendServiceClient;
        private readonly string authToken;

        private readonly ObservableCollection<FriendSearchResultVm> results = new ObservableCollection<FriendSearchResultVm>();
        private CancellationTokenSource debounceCancellation;

        public event EventHandler FriendsUpdated;
        public event EventHandler CloseRequested;


        public AddFriendPage(FriendServiceClient client, string token)
        {
            InitializeComponent();
            friendServiceClient = client ?? throw new ArgumentNullException(nameof(client));
            authToken = token ?? string.Empty;
            lstResults.ItemsSource = results;
        }

        private async void BtnSearchClick(object sender, RoutedEventArgs e)
        {
            await SearchAsync(txtSearchQuery.Text);
        }

        private async void TxtSearchQueryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (debounceCancellation != null)
            {
                debounceCancellation.Cancel();
            }

            var currentCts = new CancellationTokenSource();
            debounceCancellation = currentCts;
            var ct = currentCts.Token;

            try
            {
                await Task.Delay(SearchDebounceMilliseconds, ct);

                if (!ct.IsCancellationRequested)
                {
                    var textBox = (TextBox)sender;
                    await SearchAsync(textBox.Text);
                }
            }
            catch (TaskCanceledException ex)
            {
                // Cancelación esperada por debounce
                Logger.Debug("Search debounce task was canceled.", ex);
            }
            finally
            {
                currentCts.Dispose();

                if (ReferenceEquals(debounceCancellation, currentCts))
                {
                    debounceCancellation = null;
                }
            }
        }

        private async Task SearchAsync(string query)
        {
            var trimmedQuery = (query ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(trimmedQuery))
            {
                results.Clear();
                lblStatus.Text = string.Empty;
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                lblStatus.Text = Lang.searching;

                var response = await friendServiceClient.SearchAccountsAsync(new SearchAccountsRequest
                {
                    Token = authToken,
                    Query = trimmedQuery,
                    MaxResults = MaxSearchResults
                });

                results.Clear();
                var serviceResults = response.Results ?? new SearchAccountItem[0];

                foreach (var item in serviceResults)
                {
                    var avatar = UiImageHelper.TryCreateFromUrlOrPath(item.AvatarUrl, 36)
                                 ?? UiImageHelper.DefaultAvatar(36);

                    results.Add(new FriendSearchResultVm
                    {
                        AccountId = item.AccountId,
                        DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Email : item.DisplayName,
                        Email = item.Email,
                        Avatar = avatar,
                        IsFriend = item.IsFriend,
                        HasPendingOutgoing = item.HasPendingOutgoing,
                        HasPendingIncoming = item.HasPendingIncoming,
                        PendingIncomingRequestId = item.PendingIncomingRequestId
                    });
                }

                lblStatus.Text = results.Count == 0
                    ? Lang.noResults
                    : string.Format(Lang.results, results.Count);
            }
            catch (FaultException<ServiceFault> ex)
            {
                MessageBox.Show(
                    ex.Detail.Code + ": " + ex.Detail.Message,
                    Lang.addFriendSearchTooltip,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                lblStatus.Text = Lang.addFriendServiceError;
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    Lang.noConnection + Environment.NewLine + ex.Message,
                    Lang.addFriendSearchTooltip,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                lblStatus.Text = Lang.noConnection;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void BtnAddClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            var viewModel = button.DataContext as FriendSearchResultVm;
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

                if (response.Status == FriendRequestStatus.Accepted)
                {
                    viewModel.IsFriend = true;
                    viewModel.HasPendingOutgoing = false;
                    viewModel.HasPendingIncoming = false;
                    viewModel.PendingIncomingRequestId = null;
                    viewModel.NotifyStateChanged();

                    var handler = FriendsUpdated;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }

                    MessageBox.Show(
                        string.Format(Lang.nowFriends, viewModel.DisplayName),
                        Lang.addFriendTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    viewModel.HasPendingOutgoing = true;
                    viewModel.NotifyStateChanged();
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                MessageBox.Show(
                    ex.Detail.Code + ": " + ex.Detail.Message,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    Lang.noConnection + Environment.NewLine + ex.Message,
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
            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            var viewModel = button.DataContext as FriendSearchResultVm;
            if (viewModel == null)
            {
                return;
            }

            if (!viewModel.HasPendingIncoming || !viewModel.PendingIncomingRequestId.HasValue)
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

                var handler = FriendsUpdated;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                MessageBox.Show(
                    ex.Detail.Code + ": " + ex.Detail.Message,
                    Lang.addFriendTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    Lang.noConnection + Environment.NewLine + ex.Message,
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
            var handler = CloseRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }


        [SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "Properties are used via WPF data binding in XAML.")]
        private sealed class FriendSearchResultVm : INotifyPropertyChanged
        {
            public int AccountId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public ImageSource Avatar { get; set; }

            public bool IsFriend { get; set; }
            public bool HasPendingOutgoing { get; set; }
            public bool HasPendingIncoming { get; set; }
            public int? PendingIncomingRequestId { get; set; }

            public bool CanAdd
            {
                get { return !IsFriend && !HasPendingOutgoing && !HasPendingIncoming; }
            }

            public bool ShowAccept
            {
                get { return HasPendingIncoming; }
            }

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
                OnPropertyChanged("CanAdd");
                OnPropertyChanged("ShowAccept");
                OnPropertyChanged("AddButtonText");
                OnPropertyChanged("StateText");
            }

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
    }
}
