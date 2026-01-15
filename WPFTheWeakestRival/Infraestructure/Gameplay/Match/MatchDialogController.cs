using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Pages;
using WPFTheWeakestRival.Properties.Langs;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchDialogController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchDialogController));

        private const string LOG_SHOW_MATCH_RESULT_WINDOW_ERROR = "ShowMatchResultAndClose: MatchResultWindow error.";
        private const string LOG_RESOLVE_PLAYER_DISPLAY_NAME_ERROR = "ResolvePlayerDisplayName: error reading lobby players.";
        private const string LOG_DUEL_SELECTION_ERROR = "ShowDuelSelectionAndSendAsync error.";
        private const string LOG_SEND_VOTE_ERROR = "SendVoteAsync error.";

        private readonly MatchWindowUiRefs uiMatchWindow;
        private readonly MatchSessionState state;
        private readonly GameplayClientProxy gameplay;

        public MatchDialogController(MatchWindowUiRefs ui, MatchSessionState state, GameplayClientProxy gameplay)
        {
            this.uiMatchWindow = ui ?? throw new ArgumentNullException(nameof(ui));
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
            if (isMe)
            {
                return;
            }

            string name = ResolvePlayerNameForUi(eliminatedPlayer.UserId, eliminatedPlayer.DisplayName);

            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, Lang.matchPlayerEliminatedFormat, name),
                MatchConstants.GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void ShowMatchFinishedMessage(GameplayServiceProxy.PlayerSummary winner)
        {
            string winnerName = ResolvePlayerNameForUi(
                winner != null ? winner.UserId : 0,
                winner != null ? winner.DisplayName : null);

            string message = string.Format(
                CultureInfo.CurrentCulture,
                MatchConstants.MATCH_WINNER_MESSAGE_FORMAT,
                winnerName);

            MessageBox.Show(
                message + Environment.NewLine + Lang.matchResultCloseHint,
                MatchConstants.GAME_MESSAGE_TITLE,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void ShowMatchResultAndClose(Action closeWindow)
        {
            try
            {
                PlayerSummary[] players = state.Match.Players ?? Array.Empty<PlayerSummary>();

                bool iAmWinner = state.FinalWinner != null && state.FinalWinner.UserId == state.MyUserId;

                string mainResultText = iAmWinner
                    ? Lang.matchResultWinTitle
                    : Lang.wndMatchResultTitle;

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

                resultWindow.Owner = uiMatchWindow.Window;
                resultWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_SHOW_MATCH_RESULT_WINDOW_ERROR, ex);
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
                Title = state.IsDarknessActive ? Lang.darknessVoteWindowTitle : Lang.voteWindowTitle,
                Content = votePage,
                Owner = uiMatchWindow.Window,
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

            IReadOnlyCollection<DuelCandidateItem> normalizedCandidates = NormalizeDuelCandidatesForUi(candidates);

            int? selectedUserId = null;

            var duelPage = new DuelSelectionPage(
                state.MatchDbId,
                weakestRivalUserId,
                normalizedCandidates);

            var hostWindow = new Window
            {
                Title = Lang.duelSelectionTitle,
                Content = duelPage,
                Owner = uiMatchWindow.Window,
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
                Logger.Warn(LOG_DUEL_SELECTION_ERROR, ex);
            }
        }

        private IReadOnlyCollection<DuelCandidateItem> NormalizeDuelCandidatesForUi(IReadOnlyCollection<DuelCandidateItem> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Array.Empty<DuelCandidateItem>();
            }

            Dictionary<int, string> aliasByUserId = state.IsDarknessActive
                ? BuildDarknessAliasMapIncludingMe()
                : null;

            var list = new List<DuelCandidateItem>(candidates.Count);

            foreach (DuelCandidateItem c in candidates)
            {
                if (c == null || c.UserId <= 0)
                {
                    continue;
                }

                string name = state.IsDarknessActive
                    ? ResolveAlias(aliasByUserId, c.UserId)
                    : ResolvePlayerDisplayName(c.UserId, c.DisplayName);

                list.Add(new DuelCandidateItem
                {
                    UserId = c.UserId,
                    DisplayName = name
                });
            }

            return list;
        }

        private string ResolvePlayerNameForUi(int userId, string gameplayDisplayName)
        {
            if (state.IsDarknessActive)
            {
                Dictionary<int, string> aliasByUserId = BuildDarknessAliasMapIncludingMe();
                return ResolveAlias(aliasByUserId, userId);
            }

            return ResolvePlayerDisplayName(userId, gameplayDisplayName);
        }

        private string ResolvePlayerDisplayName(int userId, string gameplayDisplayName)
        {
            if (!string.IsNullOrWhiteSpace(gameplayDisplayName) &&
                !IsGeneratedPlayerName(gameplayDisplayName, userId))
            {
                return gameplayDisplayName;
            }

            try
            {
                PlayerSummary[] lobbyPlayers = state.Match?.Players ?? Array.Empty<PlayerSummary>();
                PlayerSummary lobby = lobbyPlayers.FirstOrDefault(p => p != null && p.UserId == userId);

                if (lobby != null && !string.IsNullOrWhiteSpace(lobby.DisplayName))
                {
                    return lobby.DisplayName;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_RESOLVE_PLAYER_DISPLAY_NAME_ERROR, ex);
            }

            if (userId > 0)
            {
                return string.Format(CultureInfo.CurrentCulture, Lang.playerWithIdFormat, userId);
            }

            return MatchConstants.DEFAULT_PLAYER_NAME;
        }

        private static bool IsGeneratedPlayerName(string displayName, int userId)
        {
            if (string.IsNullOrWhiteSpace(displayName) || userId <= 0)
            {
                return false;
            }

            string trimmed = displayName.Trim();

            string expectedCurrent = string.Format(CultureInfo.CurrentCulture, Lang.playerWithIdFormat, userId);
            if (string.Equals(trimmed, expectedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string expectedInvariant = string.Format(CultureInfo.InvariantCulture, Lang.playerWithIdFormat, userId);
            if (string.Equals(trimmed, expectedInvariant, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string userIdText = userId.ToString(CultureInfo.InvariantCulture);

            return trimmed.StartsWith(Lang.player, StringComparison.OrdinalIgnoreCase)
                && trimmed.EndsWith(userIdText, StringComparison.OrdinalIgnoreCase);
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
                        ? string.Format(CultureInfo.CurrentCulture, Lang.playerWithIdFormat, p.UserId)
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
                return Lang.darknessUnknownPlayerName;
            }

            string alias;
            if (aliasByUserId.TryGetValue(userId, out alias) && !string.IsNullOrWhiteSpace(alias))
            {
                return alias;
            }

            return Lang.darknessUnknownPlayerName;
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
                    map[p.UserId] = string.Format(CultureInfo.CurrentCulture, Lang.darknessSlotFormat, index);
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
                Logger.Warn(LOG_SEND_VOTE_ERROR, ex);
            }
        }
    }
}
