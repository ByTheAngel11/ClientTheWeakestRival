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

        private const string DarknessAliasFormat = "Concursante {0}";
        private const string DarknessFallbackName = "???";

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
                ui.LstPlayers.ItemsSource = playersForUi;
            }

            if (ui.TxtPlayersSummary != null)
            {
                ui.TxtPlayersSummary.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    "({0})",
                    playersForUi.Length);
            }
        }

        public void EnableDarknessMode(int seed)
        {
            try
            {
                PlayerSummary[] basePlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();

                var alive = basePlayers
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
                            DisplayName = string.Format(CultureInfo.CurrentCulture, DarknessAliasFormat, index),
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

                if (ui.TxtPlayersSummary != null)
                {
                    ui.TxtPlayersSummary.Text = string.Format(CultureInfo.CurrentCulture, "({0})", playersForUi.Length);
                }

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
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Logger.Warn("TurnOrderController.PlayTurnIntroAsync error.", ex);
            }
            finally
            {
                hideOverlay?.Invoke();
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

            var byId = new Dictionary<int, PlayerSummary>();
            foreach (PlayerSummary p in basePlayers)
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
                            DisplayName = string.Format(CultureInfo.CurrentCulture, "Jugador {0}", userId),
                            Avatar = null
                        });
                }
            }

            foreach (PlayerSummary p in basePlayers)
            {
                if (p == null)
                {
                    continue;
                }

                if (ordered.Any(x => x.UserId == p.UserId))
                {
                    continue;
                }

                ordered.Add(p);
            }

            playersForUi = ordered.ToArray();
            ui.LstPlayers.ItemsSource = playersForUi;

            if (ui.TxtPlayersSummary != null)
            {
                ui.TxtPlayersSummary.Text = string.Format(CultureInfo.CurrentCulture, "({0})", playersForUi.Length);
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
                            DisplayName = DarknessFallbackName,
                            Avatar = null
                        });
                }
            }

            playersForUi = ordered.ToArray();
            ui.LstPlayers.ItemsSource = playersForUi;

            if (ui.TxtPlayersSummary != null)
            {
                ui.TxtPlayersSummary.Text = string.Format(CultureInfo.CurrentCulture, "({0})", playersForUi.Length);
            }
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

                return DarknessFallbackName;
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

            return string.Format(CultureInfo.CurrentCulture, "Jugador {0}", userId);
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
