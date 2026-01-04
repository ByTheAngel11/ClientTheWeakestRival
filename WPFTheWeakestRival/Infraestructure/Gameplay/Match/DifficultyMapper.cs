namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal static class DifficultyMapper
    {
        public static byte MapDifficultyToByte(string difficultyCode)
        {
            if (string.IsNullOrWhiteSpace(difficultyCode))
            {
                return MatchConstants.DIFFICULTY_EASY;
            }

            string code = difficultyCode.Trim().ToUpperInvariant();

            switch (code)
            {
                case "EASY":
                case "E":
                    return MatchConstants.DIFFICULTY_EASY;

                case "NORMAL":
                case "MEDIUM":
                case "M":
                    return MatchConstants.DIFFICULTY_MEDIUM;

                case "HARD":
                case "H":
                    return MatchConstants.DIFFICULTY_HARD;

                default:
                    return MatchConstants.DIFFICULTY_EASY;
            }
        }
    }
}
