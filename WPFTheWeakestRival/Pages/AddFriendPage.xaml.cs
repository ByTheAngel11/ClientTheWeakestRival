using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;

namespace WPFTheWeakestRival.Pages
{
    public partial class AddFriendPage : Page
    {
        private readonly FriendServiceClient _client;
        private readonly string _token;

        private readonly ObservableCollection<SearchVm> _items = new ObservableCollection<SearchVm>();
        private CancellationTokenSource _cts;

        public event EventHandler FriendsUpdated;

        public AddFriendPage(FriendServiceClient client, string token)
        {
            InitializeComponent();
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _token = token ?? string.Empty;
            lstResults.ItemsSource = _items;
        }

        // ===== Buscar (click) =====
        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            await DoSearchAsync(txtQuery.Text);
        }

        // ===== Buscar (debounce al teclear) =====
        private async void txtQuery_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cts != null) _cts.Cancel();
            _cts = new CancellationTokenSource();
            var tk = _cts.Token;

            try
            {
                await Task.Delay(300, tk);
                if (!tk.IsCancellationRequested)
                    await DoSearchAsync(((TextBox)sender).Text);
            }
            catch (TaskCanceledException) { }
        }

        private async Task DoSearchAsync(string query)
        {
            var q = (query ?? "").Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                _items.Clear();
                txtStatus.Text = "";
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                txtStatus.Text = "Buscando…";

                var resp = await _client.SearchAccountsAsync(new SearchAccountsRequest
                {
                    Token = _token,
                    Query = q,
                    MaxResults = 20
                });


                _items.Clear();
                var results = resp.Results ?? new SearchAccountItem[0];

                foreach (var r in results)
                {
                    var img = UiImageHelper.TryCreateFromUrlOrPath(r.AvatarUrl, 36)
                              ?? UiImageHelper.DefaultAvatar(36);

                    _items.Add(new SearchVm
                    {
                        AccountId = r.AccountId,
                        DisplayName = string.IsNullOrWhiteSpace(r.DisplayName) ? r.Email : r.DisplayName,
                        Email = r.Email,
                        Avatar = img,
                        IsFriend = r.IsFriend,
                        HasPendingOutgoing = r.HasPendingOutgoing,
                        HasPendingIncoming = r.HasPendingIncoming,
                        PendingIncomingRequestId = r.PendingIncomingRequestId
                    });
                }

                txtStatus.Text = _items.Count == 0 ? "Sin resultados" : _items.Count + " resultado(s)";
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Buscar amigos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtStatus.Text = "Error del servicio";
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + cx.Message, "Buscar amigos",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Sin conexión";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ===== Enviar solicitud =====
        private async void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var vm = btn.DataContext as SearchVm;
            if (vm == null || !vm.CanAdd) return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var resp = await _client.SendFriendRequestAsync(new SendFriendRequestRequest
                {
                    Token = _token,
                    TargetAccountId = vm.AccountId
                });


                if (resp.Status == FriendRequestStatus.Accepted)
                {
                    vm.IsFriend = true;
                    vm.HasPendingOutgoing = false;
                    vm.HasPendingIncoming = false;
                    vm.PendingIncomingRequestId = null;
                    vm.NotifyStateChanged();

                    var ev = FriendsUpdated; if (ev != null) ev(this, EventArgs.Empty);
                    MessageBox.Show("Ahora eres amigo de " + vm.DisplayName + ".", "Amigos",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    vm.HasPendingOutgoing = true;
                    vm.NotifyStateChanged();
                }
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show(fx.Detail.Code + ": " + fx.Detail.Message, "Agregar amigo",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException cx)
            {
                MessageBox.Show("No se pudo conectar con el servicio.\n" + cx.Message, "Agregar amigo",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ===== Aceptar solicitud entrante =====
        private async void btnAccept_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var vm = btn.DataContext as SearchVm;
            if (vm == null) return;

            if (!vm.HasPendingIncoming || !vm.PendingIncomingRequestId.HasValue) return;
            int frId = vm.PendingIncomingRequestId.Value;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await _client.AcceptFriendRequestAsync(new AcceptFriendRequestRequest
                {
                    Token = _token,
                    FriendRequestId = frId
                });


                vm.IsFriend = true;
                vm.HasPendingIncoming = false;
                vm.PendingIncomingRequestId = null;
                vm.HasPendingOutgoing = false;
                vm.NotifyStateChanged();

                var ev = FriendsUpdated; if (ev != null) ev(this, EventArgs.Empty);
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

        // ===== VM interna =====
        private class SearchVm : INotifyPropertyChanged
        {
            public int AccountId { get; set; }
            public string DisplayName { get; set; } = "";
            public string Email { get; set; } = "";
            public ImageSource Avatar { get; set; }

            public bool IsFriend { get; set; }
            public bool HasPendingOutgoing { get; set; }
            public bool HasPendingIncoming { get; set; }
            public int? PendingIncomingRequestId { get; set; }

            // Derivados para UI (sin switch expr)
            public bool CanAdd { get { return !IsFriend && !HasPendingOutgoing && !HasPendingIncoming; } }
            public bool ShowAccept { get { return HasPendingIncoming; } }
            public string AddButtonText
            {
                get
                {
                    if (IsFriend) return "Amigos";
                    if (HasPendingOutgoing) return "Pendiente";
                    if (HasPendingIncoming) return "—";
                    return "Agregar";
                }
            }
            public string StateText
            {
                get
                {
                    if (IsFriend) return "Ya son amigos";
                    if (HasPendingOutgoing) return "Solicitud enviada";
                    if (HasPendingIncoming) return "Solicitud recibida";
                    return "Sugerido";
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
            private void OnPropertyChanged(string name)
            {
                var ev = PropertyChanged; if (ev != null) ev(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
