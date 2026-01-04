using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Threading;
using log4net;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay
{
    internal sealed class GameplayCallbackBridge : GameplayServiceProxy.IGameplayServiceCallback
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayCallbackBridge));

        private readonly Dispatcher dispatcher;

        public GameplayCallbackBridge(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher
                ?? Application.Current?.Dispatcher
                ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public event Action<Guid, GameplayServiceProxy.PlayerSummary, GameplayServiceProxy.QuestionWithAnswersDto, decimal, decimal> NextQuestion;
        public event Action<Guid, GameplayServiceProxy.PlayerSummary, GameplayServiceProxy.AnswerResult> AnswerEvaluated;
        public event Action<Guid, GameplayServiceProxy.BankState> BankUpdated;
        public event Action<Guid, TimeSpan> VotePhaseStarted;
        public event Action<Guid, GameplayServiceProxy.PlayerSummary> Elimination;
        public event Action<Guid, string, string> SpecialEvent;
        public event Action<Guid, GameplayServiceProxy.CoinFlipResolvedDto> CoinFlipResolved;
        public event Action<Guid, GameplayServiceProxy.DuelCandidatesDto> DuelCandidates;
        public event Action<Guid, GameplayServiceProxy.PlayerSummary> MatchFinished;

        public event Action<Guid, Guid, GameplayServiceProxy.PlayerSummary, int, int> LightningChallengeStarted;
        public event Action<Guid, Guid, int, GameplayServiceProxy.QuestionWithAnswersDto> LightningChallengeQuestion;
        public event Action<Guid, Guid, int, bool> LightningChallengeFinished;

        public event Action<Guid, GameplayServiceProxy.TurnOrderDto> TurnOrderInitialized;
        public event Action<Guid, GameplayServiceProxy.TurnOrderDto, string> TurnOrderChanged;

        internal void Ui(Action action)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                if (dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                dispatcher.BeginInvoke(action);
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayCallbackBridge.Ui error.", ex);
            }
        }

        public void OnNextQuestion(
            Guid matchId,
            GameplayServiceProxy.PlayerSummary targetPlayer,
            GameplayServiceProxy.QuestionWithAnswersDto question,
            decimal currentChain,
            decimal banked)
        {
            Ui(() =>
            {
                try
                {
                    NextQuestion?.Invoke(matchId, targetPlayer, question, currentChain, banked);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback NextQuestion handler error.", ex);
                }
            });
        }

        public void OnAnswerEvaluated(
            Guid matchId,
            GameplayServiceProxy.PlayerSummary player,
            GameplayServiceProxy.AnswerResult result)
        {
            Ui(() =>
            {
                try
                {
                    AnswerEvaluated?.Invoke(matchId, player, result);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback AnswerEvaluated handler error.", ex);
                }
            });
        }

        public void OnBankUpdated(Guid matchId, GameplayServiceProxy.BankState bank)
        {
            Ui(() =>
            {
                try
                {
                    BankUpdated?.Invoke(matchId, bank);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback BankUpdated handler error.", ex);
                }
            });
        }

        public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit)
        {
            Ui(() =>
            {
                try
                {
                    VotePhaseStarted?.Invoke(matchId, timeLimit);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback VotePhaseStarted handler error.", ex);
                }
            });
        }

        public void OnElimination(Guid matchId, GameplayServiceProxy.PlayerSummary eliminatedPlayer)
        {
            Ui(() =>
            {
                try
                {
                    Elimination?.Invoke(matchId, eliminatedPlayer);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback Elimination handler error.", ex);
                }
            });
        }

        public void OnSpecialEvent(Guid matchId, string eventName, string description)
        {
            Ui(() =>
            {
                try
                {
                    SpecialEvent?.Invoke(matchId, eventName, description);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback SpecialEvent handler error.", ex);
                }
            });
        }

        public void OnCoinFlipResolved(Guid matchId, GameplayServiceProxy.CoinFlipResolvedDto coinFlip)
        {
            Ui(() =>
            {
                try
                {
                    CoinFlipResolved?.Invoke(matchId, coinFlip);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback CoinFlipResolved handler error.", ex);
                }
            });
        }

        public void OnDuelCandidates(Guid matchId, GameplayServiceProxy.DuelCandidatesDto duelCandidates)
        {
            Ui(() =>
            {
                try
                {
                    DuelCandidates?.Invoke(matchId, duelCandidates);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback DuelCandidates handler error.", ex);
                }
            });
        }

        public void OnMatchFinished(Guid matchId, GameplayServiceProxy.PlayerSummary winner)
        {
            Ui(() =>
            {
                try
                {
                    MatchFinished?.Invoke(matchId, winner);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback MatchFinished handler error.", ex);
                }
            });
        }

        public void OnLightningChallengeStarted(
            Guid matchId,
            Guid roundId,
            GameplayServiceProxy.PlayerSummary targetPlayer,
            int totalQuestions,
            int totalTimeSeconds)
        {
            Ui(() =>
            {
                try
                {
                    LightningChallengeStarted?.Invoke(matchId, roundId, targetPlayer, totalQuestions, totalTimeSeconds);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback LightningChallengeStarted handler error.", ex);
                }
            });
        }

        public void OnLightningChallengeQuestion(
            Guid matchId,
            Guid roundId,
            int questionIndex,
            GameplayServiceProxy.QuestionWithAnswersDto question)
        {
            Ui(() =>
            {
                try
                {
                    LightningChallengeQuestion?.Invoke(matchId, roundId, questionIndex, question);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback LightningChallengeQuestion handler error.", ex);
                }
            });
        }

        public void OnLightningChallengeFinished(
            Guid matchId,
            Guid roundId,
            int correctAnswers,
            bool isSuccess)
        {
            Ui(() =>
            {
                try
                {
                    LightningChallengeFinished?.Invoke(matchId, roundId, correctAnswers, isSuccess);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback LightningChallengeFinished handler error.", ex);
                }
            });
        }

        public void OnTurnOrderInitialized(Guid matchId, GameplayServiceProxy.TurnOrderDto turnOrder)
        {
            Ui(() =>
            {
                try
                {
                    TurnOrderInitialized?.Invoke(matchId, turnOrder);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback TurnOrderInitialized handler error.", ex);
                }
            });
        }

        public void OnTurnOrderChanged(Guid matchId, GameplayServiceProxy.TurnOrderDto turnOrder, string reasonCode)
        {
            Ui(() =>
            {
                try
                {
                    TurnOrderChanged?.Invoke(matchId, turnOrder, reasonCode);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Callback TurnOrderChanged handler error.", ex);
                }
            });
        }
    }
}
