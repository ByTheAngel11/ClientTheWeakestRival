namespace WPFTheWeakestRival.Models
{
    public sealed class PlayerVoteItem
    {
        public int UserId { get; set; }

        public string DisplayName { get; set; }

        public bool IsEliminated { get; set; }

        public decimal BankedPoints { get; set; }

        public int CorrectAnswers { get; set; }

        public int WrongAnswers { get; set; }

        public override string ToString()
        {
            return string.Format(
                "{0}  |  Bank: {1:0.00}  |  ✓ {2}  ✗ {3}",
                DisplayName,
                BankedPoints,
                CorrectAnswers,
                WrongAnswers);
        }
    }
}
