using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival
{
    public partial class MatchResultWindow : Window
    {
        private sealed class PlayerResultItem
        {
            public int Position { get; set; }

            public string DisplayName { get; set; }
        }

        public MatchResultWindow(
            string mainResultText,
            int localUserId,
            AvatarAppearance localAvatar,
            int myCorrectAnswers,
            int myTotalAnswers,
            IReadOnlyList<PlayerSummary> allPlayers,
            int winnerUserId)
        {
            InitializeComponent();

            txtMainResult.Text = string.IsNullOrWhiteSpace(mainResultText)
                ? "RESULTADO"
                : mainResultText;

            if (localAvatar != null)
            {
                ResultAvatar.Appearance = localAvatar;
            }

            PlayerSummary localPlayer = allPlayers?
                .FirstOrDefault(p => p != null && p.UserId == localUserId);

            txtPlayerName.Text = !string.IsNullOrWhiteSpace(localPlayer?.DisplayName)
                ? localPlayer.DisplayName
                : $"Jugador {localUserId}";

            int localPosition;
            List<PlayerResultItem> rankingItems = BuildRanking(
                allPlayers,
                winnerUserId,
                localUserId,
                out localPosition);

            txtPosition.Text = $"Tu posición: {localPosition}°";

            double ratio = 0d;
            if (myTotalAnswers > 0)
            {
                ratio = (double)myCorrectAnswers / myTotalAnswers;
            }

            txtPercentage.Text = $"{ratio:P0}";
            txtAnswers.Text = $"{myCorrectAnswers} de {myTotalAnswers} preguntas correctas";

            lstResults.ItemsSource = rankingItems;
        }

        private static List<PlayerResultItem> BuildRanking(
            IReadOnlyList<PlayerSummary> allPlayers,
            int winnerUserId,
            int localUserId,
            out int localPosition)
        {
            localPosition = 1;

            if (allPlayers == null || allPlayers.Count == 0)
            {
                return new List<PlayerResultItem>();
            }

            List<PlayerSummary> ordered = new List<PlayerSummary>();

            PlayerSummary winner = allPlayers.FirstOrDefault(
                p => p != null && p.UserId == winnerUserId);

            if (winner != null)
            {
                ordered.Add(winner);
            }

            foreach (PlayerSummary player in allPlayers)
            {
                if (player == null || player.UserId == winnerUserId)
                {
                    continue;
                }

                ordered.Add(player);
            }

            List<PlayerResultItem> items = new List<PlayerResultItem>();

            for (int index = 0; index < ordered.Count; index++)
            {
                PlayerSummary player = ordered[index];

                string displayName = string.IsNullOrWhiteSpace(player.DisplayName)
                    ? $"Jugador {player.UserId}"
                    : player.DisplayName;

                int position = index + 1;

                items.Add(
                    new PlayerResultItem
                    {
                        Position = position,
                        DisplayName = displayName
                    });

                if (player.UserId == localUserId)
                {
                    localPosition = position;
                }
            }

            return items;
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
