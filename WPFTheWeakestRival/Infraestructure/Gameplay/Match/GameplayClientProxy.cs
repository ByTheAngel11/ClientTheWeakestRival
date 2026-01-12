using System;
using System.Threading.Tasks;
using log4net;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class GameplayClientProxy : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayClientProxy));

        private const string ERROR_HUB_REQUIRED = "GameplayHub is required.";
        private const string ERROR_CLIENT_NOT_AVAILABLE = "Gameplay client is not available.";

        private readonly WPFTheWeakestRival.Infrastructure.Gameplay.GameplayHub hub;
        private bool isDisposed;

        public GameplayClientProxy(WPFTheWeakestRival.Infrastructure.Gameplay.GameplayHub hub)
        {
            if (hub == null)
            {
                throw new ArgumentNullException(nameof(hub), ERROR_HUB_REQUIRED);
            }

            this.hub = hub;
        }

        private GameplayServiceProxy.GameplayServiceClient Client
        {
            get
            {
                GameplayServiceProxy.GameplayServiceClient client = hub.RawClient;
                if (client == null)
                {
                    throw new InvalidOperationException(ERROR_CLIENT_NOT_AVAILABLE);
                }

                return client;
            }
        }

        public Task<GameplayServiceProxy.GameplayJoinMatchResponse> JoinMatchAsync(
            GameplayServiceProxy.GameplayJoinMatchRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return hub.JoinMatchAsync(request);
        }

        public Task StartMatchAsync(GameplayServiceProxy.GameplayStartMatchRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Client.StartMatchAsync(request);
        }

        public Task<GameplayServiceProxy.SubmitAnswerResponse> SubmitAnswerAsync(
            GameplayServiceProxy.SubmitAnswerRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Client.SubmitAnswerAsync(request);
        }

        public Task<GameplayServiceProxy.BankResponse> BankAsync(GameplayServiceProxy.BankRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Client.BankAsync(request);
        }

        public Task CastVoteAsync(GameplayServiceProxy.CastVoteRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Client.CastVoteAsync(request);
        }

        public Task ChooseDuelOpponentAsync(GameplayServiceProxy.ChooseDuelOpponentRequest request)
        {
            ValidateNotDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            return Client.ChooseDuelOpponentAsync(request);
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
                hub.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayClientProxy.CloseSafely error.", ex);
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
