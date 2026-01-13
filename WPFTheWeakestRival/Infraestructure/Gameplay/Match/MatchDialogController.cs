using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Pages;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchDialogController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchDialogController));

        private const string DARKNESS_ALIAS_FORMAT = "Concursante {0}";
        private const string DARKNESS_FALLBACK_NAME = "???";

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;
        private readonly GameplayClientProxy gameplay;

        public MatchDialogController(MatchWindowUiRefs ui, MatchSessionState state, GameplayClientProxy gameplay)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        }

        public void ShowEliminationMessage(GameplayServiceProxy.PlayerSummary eliminatedPlayer)
        {
            if (eliminatedPlayer == null)
            {
                return;
            }

            bool isMe = eliminatedPlayer.UserId == state.MyUserId;

            string name = string.IsNullOrWhiteSpace(eliminatedPlayer.DisplayName)
                ? MatchConstants.DEFAULT_PLAYER_NAME
                : eliminatedPlayer.DisplayName;

            if (isMe)
            {
                return;
            }

            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, "{0} ha sido eliminado de la ronda.", name),
                MatchConstants.GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void ShowMatchFinishedMessage(GameplayServiceProxy.PlayerSummary winner)
        {
            string winnerName = winner != null && !string.IsNullOrWhiteSpace(winner.DisplayName)
                ? winner.DisplayName
                : MatchConstants.DEFAULT_PLAYER_NAME;

            string message = string.Format(
                CultureInfo.CurrentCulture,
                MatchConstants.MATCH_WINNER_MESSAGE_FORMAT,
                winnerName);

            MessageBox.Show(
                message + "\nPuedes cerrar la ventana cuando quieras para ver tu resultado.",
                MatchConstants.GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void ShowMatchResultAndClose(Action closeWindow)
        {
            try
            {
                PlayerSummary[] players = state.Match.Players ?? Array.Empty<PlayerSummary>();

                string mainResultText;
                bool iAmWinner = state.FinalWinner != null && state.FinalWinner.UserId == state.MyUserId;

                mainResultText = iAmWinner
                    ? "¡GANASTE LA PARTIDA!"
                    : "Resultado de la partida";

                AvatarAppearance localAvatar = null;

                PlayerSummary myLobbyPlayer = players.FirstOrDefault(p => p != null && p.UserId == state.MyUserId);

                if (myLobbyPlayer != null)
                {
                    localAvatar = AvatarMapper.FromLobbyDto(myLobbyPlayer.Avatar);
                }

                int winnerUserId = state.FinalWinner != null ? state.FinalWinner.UserId : 0;

                var resultWindow = new MatchResultWindow(
                    mainResultText,
                    state.MyUserId,
                    localAvatar,
                    state.MyCorrectAnswers,
                    state.MyTotalAnswers,
                    players.ToList(),
                    winnerUserId);

                resultWindow.Owner = ui.Window;
                resultWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error al mostrar MatchResultWindow.", ex);
            }
            finally
            {
                closeWindow?.Invoke();
            }
        }

        public async Task ShowVoteAndSendAsync(int voteDurationSeconds)
        {
            if (state.IsEliminated(state.MyUserId))
            {
                Logger.Info("ShowVoteAndSendAsync: current user eliminated, skipping vote.");
                await SendVoteAsync(null);
                return;
            }

            var playersForVote = new List<PlayerVoteItem>(BuildVotePlayers());

            var votePage = new VotePage(
                state.MatchDbId,
                state.MyUserId,
                playersForVote,
                voteDurationSeconds);

            int? selectedTargetUserId = null;

            var hostWindow = new Window
            {
                Title = "Votación",
                Content = votePage,
                Owner = ui.Window,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };

            votePage.VoteCompleted += (s, e) =>
            {
                selectedTargetUserId = e != null ? e.TargetUserId : (int?)null;
                hostWindow.DialogResult = true;
                hostWindow.Close();
            };

            hostWindow.ShowDialog();

            if (state.IsEliminated(state.MyUserId))
            {
                Logger.Info("ShowVoteAndSendAsync: eliminated after dialog, ignoring.");
                return;
            }

            if (state.IsDarknessActive)
            {
                state.PendingDarknessVotedUserId = selectedTargetUserId;
            }

            await SendVoteAsync(selectedTargetUserId);
        }

        private IEnumerable<PlayerVoteItem> BuildVotePlayers()
        {
            PlayerSummary[] lobbyPlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();

            Dictionary<int, string> aliasByUserId = state.IsDarknessActive
                ? BuildDarknessAliasMapIncludingMe()
                : null;

            foreach (PlayerSummary p in lobbyPlayers)
            {
                if (p == null)
                {
                    continue;
                }

                if (p.UserId == state.MyUserId || state.IsEliminated(p.UserId))
                {
                    continue;
                }

                string displayName;

                if (state.IsDarknessActive)
                {
                    displayName = ResolveAlias(aliasByUserId, p.UserId);
                }
                else
                {
                    displayName = string.IsNullOrWhiteSpace(p.DisplayName)
                        ? MatchConstants.DEFAULT_PLAYER_NAME
                        : p.DisplayName;
                }

                yield return new PlayerVoteItem
                {
                    UserId = p.UserId,
                    DisplayName = displayName,
                    BankedPoints = 0m,
                    CorrectAnswers = 0,
                    WrongAnswers = 0
                };
            }
        }

        private static string ResolveAlias(Dictionary<int, string> aliasByUserId, int userId)
        {
            if (aliasByUserId == null || userId <= 0)
            {
                return DARKNESS_FALLBACK_NAME;
            }

            string alias;
            if (aliasByUserId.TryGetValue(userId, out alias) && !string.IsNullOrWhiteSpace(alias))
            {
                return alias;
            }

            return DARKNESS_FALLBACK_NAME;
        }

        private Dictionary<int, string> BuildDarknessAliasMapIncludingMe()
        {
            int seed = state.DarknessSeed.HasValue
                ? state.DarknessSeed.Value
                : (state.Match.MatchId.GetHashCode() ^ state.CurrentRoundNumber);

            PlayerSummary[] basePlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();

            var alive = basePlayers
                .Where(p => p != null && p.UserId > 0 && !state.IsEliminated(p.UserId))
                .ToList();

            Shuffle(alive, seed);

            var map = new Dictionary<int, string>();

            int index = 1;
            foreach (PlayerSummary p in alive)
            {
                if (p == null || p.UserId <= 0)
                {
                    continue;
                }

                if (!map.ContainsKey(p.UserId))
                {
                    map[p.UserId] = string.Format(CultureInfo.CurrentCulture, DARKNESS_ALIAS_FORMAT, index);
                }

                index++;
            }

            return map;
        }

        private static void Shuffle(List<PlayerSummary> list, int seed)
        {
            if (list == null || list.Count <= 1)
            {
                return;
            }

            var random = new Random(seed);

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);

                PlayerSummary temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private async Task SendVoteAsync(int? targetUserId)
        {
            try
            {
                var request = new GameplayServiceProxy.CastVoteRequest
                {
                    Token = state.Token,
                    MatchId = state.Match.MatchId,
                    TargetUserId = targetUserId
                };

                Logger.InfoFormat(
                    "Sending CastVote. MatchId={0}, VoterUserId={1}, TargetUserId={2}",
                    state.Match.MatchId,
                    state.MyUserId,
                    targetUserId.HasValue ? targetUserId.Value : 0);

                await gameplay.CastVoteAsync(request);
            }
            catch (Exception ex)
            {
                Logger.Warn("SendVoteAsync error.", ex);
            }
        }

        public async Task ShowDuelSelectionAndSendAsync(
            int weakestRivalUserId,
            IReadOnlyCollection<DuelCandidateItem> candidates)
        {
            if (weakestRivalUserId != state.MyUserId)
            {
                return;
            }

            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            int? selectedUserId = null;

            var duelPage = new DuelSelectionPage(
                state.MatchDbId,
                weakestRivalUserId,
                candidates);

            var hostWindow = new Window
            {
                Title = "Selecciona a quién retar",
                Content = duelPage,
                Owner = ui.Window,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };

            duelPage.DuelSelectionCompleted += (s, e) =>
            {
                selectedUserId = e != null ? (int?)e.TargetUserId : null;
                hostWindow.DialogResult = true;
                hostWindow.Close();
            };

            hostWindow.ShowDialog();

            if (!selectedUserId.HasValue)
            {
                Logger.Info("ShowDuelSelectionAndSendAsync: user closed without selection.");
                return;
            }

            try
            {
                var request = new GameplayServiceProxy.ChooseDuelOpponentRequest
                {
                    Token = state.Token,
                    TargetUserId = selectedUserId.Value
                };

                Logger.InfoFormat(
                    "Enviando ChooseDuelOpponent. MatchDbId={0}, WeakestRivalUserId={1}, TargetUserId={2}",
                    state.MatchDbId,
                    weakestRivalUserId,
                    selectedUserId.Value);

                await gameplay.ChooseDuelOpponentAsync(request);
            }
            catch (Exception ex)
            {
                Logger.Warn("ShowDuelSelectionAndSendAsync error.", ex);
            }
        }
    }
}
