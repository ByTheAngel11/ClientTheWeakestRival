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
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;

namespace WPFTheWeakestRival.Pages
{
    public partial class FriendRequestsPage : Page
    {
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
                    var getReq = new GetAccountsByIdsRequest { Token = authToken, AccountIds = new int[accountIds.Count] };
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
                    AccountMini mini;
                    accountsById.TryGetValue(summary.FromAccountId, out mini);

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
                    AccountMini mini;
                    accountsById.TryGetValue(summary.ToAccountId, out mini);

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
            }
            catch (FaultException<ServiceFault> ex)
            {
                MessageBox.Show(ex.Detail.Code + ": " + ex.Detail.Message, "Solicitudes", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + ex.Message, "Solicitudes", MessageBoxButton.OK, MessageBoxImage.Error);
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

                var handler = FriendsChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                MessageBox.Show(ex.Detail.Code + ": " + ex.Detail.Message, "Aceptar solicitud", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + ex.Message, "Aceptar solicitud", MessageBoxButton.OK, MessageBoxImage.Error);
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

                var handler = FriendsChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                MessageBox.Show(ex.Detail.Code + ": " + ex.Detail.Message, "Rechazar solicitud", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + ex.Message, "Rechazar solicitud", MessageBoxButton.OK, MessageBoxImage.Error);
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

                var handler = FriendsChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                MessageBox.Show(ex.Detail.Code + ": " + ex.Detail.Message, "Cancelar solicitud", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + ex.Message, "Cancelar solicitud", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
