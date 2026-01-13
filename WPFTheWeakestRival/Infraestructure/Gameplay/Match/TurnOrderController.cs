using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class TurnOrderController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TurnOrderController));

        private const string DARKNESS_ALIAS_FORMAT = "Concursante {0}";
        private const string DARKNESS_FALLBACK_NAME = "???";
        private const string FALLBACK_PLAYER_NAME_FORMAT = "Jugador {0}";

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;

        private PlayerSummary[] playersForUi = Array.Empty<PlayerSummary>();
        private CancellationTokenSource turnIntroCts;

        public TurnOrderController(MatchWindowUiRefs ui, MatchSessionState state)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void InitializePlayers()
        {
            playersForUi = state.Match.Players ?? Array.Empty<PlayerSummary>();

            if (ui.LstPlayers != null)
            {
                ui.LstPlayers.DisplayMemberPath = nameof(PlayerSummary.DisplayName);
                ui.LstPlayers.ItemsSource = playersForUi;
            }

            UpdatePlayersSummaryText();
        }


        public void EnableDarknessMode(int seed)
        {
            try
            {
                PlayerSummary[] basePlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();

                List<PlayerSummary> alive = basePlayers
                    .Where(p => p != null && p.UserId > 0 && !state.IsEliminated(p.UserId))
                    .ToList();

                Shuffle(alive, seed);

                var anonymized = new List<PlayerSummary>(alive.Count);
                int index = 1;

                foreach (PlayerSummary p in alive)
                {
                    anonymized.Add(
                        new PlayerSummary
                        {
                            UserId = p.UserId,
                            DisplayName = string.Format(CultureInfo.CurrentCulture, DARKNESS_ALIAS_FORMAT, index),
                            Avatar = null,
                            IsOnline = p.IsOnline
                        });

                    index++;
                }

                playersForUi = anonymized.ToArray();

                if (ui.LstPlayers != null)
                {
                    ui.LstPlayers.ItemsSource = playersForUi;
                }

                UpdatePlayersSummaryText();
                TryHighlightPlayerInList(state.CurrentTurnUserId);
            }
            catch (Exception ex)
            {
                Logger.Warn("TurnOrderController.EnableDarknessMode error.", ex);
            }
        }

        public void DisableDarknessMode()
        {
            InitializePlayers();
            TryHighlightPlayerInList(state.CurrentTurnUserId);
        }

        public void ApplyTurnOrder(object turnOrderDto)
        {
            TurnOrderSnapshot snapshot = TurnOrderAdapter.ToSnapshot(turnOrderDto);

            state.CurrentTurnUserId = snapshot.CurrentTurnUserId;

            if (snapshot.OrderedAliveUserIds.Length > 0)
            {
                if (state.IsDarknessActive)
                {
                    RebuildPlayersForUiDarkness(snapshot.OrderedAliveUserIds);
                }
                else
                {
                    RebuildPlayersForUi(snapshot.OrderedAliveUserIds);
                }
            }

            TryHighlightPlayerInList(state.CurrentTurnUserId);
        }

        public async Task PlayTurnIntroAsync(object turnOrderDto, Action<string, string> showOverlay, Action hideOverlay)
        {
            if (turnOrderDto == null || ui.LstPlayers == null)
            {
                return;
            }

            CancelIntro();

            turnIntroCts = new CancellationTokenSource();
            CancellationToken ct = turnIntroCts.Token;

            TurnOrderSnapshot snapshot = TurnOrderAdapter.ToSnapshot(turnOrderDto);
            int[] orderedAliveUserIds = snapshot.OrderedAliveUserIds;

            if (orderedAliveUserIds.Length == 0)
            {
                return;
            }

            int starterUserId = snapshot.CurrentTurnUserId;

            string orderText = string.Join(
                " → ",
                orderedAliveUserIds
                    .Where(id => id > 0)
                    .Select(GetDisplayNameByUserIdSafe)
                    .ToArray());

            string starterName = GetDisplayNameByUserIdSafe(starterUserId);

            showOverlay?.Invoke(
                MatchConstants.TURN_INTRO_TITLE,
                string.Format(
                    CultureInfo.CurrentCulture,
                    MatchConstants.TURN_INTRO_DESCRIPTION_TEMPLATE,
                    orderText,
                    starterName));

            try
            {
                foreach (int userId in orderedAliveUserIds)
                {
                    ct.ThrowIfCancellationRequested();

                    TryHighlightPlayerInList(userId);
                    await Task.Delay(MatchConstants.TURN_INTRO_STEP_DELAY_MS, ct);
                }

                TryHighlightPlayerInList(starterUserId);
                await Task.Delay(MatchConstants.TURN_INTRO_FINAL_DELAY_MS, ct);
            }
            catch (OperationCanceledException ex)
            {
                Logger.Info("TurnOrderController.PlayTurnIntroAsync cancelled.", ex);
            }
            catch (Exception ex)
            {
                Logger.Warn("TurnOrderController.PlayTurnIntroAsync error.", ex);
            }
            finally
            {
                try
                {
                    hideOverlay?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.Warn("TurnOrderController.PlayTurnIntroAsync: error hiding overlay.", ex);
                }
            }
        }

        public void CancelIntro()
        {
            try
            {
                if (turnIntroCts != null)
                {
                    turnIntroCts.Cancel();
                    turnIntroCts.Dispose();
                    turnIntroCts = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("TurnOrderController.CancelIntro error.", ex);
            }
        }

        private void RebuildPlayersForUi(int[] orderedAliveUserIds)
        {
            if (ui.LstPlayers == null)
            {
                return;
            }

            PlayerSummary[] basePlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();
            Dictionary<int, PlayerSummary> byId = BuildPlayersById(basePlayers);

            List<PlayerSummary> ordered = BuildOrderedAlivePlayers(orderedAliveUserIds, byId);
            AppendMissingPlayers(basePlayers, ordered);

            ApplyPlayersForUi(ordered);
        }

        private Dictionary<int, PlayerSummary> BuildPlayersById(PlayerSummary[] basePlayers)
        {
            var byId = new Dictionary<int, PlayerSummary>();

            foreach (PlayerSummary p in basePlayers ?? Array.Empty<PlayerSummary>())
            {
                if (p == null || p.UserId <= 0)
                {
                    continue;
                }

                if (!byId.ContainsKey(p.UserId))
                {
                    byId[p.UserId] = p;
                }
            }

            return byId;
        }

        private List<PlayerSummary> BuildOrderedAlivePlayers(int[] orderedAliveUserIds, Dictionary<int, PlayerSummary> byId)
        {
            var ordered = new List<PlayerSummary>();

            foreach (int userId in orderedAliveUserIds ?? Array.Empty<int>())
            {
                if (userId <= 0 || state.IsEliminated(userId))
                {
                    continue;
                }

                PlayerSummary found;
                if (byId.TryGetValue(userId, out found))
                {
                    ordered.Add(found);
                    continue;
                }

                ordered.Add(CreateFallbackPlayer(userId));
            }

            return ordered;
        }

        private static PlayerSummary CreateFallbackPlayer(int userId)
        {
            return new PlayerSummary
            {
                UserId = userId,
                DisplayName = string.Format(CultureInfo.CurrentCulture, FALLBACK_PLAYER_NAME_FORMAT, userId),
                Avatar = null
            };
        }

        private static void AppendMissingPlayers(PlayerSummary[] basePlayers, List<PlayerSummary> ordered)
        {
            var existingIds = new HashSet<int>(ordered.Select(p => p != null ? p.UserId : 0));

            foreach (PlayerSummary p in basePlayers ?? Array.Empty<PlayerSummary>())
            {
                if (p == null)
                {
                    continue;
                }

                if (existingIds.Contains(p.UserId))
                {
                    continue;
                }

                ordered.Add(p);
            }
        }

        private void ApplyPlayersForUi(List<PlayerSummary> ordered)
        {
            playersForUi = (ordered ?? new List<PlayerSummary>()).ToArray();

            if (ui.LstPlayers != null)
            {
                ui.LstPlayers.ItemsSource = playersForUi;
            }

            UpdatePlayersSummaryText();
        }

        private void UpdatePlayersSummaryText()
        {
            if (ui.TxtPlayersSummary != null)
            {
                ui.TxtPlayersSummary.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    "({0})",
                    playersForUi != null ? playersForUi.Length : 0);
            }
        }

        private void RebuildPlayersForUiDarkness(int[] orderedAliveUserIds)
        {
            if (ui.LstPlayers == null)
            {
                return;
            }

            var byId = new Dictionary<int, PlayerSummary>();
            foreach (PlayerSummary p in playersForUi ?? Array.Empty<PlayerSummary>())
            {
                if (p != null && p.UserId > 0 && !byId.ContainsKey(p.UserId))
                {
                    byId[p.UserId] = p;
                }
            }

            var ordered = new List<PlayerSummary>();
            foreach (int userId in orderedAliveUserIds)
            {
                if (userId <= 0)
                {
                    continue;
                }

                if (state.IsEliminated(userId))
                {
                    continue;
                }

                PlayerSummary found;
                if (byId.TryGetValue(userId, out found))
                {
                    ordered.Add(found);
                }
                else
                {
                    ordered.Add(
                        new PlayerSummary
                        {
                            UserId = userId,
                            DisplayName = DARKNESS_FALLBACK_NAME,
                            Avatar = null
                        });
                }
            }

            playersForUi = ordered.ToArray();
            ui.LstPlayers.ItemsSource = playersForUi;

            UpdatePlayersSummaryText();
        }

        public void TryHighlightPlayerInList(int userId)
        {
            if (ui.LstPlayers == null || userId <= 0)
            {
                return;
            }

            try
            {
                object item = null;

                foreach (object it in ui.LstPlayers.Items)
                {
                    var p = it as PlayerSummary;
                    if (p != null && p.UserId == userId)
                    {
                        item = it;
                        break;
                    }
                }

                if (item != null)
                {
                    ui.LstPlayers.SelectedItem = item;
                    ui.LstPlayers.ScrollIntoView(item);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("TurnOrderController.TryHighlightPlayerInList error.", ex);
            }
        }

        private string GetDisplayNameByUserIdSafe(int userId)
        {
            if (state.IsDarknessActive)
            {
                PlayerSummary p = playersForUi.FirstOrDefault(x => x != null && x.UserId == userId);
                if (p != null && !string.IsNullOrWhiteSpace(p.DisplayName))
                {
                    return p.DisplayName;
                }

                return DARKNESS_FALLBACK_NAME;
            }

            if (userId <= 0)
            {
                return MatchConstants.DEFAULT_PLAYER_NAME;
            }

            PlayerSummary[] basePlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();
            PlayerSummary real = basePlayers.FirstOrDefault(x => x != null && x.UserId == userId);

            if (real != null && !string.IsNullOrWhiteSpace(real.DisplayName))
            {
                return real.DisplayName;
            }

            return string.Format(CultureInfo.CurrentCulture, FALLBACK_PLAYER_NAME_FORMAT, userId);
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
    }
}
