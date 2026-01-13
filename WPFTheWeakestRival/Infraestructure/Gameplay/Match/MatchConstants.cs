using System.Windows.Media;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal static class MatchConstants
    {
        public const string FAULT_CODE_MATCH_ALREADY_STARTED = "MATCH_ALREADY_STARTED";

        public const string DEFAULT_LOCALE = "es-MX";
        public const int MAX_QUESTIONS = 40;

        public const int QUESTION_TIME_SECONDS = 30;
        public const int TIMER_INTERVAL_SECONDS = 1;

        internal const string MATCH_FINISHED_LABEL_TEXT = "Partida finalizada";
        internal const string EXIT_TO_LOBBY_PROMPT = "La partida aún no ha terminado. ¿Deseas salir al lobby?";

        public const string TIMER_FORMAT = @"mm\:ss";
        public const string POINTS_FORMAT = "0.00";

        public const string GAME_MESSAGE_TITLE = "Juego";
        public const string WILDCARDS_MESSAGE_TITLE = "Wildcards";

        public const string DEFAULT_MATCH_CODE_TEXT = "Sin código";
        public const string DEFAULT_MATCH_CODE_PREFIX = "Código: ";

        public const string DEFAULT_CHAIN_INITIAL_VALUE = "0.00";
        public const string DEFAULT_BANKED_INITIAL_VALUE = "0.00";

        public const string DEFAULT_PLAYER_NAME = "Jugador";
        public const string DEFAULT_OTHER_PLAYER_NAME = "Otro jugador";

        public const string DEFAULT_WAITING_MATCH_TEXT = "Esperando inicio de partida...";
        public const string DEFAULT_WAITING_QUESTION_TEXT = "(esperando pregunta...)";
        public const string DEFAULT_WAITING_TURN_TEXT = "Esperando tu turno...";

        public const string TURN_MY_TURN_TEXT = "Tu turno";
        public const string TURN_OTHER_PLAYER_TEXT = "Turno de otro jugador";

        public const string DEFAULT_TIMER_TEXT = "--:--";

        public const string DEFAULT_NO_WILDCARD_NAME = "Sin comodín";
        public const string DEFAULT_WILDCARD_NAME = "Comodín";

        public const string DEFAULT_TIMEOUT_TEXT = "Tiempo agotado.";
        public const string DEFAULT_SELECT_ANSWER_TEXT = "Selecciona una respuesta.";
        public const string DEFAULT_CORRECT_TEXT = "¡Correcto!";
        public const string DEFAULT_INCORRECT_TEXT = "Respuesta incorrecta.";

        public const string DEFAULT_BANK_ERROR_MESSAGE = "No se pudieron cargar los comodines.";
        public const string DEFAULT_BANK_UNEXPECTED_ERROR_MESSAGE = "Ocurrió un error al cargar los comodines.";

        public const string COIN_FLIP_TITLE = "Volado";
        public const string COIN_FLIP_HEADS_MESSAGE = "¡Cara! Habrá duelo.";
        public const string COIN_FLIP_TAILS_MESSAGE = "Cruz. No habrá duelo.";

        public const byte DIFFICULTY_EASY = 1;
        public const byte DIFFICULTY_MEDIUM = 2;
        public const byte DIFFICULTY_HARD = 3;

        public const string MATCH_WINNER_MESSAGE_FORMAT = "El juego ha terminado. El ganador es: {0}.";

        public const string PHASE_TITLE = "Fase";
        public const string PHASE_ROUND_FORMAT = "Ronda {0}";
        public const string PHASE_DUEL_TEXT = "Duelo";
        public const string PHASE_SPECIAL_EVENT_TEXT = "Evento especial";
        public const string PHASE_FINISHED_TEXT = "Finalizada";

        public const string SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE = "LIGHTNING_WILDCARD_AWARDED";
        public const string SPECIAL_EVENT_EXTRA_WILDCARD_CODE = "EXTRA_WILDCARD_AWARDED";

        public const string SPECIAL_EVENT_BOMB_QUESTION_CODE = "BOMB_QUESTION";
        public const string SPECIAL_EVENT_BOMB_APPLIED_CODE = "BOMB_QUESTION_APPLIED";
        public const string BOMB_UI_MESSAGE = "PREGUNTA BOMBA: Acierto +0.50 a banca, fallo -0.50.";

        public const string SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE = "SURPRISE_EXAM_STARTED";
        public const string SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE = "SURPRISE_EXAM_RESOLVED";

        public const int SURPRISE_EXAM_TIME_SECONDS = 20;
        public const int SURPRISE_EXAM_OVERLAY_AUTOHIDE_MS = 1200;

        public const string SURPRISE_EXAM_BANK_BLOCKED_MESSAGE =
            "Examen sorpresa en curso. No se puede bancar.";

        public const string TURN_INTRO_TITLE = "Turnos";
        public const string TURN_INTRO_DESCRIPTION_TEMPLATE = "Orden: {0}\nInicia: {1}";
        public const string TURN_ORDER_CHANGED_TITLE = "Turnos actualizados";
        public const int TURN_INTRO_STEP_DELAY_MS = 350;
        public const int TURN_INTRO_FINAL_DELAY_MS = 700;

        public static Brush BombTurnBrush => Brushes.DarkRed;
    }
}
