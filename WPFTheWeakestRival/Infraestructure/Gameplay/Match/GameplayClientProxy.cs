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
        private bool isDisposed;

        public GameplayClientProxy(GameplayServiceProxy.GameplayServiceClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public GameplayServiceProxy.GameplayServiceClient Client => client;

        public Task JoinMatchAsync(GameplayServiceProxy.GameplayJoinMatchRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return client.JoinMatchAsync(request);
        }

        public Task StartMatchAsync(GameplayServiceProxy.GameplayStartMatchRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return client.StartMatchAsync(request);
        }

        public Task SubmitAnswerAsync(GameplayServiceProxy.SubmitAnswerRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return client.SubmitAnswerAsync(request);
        }

        public Task<GameplayServiceProxy.BankResponse> BankAsync(GameplayServiceProxy.BankRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return client.BankAsync(request);
        }

        public Task CastVoteAsync(GameplayServiceProxy.CastVoteRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return client.CastVoteAsync(request);
        }

        public Task ChooseDuelOpponentAsync(GameplayServiceProxy.ChooseDuelOpponentRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return client.ChooseDuelOpponentAsync(request);
        }

        private void ValidateNotDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(GameplayClientProxy));
            }
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
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            CloseSafely();
        }
    }
}
