namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal static class MatchDarknessConstants
    {
        public const string SPECIAL_EVENT_DARKNESS_STARTED_CODE = "DARKNESS_STARTED";
        public const string SPECIAL_EVENT_DARKNESS_ENDED_CODE = "DARKNESS_ENDED";

        public static string DARKNESS_OVERLAY_TITLE => Properties.Langs.Lang.msgSpecialEventDarknessTitle;
        public static string DARKNESS_OVERLAY_DESCRIPTION => Properties.Langs.Lang.msgSpecialEventDarknessDesc;

        public static string DARKNESS_ENDED_OVERLAY_TITLE => Properties.Langs.Lang.msgSpecialEventDarknessEndedTitle;
        public static string DARKNESS_ENDED_OVERLAY_DESCRIPTION => Properties.Langs.Lang.msgSpecialEventDarknessEndedDesc;

        public static string UNKNOWN_PLAYER_NAME => Properties.Langs.Lang.darknessUnknownPlayerName;
        public static string DARKNESS_MY_TURN_TEXT => Properties.Langs.Lang.darknessMyTurnText;
        public static string DARKNESS_OTHER_TURN_TEXT => Properties.Langs.Lang.darknessOtherTurnText;

        public static string DARKNESS_VOTE_WINDOW_TITLE => Properties.Langs.Lang.darknessVoteWindowTitle;

        public static string DARKNESS_SLOT_FORMAT => Properties.Langs.Lang.darknessSlotFormat;

        public static string DARKNESS_SOMEONE_ANSWERED_CORRECT => Properties.Langs.Lang.darknessSomeoneAnsweredCorrect;
        public static string DARKNESS_SOMEONE_ANSWERED_INCORRECT => Properties.Langs.Lang.darknessSomeoneAnsweredIncorrect;

        public static string DARKNESS_REVEAL_VOTE_FORMAT => Properties.Langs.Lang.darknessRevealVoteFormat;

        public const int HASH_SEED = 17;
        public const int HASH_MULTIPLIER = 31;
    }
}
