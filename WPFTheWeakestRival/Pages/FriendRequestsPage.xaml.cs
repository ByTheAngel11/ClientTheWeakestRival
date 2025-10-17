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
        private readonly FriendServiceClient _client;
        private readonly string _token;

        private readonly ObservableCollection<RequestVm> _incoming = new ObservableCollection<RequestVm>();
        private readonly ObservableCollection<RequestVm> _outgoing = new ObservableCollection<RequestVm>();

        public event EventHandler FriendsChanged; // para que el Lobby refresque el drawer

        public FriendRequestsPage(FriendServiceClient client, string token)
        {
            InitializeComponent();
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _token = token ?? string.Empty;

            lstIncoming.ItemsSource = _incoming;
            lstOutgoing.ItemsSource = _outgoing;

            Loaded += async (_, __) => await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var resp = await _client.ListFriendsAsync(new ListFriendsRequest
                {
                    Token = _token,
                    IncludePendingIncoming = true,
                    IncludePendingOutgoing = true
                });

                _incoming.Clear();
                _outgoing.Clear();

                var inc = resp.PendingIncoming ?? new FriendRequestSummary[0];
                var outg = resp.PendingOutgoing ?? new FriendRequestSummary[0];

                // 1) recolectar IDs a enriquecer
                var ids = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < inc.Length; i++) ids.Add(inc[i].FromAccountId);
                for (int i = 0; i < outg.Length; i++) ids.Add(outg[i].ToAccountId);

                // 2) pedir nombres/avatars
                var map = new System.Collections.Generic.Dictionary<int, AccountMini>();
                if (ids.Count > 0)
                {
                    var req = new GetAccountsByIdsRequest { Token = _token, AccountIds = new int[ids.Count] };
                    ids.CopyTo(req.AccountIds, 0);

                    var info = await _client.GetAccountsByIdsAsync(req);
                    var arr = info.Accounts ?? new AccountMini[0];
                    for (int i = 0; i < arr.Length; i++)
                        map[arr[i].AccountId] = arr[i];
                }

                // 3) poblar Incoming
                for (int i = 0; i < inc.Length; i++)
                {
                    var r = inc[i];
                    AccountMini a;
                    map.TryGetValue(r.FromAccountId, out a);

                    var name = (a != null && !string.IsNullOrWhiteSpace(a.DisplayName)) ? a.DisplayName : ("Cuenta #" + r.FromAccountId);
                    var img = UiImageHelper.TryCreateFromUrlOrPath(a != null ? a.AvatarUrl : null, 36)
                              ?? UiImageHelper.DefaultAvatar(36);

                    _incoming.Add(new RequestVm
                    {
                        FriendRequestId = r.FriendRequestId,
                        OtherAccountId = r.FromAccountId,
                        DisplayName = name,
                        Subtitle = "Te ha enviado una solicitud",
                        Avatar = img,
                        IsIncoming = true
                    });
                }

                // 4) poblar Outgoing
                for (int i = 0; i < outg.Length; i++)
                {
                    var r = outg[i];
                    AccountMini a;
                    map.TryGetValue(r.ToAccountId, out a);

                    var name = (a != null && !string.IsNullOrWhiteSpace(a.DisplayName)) ? a.DisplayName : ("Cuenta #" + r.ToAccountId);
                    var img = UiImageHelper.TryCreateFromUrlOrPath(a != null ? a.AvatarUrl : null, 36)
                              ?? UiImageHelper.DefaultAvatar(36);

                    _outgoing.Add(new RequestVm
                    {
                        FriendRequestId = r.FriendRequestId,
                        OtherAccountId = r.ToAccountId,
                        DisplayName = name,
                        Subtitle = "Solicitud enviada",
                        Avatar = img,
                        IsIncoming = false
                    });
                }
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Solicitudes",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + cx.Message, "Solicitudes",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }


        private async void btnAccept_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.DataContext as RequestVm;
            if (vm == null) return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await _client.AcceptFriendRequestAsync(new AcceptFriendRequestRequest
                {
                    Token = _token,
                    FriendRequestId = vm.FriendRequestId
                });

                _incoming.Remove(vm);
                var ev = FriendsChanged; if (ev != null) ev(this, EventArgs.Empty); // actualiza drawer
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Aceptar solicitud",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + cx.Message, "Aceptar solicitud",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void btnReject_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.DataContext as RequestVm;
            if (vm == null) return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var resp = await _client.RejectFriendRequestAsync(new RejectFriendRequestRequest
                {
                    Token = _token,
                    FriendRequestId = vm.FriendRequestId
                });

                _incoming.Remove(vm); // rechazadas desaparecen de "recibidas"
                var ev = FriendsChanged; if (ev != null) ev(this, EventArgs.Empty);
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Rechazar solicitud",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + cx.Message, "Rechazar solicitud",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.DataContext as RequestVm;
            if (vm == null) return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Cancelar usa el mismo endpoint RejectFriendRequest (si el caller es el "from" ⇒ cancelado)
                var resp = await _client.RejectFriendRequestAsync(new RejectFriendRequestRequest
                {
                    Token = _token,
                    FriendRequestId = vm.FriendRequestId
                });

                _outgoing.Remove(vm);
                var ev = FriendsChanged; if (ev != null) ev(this, EventArgs.Empty);
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Cancelar solicitud",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + cx.Message, "Cancelar solicitud",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as LobbyWindow;
            if (win != null)
            {
                var mi = win.GetType().GetMethod("OnCloseOverlayClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(win, new object[] { this, new RoutedEventArgs() });
            }
        }

        // ===== VM =====
        private class RequestVm : INotifyPropertyChanged
        {
            public int FriendRequestId { get; set; }
            public int OtherAccountId { get; set; }
            public string DisplayName { get; set; }
            public string Subtitle { get; set; }
            public ImageSource Avatar { get; set; }
            public bool IsIncoming { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string name)
            {
                var ev = PropertyChanged; if (ev != null) ev(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
