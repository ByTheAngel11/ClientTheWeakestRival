using System;
using System.ServiceModel;
using System.Threading.Tasks;
using log4net;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class GameplayClientProxy : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayClientProxy));

        private readonly GameplayServiceProxy.GameplayServiceClient client;

        public GameplayClientProxy(GameplayServiceProxy.GameplayServiceClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public GameplayServiceProxy.GameplayServiceClient Client => client;

        public Task JoinMatchAsync(GameplayServiceProxy.GameplayJoinMatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Task.Run(() => client.JoinMatch(request));
        }

        public Task StartMatchAsync(GameplayServiceProxy.GameplayStartMatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Task.Run(() => client.StartMatch(request));
        }

        public Task SubmitAnswerAsync(GameplayServiceProxy.SubmitAnswerRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Task.Run(() => client.SubmitAnswer(request));
        }

        public Task<GameplayServiceProxy.BankResponse> BankAsync(GameplayServiceProxy.BankRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Task.Run(() => client.Bank(request));
        }

        public Task CastVoteAsync(GameplayServiceProxy.CastVoteRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Task.Run(() => client.CastVote(request));
        }

        public Task ChooseDuelOpponentAsync(GameplayServiceProxy.ChooseDuelOpponentRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Task.Run(() => client.ChooseDuelOpponent(request));
        }

        public void CloseSafely()
        {
            try
            {
                if (client == null)
                {
                    return;
                }

                if (client.State == CommunicationState.Faulted)
                {
                    client.Abort();
                    return;
                }

                client.Close();
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayClientProxy.CloseSafely error.", ex);

                try
                {
                    client.Abort();
                }
                catch (Exception abortEx)
                {
                    Logger.Warn("GameplayClientProxy.Abort error.", abortEx);
                }
            }
        }

        public void Dispose()
        {
            CloseSafely();
        }
    }
}
