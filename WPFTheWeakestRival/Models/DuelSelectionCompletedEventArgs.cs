using System;

namespace WPFTheWeakestRival.Models
{
    public sealed class DuelSelectionCompletedEventArgs : EventArgs
    {
        public DuelSelectionCompletedEventArgs(
            int matchId,
            int weakestRivalUserId,
            int? targetUserId)
        {
            MatchId = matchId;
            WeakestRivalUserId = weakestRivalUserId;
            TargetUserId = targetUserId;
        }

        public int MatchId { get; }

        public int WeakestRivalUserId { get; }

        public int? TargetUserId { get; }
    }
}
