using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using log4net;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.WildcardService;
using WildcardFault = WPFTheWeakestRival.WildcardService.ServiceFault;

namespace WPFTheWeakestRival
{
    public partial class MatchWindow : Window
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchWindow));

        private readonly MatchInfo _match;
        private readonly string _token;
        private readonly int _myUserId;
        private readonly bool _isHost;
        private readonly int _matchDbId;
        private readonly LobbyWindow _lobbyWindow;
        private PlayerWildcardDto _myWildcard;

        public MatchWindow(MatchInfo match, string token, int myUserId, bool isHost, LobbyWindow lobbyWindow)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            InitializeComponent();

            _match = match;
            _token = token;
            _myUserId = myUserId;
            _isHost = isHost;
            _lobbyWindow = lobbyWindow;
            _matchDbId = match.MatchDbId;

            Closed += MatchWindow_Closed;
            Loaded += MatchWindow_Loaded;

            InitializeUi();
        }

        private void InitializeUi()
        {
            // Título
            if (txtMatchTitle != null)
            {
                txtMatchTitle.Text = "Partida";
            }

            // Código pequeño arriba
            if (txtMatchCodeSmall != null)
            {
                var code = string.IsNullOrWhiteSpace(_match.MatchCode)
                    ? "Sin código"
                    : _match.MatchCode;

                txtMatchCodeSmall.Text = $"Código: {code}";
            }

            // Jugadores
            if (lstPlayers != null)
            {
                var players = _match.Players ?? Array.Empty<PlayerSummary>();
                lstPlayers.ItemsSource = players;

                if (txtPlayersSummary != null)
                {
                    txtPlayersSummary.Text = $"({players.Length})";
                }
            }

            // Texto inicial del comodín
            UpdateWildcardUi();
        }

        private async void MatchWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadWildcardAsync();
        }

        private async Task LoadWildcardAsync()
        {
            if (string.IsNullOrWhiteSpace(_token) || _matchDbId <= 0)
            {
                return;
            }

            try
            {
                using (var client = new WildcardServiceClient("WSHttpBinding_IWildcardService"))
                {
                    var request = new GetPlayerWildcardsRequest
                    {
                        Token = _token,
                        MatchId = _matchDbId
                    };

                    var response = await Task.Run(() => client.GetPlayerWildcards(request));

                    var wildcard = response?.Wildcards != null && response.Wildcards.Length > 0
                        ? response.Wildcards[0]
                        : null;

                    _myWildcard = wildcard;

                    Dispatcher.Invoke(UpdateWildcardUi);
                }
            }
            catch (FaultException<WildcardFault> ex)
            {
                Logger.Warn("Fault al obtener comodines del jugador en MatchWindow.", ex);
                MessageBox.Show(
                    $"{ex.Detail.Code}: {ex.Detail.Message}",
                    "Wildcards",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al obtener comodines en MatchWindow.", ex);
                MessageBox.Show(
                    "No se pudieron cargar los comodines." + Environment.NewLine + ex.Message,
                    "Wildcards",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al obtener comodines en MatchWindow.", ex);
                MessageBox.Show(
                    "Ocurrió un error al cargar los comodines." + Environment.NewLine + ex.Message,
                    "Wildcards",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateWildcardUi()
        {
            if (txtWildcardName == null || txtWildcardDescription == null)
            {
                return;
            }

            if (_myWildcard == null)
            {
                txtWildcardName.Text = "Sin comodín";
                txtWildcardDescription.Text = string.Empty;

                if (imgWildcardIcon != null)
                {
                    imgWildcardIcon.Visibility = Visibility.Collapsed;
                    imgWildcardIcon.Source = null;
                }

                return;
            }

            var name = string.IsNullOrWhiteSpace(_myWildcard.Name)
                ? _myWildcard.Code
                : _myWildcard.Name;

            txtWildcardName.Text = name ?? "Comodín";

            txtWildcardDescription.Text =
                string.IsNullOrWhiteSpace(_myWildcard.Description)
                    ? _myWildcard.Code
                    : _myWildcard.Description;

            UpdateWildcardIcon();
        }

        private void UpdateWildcardIcon()
        {
            if (imgWildcardIcon == null)
            {
                return;
            }

            if (_myWildcard == null || string.IsNullOrWhiteSpace(_myWildcard.Code))
            {
                imgWildcardIcon.Visibility = Visibility.Collapsed;
                imgWildcardIcon.Source = null;
                return;
            }

            try
            {
                var code = _myWildcard.Code.Trim();
                var uriString = $"pack://application:,,,/Assets/Wildcards/{code}.png";

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(uriString, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                imgWildcardIcon.Source = bitmap;
                imgWildcardIcon.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.Warn($"No se pudo cargar la imagen del comodín '{_myWildcard.Code}'.", ex);
                imgWildcardIcon.Visibility = Visibility.Collapsed;
                imgWildcardIcon.Source = null;
            }
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MatchWindow_Closed(object sender, EventArgs e)
        {
            if (_lobbyWindow != null)
            {
                _lobbyWindow.Show();
                _lobbyWindow.Activate();
            }
        }
    }
}
