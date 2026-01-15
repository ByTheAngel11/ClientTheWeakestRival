using System.Windows.Media;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal static class MatchConstants
    {
        public const string FAULT_CODE_MATCH_ALREADY_STARTED = "MATCH_ALREADY_STARTED";

        public const string DEFAULT_LOCALE = "es-MX";
        public const int MAX_QUESTIONS = 40;

        public const int QUESTION_TIME_SECONDS = 30;
        public const int TIMER_INTERVAL_SECONDS = 1;

        internal static string MATCH_FINISHED_LABEL_TEXT => Lang.matchFinishedLabelText;
        internal static string EXIT_TO_LOBBY_PROMPT => Lang.exitToLobbyPrompt;

        public const string TIMER_FORMAT = @"mm\:ss";
        public const string POINTS_FORMAT = "0.00";

        public static string GAME_MESSAGE_TITLE => Lang.gameMessageTitle;
        public static string WILDCARDS_MESSAGE_TITLE => Lang.wildcardsMessageTitle;

        public static string DEFAULT_MATCH_CODE_TEXT => Lang.matchCodeNone;
        public static string DEFAULT_MATCH_CODE_PREFIX => Lang.matchCodePrefix;

        public const string DEFAULT_CHAIN_INITIAL_VALUE = "0.00";
        public const string DEFAULT_BANKED_INITIAL_VALUE = "0.00";

        public static string DEFAULT_PLAYER_NAME => Lang.player;
        public static string DEFAULT_OTHER_PLAYER_NAME => Lang.otherPlayer;

        public static string DEFAULT_WAITING_MATCH_TEXT => Lang.lblGameWaitingForMatchStart;
        public static string DEFAULT_WAITING_QUESTION_TEXT => Lang.matchWaitingForQuestion;
        public static string DEFAULT_WAITING_TURN_TEXT => Lang.matchWaitingForYourTurn;

        public static string TURN_MY_TURN_TEXT => Lang.matchMyTurn;
        public static string TURN_OTHER_PLAYER_TEXT => Lang.matchOtherTurn;

        public const string DEFAULT_TIMER_TEXT = "--:--";

        public static string DEFAULT_NO_WILDCARD_NAME => Lang.wildcardNone;
        public static string DEFAULT_WILDCARD_NAME => Lang.wildcardGeneric;

        public static string DEFAULT_TIMEOUT_TEXT => Lang.matchTimeout;
        public static string DEFAULT_SELECT_ANSWER_TEXT => Lang.matchSelectAnswer;
        public static string DEFAULT_CORRECT_TEXT => Lang.matchCorrect;
        public static string DEFAULT_INCORRECT_TEXT => Lang.matchIncorrect;

        public static string DEFAULT_BANK_ERROR_MESSAGE => Lang.wildcardsLoadFailed;
        public static string DEFAULT_BANK_UNEXPECTED_ERROR_MESSAGE => Lang.wildcardsLoadUnexpected;

        public static string COIN_FLIP_TITLE => Lang.coinFlipTitle;
        public static string COIN_FLIP_HEADS_MESSAGE => Lang.coinFlipHeadsMessage;
        public static string COIN_FLIP_TAILS_MESSAGE => Lang.coinFlipTailsMessage;

        public const byte DIFFICULTY_EASY = 1;
        public const byte DIFFICULTY_MEDIUM = 2;
        public const byte DIFFICULTY_HARD = 3;

        public static string MATCH_WINNER_MESSAGE_FORMAT => Lang.matchWinnerMessageFormat;

        public static string PHASE_TITLE => Lang.phaseTitle;
        public static string PHASE_ROUND_FORMAT => Lang.phaseRoundFormat;
        public static string PHASE_DUEL_TEXT => Lang.phaseDuel;
        public static string PHASE_SPECIAL_EVENT_TEXT => Lang.phaseSpecialEvent;
        public static string PHASE_FINISHED_TEXT => Lang.phaseFinished;

        public const string SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE = "LIGHTNING_WILDCARD_AWARDED";
        public const string SPECIAL_EVENT_EXTRA_WILDCARD_CODE = "EXTRA_WILDCARD_AWARDED";

        public const string SPECIAL_EVENT_BOMB_QUESTION_CODE = "BOMB_QUESTION";
        public const string SPECIAL_EVENT_BOMB_APPLIED_CODE = "BOMB_QUESTION_APPLIED";
        public static string BOMB_UI_MESSAGE => Lang.msgSpecialEventBombQuestionDesc;

        public const string SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE = "SURPRISE_EXAM_STARTED";
        public const string SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE = "SURPRISE_EXAM_RESOLVED";

        public const int SURPRISE_EXAM_TIME_SECONDS = 20;
        public const int SURPRISE_EXAM_OVERLAY_AUTOHIDE_MS = 1200;

        public static string SURPRISE_EXAM_BANK_BLOCKED_MESSAGE => Lang.surpriseExamBankBlockedMessage;

        public static string TURN_INTRO_TITLE => Lang.turnIntroTitle;
        public static string TURN_INTRO_DESCRIPTION_TEMPLATE => Lang.turnIntroDescriptionTemplate;
        public static string TURN_ORDER_CHANGED_TITLE => Lang.turnOrderChangedTitle;
        public const int TURN_INTRO_STEP_DELAY_MS = 350;
        public const int TURN_INTRO_FINAL_DELAY_MS = 700;

        public static Brush BombTurnBrush => Brushes.DarkRed;
    }
}
