using System;
using System.Windows;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival
{
    public partial class MatchWindow : Window
    {
        private readonly MatchInfo _match;
        private readonly string _token;
        private readonly int _myUserId;
        private readonly bool _isHost;

        // NUEVO: referencia directa al lobby
        private readonly LobbyWindow _lobbyWindow;

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

            // cuando se cierre la ventana de la partida, reabrimos el lobby
            this.Closed += MatchWindow_Closed;

            InitializeUi();
        }

        private void InitializeUi()
        {
            // Título fijo
            if (txtMatchTitle != null)
            {
                txtMatchTitle.Text = "Partida";
            }

            // Código pequeño arriba a la izquierda
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

            // Ya no pintamos estado ("Waiting") ni configuración
        }



        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // NUEVO: reabrir el lobby correcto
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
