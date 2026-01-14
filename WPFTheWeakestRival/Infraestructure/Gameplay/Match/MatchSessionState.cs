    using System;
    using System.Collections.Generic;
    using System.Linq;
    using WPFTheWeakestRival.LobbyService;
    using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

    namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
    {
        internal enum MatchPhase
        {
            NormalRound,
            Duel,
            SpecialEvent,
            Final,
            Finished
        }

        internal sealed class MatchSessionState
        {
            private readonly HashSet<int> eliminatedUserIds = new HashSet<int>();
            private readonly List<int> eliminationOrder = new List<int>();

            public MatchSessionState(MatchInfo match, string token, int myUserId, bool isHost)
            {
                Match = match ?? throw new ArgumentNullException(nameof(match));
                Token = token;
                MyUserId = myUserId;
                IsHost = isHost;

                MatchDbId = match.MatchDbId;
            }

            public MatchInfo Match { get; }
            public string Token { get; }
            public int MyUserId { get; }
            public bool IsHost { get; }

            public int MatchDbId { get; }

            public bool IsMyTurn { get; set; }
            public int CurrentTurnUserId { get; set; }

            public bool IsBombQuestionActive { get; set; }
            public bool IsSurpriseExamActive { get; set; }

            public bool IsDarknessActive { get; set; }
            public int? PendingDarknessVotedUserId { get; set; }

            public int CurrentRoundNumber { get; set; } = 1;
            public MatchPhase CurrentPhase { get; set; } = MatchPhase.NormalRound;

            public GameplayServiceProxy.QuestionWithAnswersDto CurrentQuestion { get; set; }

            public int MyCorrectAnswers { get; set; }
            public int MyTotalAnswers { get; set; }

            public bool IsMatchFinished { get; set; }
            public GameplayServiceProxy.PlayerSummary FinalWinner { get; set; }

            public IReadOnlyCollection<int> EliminatedUserIds => eliminatedUserIds;
            public IReadOnlyList<int> EliminationOrder => eliminationOrder;

            public int? DarknessSeed { get; set; }

            public bool HasAnnouncedFinalPhase { get; set; }

            public void AddEliminated(int userId)
            {
                if (userId <= 0)
                {
                    return;
                }

                eliminatedUserIds.Add(userId);

                if (!eliminationOrder.Contains(userId))
                {
                    eliminationOrder.Add(userId);
                }
            }

            public bool IsEliminated(int userId)
            {
                return userId > 0 && eliminatedUserIds.Contains(userId);
            }

            public int GetAlivePlayersCount()
            {
                PlayerSummary[] players = Match != null ? (Match.Players ?? Array.Empty<PlayerSummary>()) : Array.Empty<PlayerSummary>();

                return players
                    .Where(p => p != null && p.UserId > 0 && !IsEliminated(p.UserId))
                    .Select(p => p.UserId)
                    .Distinct()
                    .Count();
            }

            public bool IsInFinalPhase()
            {
                return CurrentPhase == MatchPhase.Final;
            }
        }
    }
