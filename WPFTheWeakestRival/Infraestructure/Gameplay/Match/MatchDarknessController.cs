using log4net;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchDarknessController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchDarknessController));

        private const string LEGACY_DARKNESS_START_CODE = "DARKNESS_STARTED";
        private const string LEGACY_DARKNESS_END_CODE = "DARKNESS_ENDED";

        private const string DARK_MODE_START_CODE = "DARK_MODE_STARTED";
        private const string DARK_MODE_END_CODE = "DARK_MODE_ENDED";
        private const string DARK_MODE_VOTE_REVEAL_CODE = "DARK_MODE_VOTE_REVEAL";

        private const string DARKNESS_KEYWORD_ES = "oscuras";
        private const string DARKNESS_UNKNOWN_NAME = "???";
        private const string DARKNESS_TURN_LABEL = "A oscuras";

        private const string DARK_MODE_VOTE_REVEAL_TITLE = "A oscuras";
        private const string DARK_MODE_VOTE_REVEAL_DESCRIPTION = "Voto revelado.";

        private const string GENERIC_PLAYER_NAME_TEMPLATE = "Jugador {0}";
        private const string REVEAL_VOTE_TEMPLATE = "Votaste por: {0}";

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;
        private readonly TurnOrderController turns;
        private readonly QuestionController questions;

        internal MatchDarknessController(
            MatchWindowUiRefs ui,
            MatchSessionState state,
            TurnOrderController turns,
            QuestionController questions)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.turns = turns ?? throw new ArgumentNullException(nameof(turns));
            this.questions = questions ?? throw new ArgumentNullException(nameof(questions));
        }

        internal bool IsDarkModeStartEvent(string eventName, string description)
        {
            return IsCode(eventName, DARK_MODE_START_CODE)
                || IsCode(description, DARK_MODE_START_CODE)
                || IsCode(eventName, LEGACY_DARKNESS_START_CODE)
                || IsCode(description, LEGACY_DARKNESS_START_CODE)
                || ContainsKeyword(eventName, DARKNESS_KEYWORD_ES)
                || ContainsKeyword(description, DARKNESS_KEYWORD_ES);
        }

        internal bool IsDarkModeEndEvent(string eventName, string description)
        {
            return IsCode(eventName, DARK_MODE_END_CODE)
                || IsCode(description, DARK_MODE_END_CODE);
        }

        internal bool IsLegacyDarknessEndEvent(string eventName, string description)
        {
            return IsCode(eventName, LEGACY_DARKNESS_END_CODE)
                || IsCode(description, LEGACY_DARKNESS_END_CODE);
        }

        internal bool IsVoteRevealEvent(string eventName, string description)
        {
            return StartsWithCode(eventName, DARK_MODE_VOTE_REVEAL_CODE)
                || StartsWithCode(description, DARK_MODE_VOTE_REVEAL_CODE);
        }

        internal void ShowVoteRevealOverlay(OverlayController overlay)
        {
            if (overlay == null)
            {
                return;
            }

            overlay.ShowSpecialEvent(DARK_MODE_VOTE_REVEAL_TITLE, DARK_MODE_VOTE_REVEAL_DESCRIPTION);
        }

        internal void SetPendingVoteRevealUserId(int? userId)
        {
            if (userId.HasValue && userId.Value > 0)
            {
                state.PendingDarknessVotedUserId = userId.Value;
            }
        }

        internal void BeginDarknessMode()
        {
            if (state.IsDarknessActive)
            {
                return;
            }

            state.IsDarknessActive = true;

            int seed = BuildDarknessSeed(state.Match.MatchId, state.CurrentRoundNumber);

            state.DarknessSeed = seed;

            turns.EnableDarknessMode(seed);

            questions.SetDarknessActive(true);

            ApplyDarknessUiImmediate();
        }

        internal void EndDarknessMode(bool revealVote)
        {
            if (!state.IsDarknessActive)
            {
                return;
            }

            state.IsDarknessActive = false;
            state.DarknessSeed = null;

            turns.DisableDarknessMode();
            questions.SetDarknessActive(false);

            if (revealVote)
            {
                RevealPendingVoteIfAny();
            }
        }

        internal void RevealPendingVoteIfAny()
        {
            int? votedUserId = state.PendingDarknessVotedUserId;
            state.PendingDarknessVotedUserId = null;

            if (!votedUserId.HasValue || votedUserId.Value <= 0)
            {
                return;
            }

            try
            {
                PlayerSummary[] lobbyPlayers = state.Match.Players ?? Array.Empty<PlayerSummary>();

                PlayerSummary voted = lobbyPlayers.FirstOrDefault(p => p != null && p.UserId == votedUserId.Value);

                string name = voted != null && !string.IsNullOrWhiteSpace(voted.DisplayName)
                    ? voted.DisplayName
                    : string.Format(CultureInfo.CurrentCulture, GENERIC_PLAYER_NAME_TEMPLATE, votedUserId.Value);

                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, REVEAL_VOTE_TEMPLATE, name),
                    MatchConstants.GAME_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Warn("RevealPendingVoteIfAny error.", ex);
            }
        }

        private void ApplyDarknessUiImmediate()
        {
            try
            {
                if (ui.TurnAvatar != null)
                {
                    ui.TurnAvatar.Visibility = Visibility.Collapsed;
                }

                if (ui.TxtTurnPlayerName != null)
                {
                    ui.TxtTurnPlayerName.Text = DARKNESS_UNKNOWN_NAME;
                }

                if (ui.TxtTurnLabel != null)
                {
                    ui.TxtTurnLabel.Text = DARKNESS_TURN_LABEL;
                }

                if (ui.TurnBannerBackground != null)
                {
                    ui.TurnBannerBackground.Background = (Brush)ui.Window.FindResource("Brush.Turn.OtherTurn");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("ApplyDarknessUiImmediate error.", ex);
            }
        }

        private static int BuildDarknessSeed(Guid matchId, int roundNumber)
        {
            return matchId.GetHashCode() ^ roundNumber;
        }

        private static bool IsCode(string text, string code)
        {
            return string.Equals(text != null ? text.Trim() : null, code, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWithCode(string text, string code)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            return text.Trim().StartsWith(code, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
