using System;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal static class MatchDarknessConstants
    {
        public const string SpecialEventDarknessStartedCode = "DARKNESS_STARTED";
        public const string SpecialEventDarknessEndedCode = "DARKNESS_ENDED";

        public const string DarknessOverlayTitle = "A oscuras";
        public const string DarknessOverlayDescription =
            "Las luces se apagaron. No puedes ver quién es quién hasta después de la votación.";

        public const string DarknessEndedOverlayTitle = "Luces encendidas";
        public const string DarknessEndedOverlayDescription =
            "Se revelan las identidades nuevamente.";

        public const string UnknownPlayerName = "???";
        public const string DarknessMyTurnText = "A oscuras: tu turno";
        public const string DarknessOtherTurnText = "A oscuras: esperando turno";

        public const string DarknessVoteWindowTitle = "Votación (a oscuras)";

        public const string DarknessSlotFormat = "Sombra {0}";

        public const string DarknessSomeoneAnsweredCorrect = "Alguien respondió correcto.";
        public const string DarknessSomeoneAnsweredIncorrect = "Alguien respondió incorrecto.";

        public const string DarknessRevealVoteFormat = "Votaste por: {0}";

        public const int HashSeed = 17;
        public const int HashMultiplier = 31;
    }
}
