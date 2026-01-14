using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class MatchResultWindow : Window
    {
        private const string KEY_MATCH_RESULT_DEFAULT_TITLE = "matchResultDefaultTitle";
        private const string KEY_PLAYER_FALLBACK_FORMAT = "playerFallbackFormat";
        private const string KEY_POSITION_FORMAT = "matchResultPositionFormat"; 
        private const string KEY_ANSWERS_FORMAT = "matchResultAnswersFormat";

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
                ? Localize(KEY_MATCH_RESULT_DEFAULT_TITLE)
                : mainResultText;

            if (localAvatar != null)
            {
                ResultAvatar.Appearance = localAvatar;
            }

            PlayerSummary localPlayer = allPlayers?
                .FirstOrDefault(p => p != null && p.UserId == localUserId);

            txtPlayerName.Text = !string.IsNullOrWhiteSpace(localPlayer?.DisplayName)
                ? localPlayer.DisplayName
                : string.Format(CultureInfo.CurrentCulture, Localize(KEY_PLAYER_FALLBACK_FORMAT), localUserId);

            List<PlayerResultItem> rankingItems = BuildRanking(
                allPlayers,
                winnerUserId,
                localUserId,
                out int localPosition);

            txtPosition.Text = string.Format(
                CultureInfo.CurrentCulture,
                Localize(KEY_POSITION_FORMAT),
                localPosition);

            double ratio = 0d;
            if (myTotalAnswers > 0)
            {
                ratio = (double)myCorrectAnswers / myTotalAnswers;
            }

            txtPercentage.Text = ratio.ToString("P0", CultureInfo.CurrentCulture);

            txtAnswers.Text = string.Format(
                CultureInfo.CurrentCulture,
                Localize(KEY_ANSWERS_FORMAT),
                myCorrectAnswers,
                myTotalAnswers);

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
                    ? string.Format(CultureInfo.CurrentCulture, Localize(KEY_PLAYER_FALLBACK_FORMAT), player.UserId)
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

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
