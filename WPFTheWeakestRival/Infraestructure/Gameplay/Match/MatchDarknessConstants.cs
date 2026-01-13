using System;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal static class MatchDarknessConstants
    {
        public const string SPECIAL_EVENT_DARKNESS_STARTED_CODE = "DARKNESS_STARTED";
        public const string SPECIAL_EVENT_DARKNESS_ENDED_CODE = "DARKNESS_ENDED";

        public const string DARKNESS_OVERLAY_TITLE = "A oscuras";
        public const string DARKNESS_OVERLAY_DESCRIPTION =
            "Las luces se apagaron. No puedes ver quién es quién hasta después de la votación.";

        public const string DARKNESS_ENDED_OVERLAY_TITLE = "Luces encendidas";
        public const string DARKNESS_ENDED_OVERLAY_DESCRIPTION =
            "Se revelan las identidades nuevamente.";

        public const string UNKNOWN_PLAYER_NAME = "???";
        public const string DARKNESS_MY_TURN_TEXT = "A oscuras: tu turno";
        public const string DARKNESS_OTHER_TURN_TEXT = "A oscuras: esperando turno";

        public const string DARKNESS_VOTE_WINDOW_TITLE = "Votación (a oscuras)";

        public const string DARKNESS_SLOT_FORMAT = "Sombra {0}";

        public const string DARKNESS_SOMEONE_ANSWERED_CORRECT = "Alguien respondió correcto.";
        public const string DARKNESS_SOMEONE_ANSWERED_INCORRECT = "Alguien respondió incorrecto.";

        public const string DARKNESS_REVEAL_VOTE_FORMAT = "Votaste por: {0}";

        public const int HASH_SEED = 17;
        public const int HASH_MULTIPLIER = 31;
    }
}
