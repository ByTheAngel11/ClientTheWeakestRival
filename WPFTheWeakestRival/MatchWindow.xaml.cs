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
            if (txtMatchTitle != null)
            {
                var title = string.IsNullOrWhiteSpace(_match.MatchCode)
                    ? "Partida"
                    : $"Partida {_match.MatchCode}";

                txtMatchTitle.Text = title;
            }

            if (txtMatchCode != null)
            {
                txtMatchCode.Text = _match.MatchCode ?? string.Empty;
            }

            if (txtMatchState != null)
            {
                txtMatchState.Text = string.IsNullOrWhiteSpace(_match.State)
                    ? "Waiting"
                    : _match.State;
            }

            if (lstPlayers != null)
            {
                var players = _match.Players ?? Array.Empty<PlayerSummary>();
                lstPlayers.ItemsSource = players;

                if (txtPlayersSummary != null)
                {
                    txtPlayersSummary.Text = players.Length.ToString();
                }
            }

            var cfg = _match.Config;
            if (cfg != null)
            {
                if (txtStartingScore != null)
                    txtStartingScore.Text = cfg.StartingScore.ToString("0.##");

                if (txtMaxScore != null)
                    txtMaxScore.Text = cfg.MaxScore.ToString("0.##");

                if (txtPointsCorrect != null)
                    txtPointsCorrect.Text = cfg.PointsPerCorrect.ToString("0.##");

                if (txtPointsWrong != null)
                    txtPointsWrong.Text = cfg.PointsPerWrong.ToString("0.##");

                if (txtPointsElimination != null)
                    txtPointsElimination.Text = cfg.PointsPerEliminationGain.ToString("0.##");

                if (txtCoinflip != null)
                    txtCoinflip.Text = cfg.AllowTiebreakCoinflip ? "Sí" : "No";
            }
            else
            {
                if (txtStartingScore != null) txtStartingScore.Text = "-";
                if (txtMaxScore != null) txtMaxScore.Text = "-";
                if (txtPointsCorrect != null) txtPointsCorrect.Text = "-";
                if (txtPointsWrong != null) txtPointsWrong.Text = "-";
                if (txtPointsElimination != null) txtPointsElimination.Text = "-";
                if (txtCoinflip != null) txtCoinflip.Text = "-";
            }
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
