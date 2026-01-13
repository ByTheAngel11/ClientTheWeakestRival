using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using log4net;
using GameplayServiceProxy = WPFTheWeakestRival.GameplayService;

namespace WPFTheWeakestRival.Infrastructure.Gameplay
{
    public sealed class GameplayHub : IDisposable, IStoppable
    {
        private const string ERROR_ENDPOINT_REQUIRED = "Endpoint name cannot be null or whitespace.";

        private const string LOG_UI_ERROR = "GameplayHub.Ui error.";
        private const string LOG_CHANNEL_FAULTED = "GameplayHub called with channel Faulted.";

        private const int RECONNECT_INTERVAL_SECONDS = 5;
        private const int RECONNECT_TEST_TIMEOUT_SECONDS = 3;

        private const string ENDPOINT_GAMEPLAY_NETTCP = "NetTcpBinding_IGameplayService";
        private const string ENDPOINT_GAMEPLAY_WSDUAL_LEGACY = "WSDualHttpBinding_IGameplayService";

        private const string LOG_ENDPOINT_REMAPPED = "GameplayHub endpoint remapped from {0} to {1}.";

        private const string FAULT_CODE_DATABASE = "Error de base de datos";
        private const string FAULT_MESSAGE_DATABASE_MARKER = "base de datos";

        private static readonly TimeSpan RECONNECT_INTERVAL = TimeSpan.FromSeconds(RECONNECT_INTERVAL_SECONDS);
        private static readonly TimeSpan RECONNECT_TEST_TIMEOUT = TimeSpan.FromSeconds(RECONNECT_TEST_TIMEOUT_SECONDS);

        private static readonly DispatcherPriority UI_DISPATCHER_PRIORITY = DispatcherPriority.Send;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayHub));

        private readonly Dispatcher dispatcher;
        private readonly object reconnectSyncRoot = new object();

        private GameplayServiceProxy.GameplayServiceClient client;
        private DispatcherTimer reconnectTimer;

        private bool isStoppedOrDisposed;
        private bool isReconnectInProgress;

        private readonly string endpointName;
        private readonly GameplayCallbackBridge callbackBridge;

        private GameplayServiceProxy.GameplayJoinMatchRequest lastJoinRequest;

        public event Action<Exception> ConnectionLost;
        public event Action ConnectionRestored;

        public event Action<string> DatabaseErrorDetected;

        public GameplayHub(string endpointName)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException(ERROR_ENDPOINT_REQUIRED, nameof(endpointName));
            }

            dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            this.endpointName = ResolveEndpointName(endpointName);

            callbackBridge = new GameplayCallbackBridge(dispatcher);

            RecreateClient();
        }

        public GameplayServiceProxy.GameplayServiceClient RawClient => client;

        internal GameplayCallbackBridge Callbacks => callbackBridge;

        public async Task<GameplayServiceProxy.GameplayJoinMatchResponse> JoinMatchAsync(
            GameplayServiceProxy.GameplayJoinMatchRequest request)
        {
            if (request == null || isStoppedOrDisposed)
            {
                return new GameplayServiceProxy.GameplayJoinMatchResponse();
            }

            lastJoinRequest = request;

            if (IsClientFaulted(LOG_CHANNEL_FAULTED))
            {
                StartReconnectLoop();
                return new GameplayServiceProxy.GameplayJoinMatchResponse();
            }

            try
            {
                GameplayServiceProxy.GameplayJoinMatchResponse response =
                    await Task.Run(() => client.JoinMatch(request)).ConfigureAwait(false);

                StopReconnectLoop();
                return response ?? new GameplayServiceProxy.GameplayJoinMatchResponse();
            }
            catch (FaultException ex)
            {
                Logger.Warn("JoinMatchAsync fault.", ex);

                if (IsDatabaseFault(ex.Message))
                {
                    RaiseDatabaseErrorDetected(ex.Message);
                }

                return new GameplayServiceProxy.GameplayJoinMatchResponse();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("JoinMatchAsync communication error.", ex);
                OnChannelDead("JoinMatchAsync communication error.", ex);
                return new GameplayServiceProxy.GameplayJoinMatchResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("JoinMatchAsync timeout.", ex);
                OnChannelDead("JoinMatchAsync timeout.", ex);
                return new GameplayServiceProxy.GameplayJoinMatchResponse();
            }
            catch (Exception ex)
            {
                Logger.Error("JoinMatchAsync unexpected error.", ex);
                OnChannelDead("JoinMatchAsync unexpected error.", ex);
                return new GameplayServiceProxy.GameplayJoinMatchResponse();
            }
        }

        public async Task StartMatchAsync(GameplayServiceProxy.GameplayStartMatchRequest request)
        {
            if (request == null || isStoppedOrDisposed)
            {
                return;
            }

            if (IsClientFaulted(LOG_CHANNEL_FAULTED))
            {
                StartReconnectLoop();
                throw new CommunicationException("Gameplay channel faulted.");
            }

            try
            {
                await Task.Run(() => client.StartMatch(request)).ConfigureAwait(false);
                StopReconnectLoop();
            }
            catch (FaultException ex)
            {
                Logger.Warn("StartMatchAsync fault.", ex);

                if (IsDatabaseFault(ex.Message))
                {
                    StopReconnectLoop();
                    RaiseDatabaseErrorDetected(ex.Message);
                }

                throw;
            }
            catch (CommunicationException ex)
            {
                OnChannelDead("StartMatchAsync communication error.", ex);
                throw;
            }
            catch (TimeoutException ex)
            {
                OnChannelDead("StartMatchAsync timeout.", ex);
                throw;
            }
        }

        public async Task<GameplayServiceProxy.SubmitAnswerResponse> SubmitAnswerAsync(
            GameplayServiceProxy.SubmitAnswerRequest request)
        {
            if (request == null || isStoppedOrDisposed)
            {
                return new GameplayServiceProxy.SubmitAnswerResponse();
            }

            if (IsClientFaulted(LOG_CHANNEL_FAULTED))
            {
                StartReconnectLoop();
                throw new CommunicationException("Gameplay channel faulted.");
            }

            try
            {
                GameplayServiceProxy.SubmitAnswerResponse response =
                    await Task.Run(() => client.SubmitAnswer(request)).ConfigureAwait(false);

                StopReconnectLoop();
                return response ?? new GameplayServiceProxy.SubmitAnswerResponse();
            }
            catch (FaultException ex)
            {
                Logger.Warn("SubmitAnswerAsync fault.", ex);

                if (IsDatabaseFault(ex.Message))
                {
                    StopReconnectLoop();
                    RaiseDatabaseErrorDetected(ex.Message);
                }

                throw;
            }
            catch (CommunicationException ex)
            {
                OnChannelDead("SubmitAnswerAsync communication error.", ex);
                throw;
            }
            catch (TimeoutException ex)
            {
                OnChannelDead("SubmitAnswerAsync timeout.", ex);
                throw;
            }
        }

        public async Task<GameplayServiceProxy.BankResponse> BankAsync(GameplayServiceProxy.BankRequest request)
        {
            if (request == null || isStoppedOrDisposed)
            {
                return new GameplayServiceProxy.BankResponse();
            }

            if (IsClientFaulted(LOG_CHANNEL_FAULTED))
            {
                StartReconnectLoop();
                throw new CommunicationException("Gameplay channel faulted.");
            }

            try
            {
                GameplayServiceProxy.BankResponse response =
                    await Task.Run(() => client.Bank(request)).ConfigureAwait(false);

                StopReconnectLoop();
                return response ?? new GameplayServiceProxy.BankResponse();
            }
            catch (FaultException ex)
            {
                Logger.Warn("BankAsync fault.", ex);

                if (IsDatabaseFault(ex.Message))
                {
                    StopReconnectLoop();
                    RaiseDatabaseErrorDetected(ex.Message);
                }

                throw;
            }
            catch (CommunicationException ex)
            {
                OnChannelDead("BankAsync communication error.", ex);
                throw;
            }
            catch (TimeoutException ex)
            {
                OnChannelDead("BankAsync timeout.", ex);
                throw;
            }
        }

        public async Task CastVoteAsync(GameplayServiceProxy.CastVoteRequest request)
        {
            if (request == null || isStoppedOrDisposed)
            {
                return;
            }

            if (IsClientFaulted(LOG_CHANNEL_FAULTED))
            {
                StartReconnectLoop();
                throw new CommunicationException("Gameplay channel faulted.");
            }

            try
            {
                await Task.Run(() => client.CastVote(request)).ConfigureAwait(false);
                StopReconnectLoop();
            }
            catch (FaultException ex)
            {
                Logger.Warn("CastVoteAsync fault.", ex);

                if (IsDatabaseFault(ex.Message))
                {
                    StopReconnectLoop();
                    RaiseDatabaseErrorDetected(ex.Message);
                }

                throw;
            }
            catch (CommunicationException ex)
            {
                OnChannelDead("CastVoteAsync communication error.", ex);
                throw;
            }
            catch (TimeoutException ex)
            {
                OnChannelDead("CastVoteAsync timeout.", ex);
                throw;
            }
        }

        public async Task ChooseDuelOpponentAsync(GameplayServiceProxy.ChooseDuelOpponentRequest request)
        {
            if (request == null || isStoppedOrDisposed)
            {
                return;
            }

            if (IsClientFaulted(LOG_CHANNEL_FAULTED))
            {
                StartReconnectLoop();
                throw new CommunicationException("Gameplay channel faulted.");
            }

            try
            {
                await Task.Run(() => client.ChooseDuelOpponent(request)).ConfigureAwait(false);
                StopReconnectLoop();
            }
            catch (FaultException ex)
            {
                Logger.Warn("ChooseDuelOpponentAsync fault.", ex);

                if (IsDatabaseFault(ex.Message))
                {
                    StopReconnectLoop();
                    RaiseDatabaseErrorDetected(ex.Message);
                }

                throw;
            }
            catch (CommunicationException ex)
            {
                OnChannelDead("ChooseDuelOpponentAsync communication error.", ex);
                throw;
            }
            catch (TimeoutException ex)
            {
                OnChannelDead("ChooseDuelOpponentAsync timeout.", ex);
                throw;
            }
        }

        public void Stop()
        {
            if (isStoppedOrDisposed)
            {
                return;
            }

            isStoppedOrDisposed = true;

            StopReconnectLoop();

            try
            {
                if (client != null)
                {
                    if (client.State == CommunicationState.Faulted)
                    {
                        client.Abort();
                    }
                    else
                    {
                        client.Close();
                    }
                }
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CommunicationException while stopping GameplayHub. Aborting.", ex);
                SafeAbort();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("TimeoutException while stopping GameplayHub. Aborting.", ex);
                SafeAbort();
            }
            catch (Exception ex)
            {
                Logger.Warn("Unexpected error while stopping GameplayHub. Aborting.", ex);
                SafeAbort();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void Ui(Action action)
        {
            if (action == null || isStoppedOrDisposed)
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

                dispatcher.BeginInvoke(action, UI_DISPATCHER_PRIORITY);
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_UI_ERROR, ex);
            }
        }

        private void SafeAbort()
        {
            try
            {
                if (client != null)
                {
                    client.Abort();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error while aborting GameplayHub client.", ex);
            }
        }

        private void RecreateClient()
        {
            SafeAbort();

            var instanceContext = new InstanceContext(callbackBridge);
            client = new GameplayServiceProxy.GameplayServiceClient(instanceContext, endpointName);

            AttachChannelEvents();
        }

        private void AttachChannelEvents()
        {
            try
            {
                ICommunicationObject channel = client.InnerChannel;

                channel.Faulted += (_, __) => OnChannelDead("GameplayHub channel faulted.", null);
                channel.Closed += (_, __) => OnChannelDead("GameplayHub channel closed.", null);
            }
            catch (Exception ex)
            {
                Logger.Warn("AttachChannelEvents failed.", ex);
            }
        }

        private void OnChannelDead(string reason, Exception ex)
        {
            if (isStoppedOrDisposed)
            {
                return;
            }

            var effective = ex ?? new CommunicationException(reason);

            Logger.Warn("GameplayHub channel dead.", effective);

            Ui(() => ConnectionLost?.Invoke(effective));

            StartReconnectLoop();
        }

        private void StartReconnectLoop()
        {
            if (isStoppedOrDisposed || !CanUseDispatcher())
            {
                return;
            }

            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(StartReconnectLoop), UI_DISPATCHER_PRIORITY);
                return;
            }

            lock (reconnectSyncRoot)
            {
                if (reconnectTimer == null)
                {
                    reconnectTimer = new DispatcherTimer(UI_DISPATCHER_PRIORITY, dispatcher)
                    {
                        Interval = RECONNECT_INTERVAL
                    };

                    reconnectTimer.Tick += ReconnectTimerTick;
                }

                if (!reconnectTimer.IsEnabled)
                {
                    reconnectTimer.Start();
                }
            }
        }

        private void StopReconnectLoop()
        {
            if (!CanUseDispatcher())
            {
                return;
            }

            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(StopReconnectLoop), UI_DISPATCHER_PRIORITY);
                return;
            }

            lock (reconnectSyncRoot)
            {
                if (reconnectTimer != null && reconnectTimer.IsEnabled)
                {
                    reconnectTimer.Stop();
                }
            }
        }

        private async void ReconnectTimerTick(object sender, EventArgs e)
        {
            if (isStoppedOrDisposed)
            {
                StopReconnectLoop();
                return;
            }

            lock (reconnectSyncRoot)
            {
                if (isReconnectInProgress)
                {
                    return;
                }

                isReconnectInProgress = true;
            }

            try
            {
                await TryReconnectAsync().ConfigureAwait(false);
            }
            finally
            {
                lock (reconnectSyncRoot)
                {
                    isReconnectInProgress = false;
                }
            }
        }

        private async Task TryReconnectAsync()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return;
            }

            if (isStoppedOrDisposed)
            {
                StopReconnectLoop();
                return;
            }

            if (lastJoinRequest == null)
            {
                return;
            }

            try
            {
                bool mustRecreate =
                    client == null ||
                    client.State != CommunicationState.Opened;

                if (mustRecreate)
                {
                    RecreateClient();
                }

                try
                {
                    client.InnerChannel.OperationTimeout = RECONNECT_TEST_TIMEOUT;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Setting OperationTimeout failed.", ex);
                }

                try
                {
                    _ = await Task.Run(() => client.JoinMatch(lastJoinRequest)).ConfigureAwait(false);
                }
                catch (FaultException ex)
                {
                    Logger.Warn("GameplayHub reconnect fault.", ex);

                    StopReconnectLoop();

                    if (IsDatabaseFault(ex.Message))
                    {
                        RaiseDatabaseErrorDetected(ex.Message);
                    }

                    return;
                }

                Logger.Info("GameplayHub reconnected successfully.");
                StopReconnectLoop();

                Ui(() => ConnectionRestored?.Invoke());
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("GameplayHub reconnect attempt communication error.", ex);
                SafeAbort();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("GameplayHub reconnect attempt timeout.", ex);
                SafeAbort();
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayHub reconnect attempt failed.", ex);
                SafeAbort();
            }
        }

        private static bool IsDatabaseFault(string faultMessage)
        {
            string message = faultMessage ?? string.Empty;

            if (message.IndexOf(FAULT_CODE_DATABASE, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return message.IndexOf(FAULT_MESSAGE_DATABASE_MARKER, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RaiseDatabaseErrorDetected(string message)
        {
            try
            {
                Ui(() => DatabaseErrorDetected?.Invoke(message ?? string.Empty));
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayHub.RaiseDatabaseErrorDetected failed.", ex);
            }
        }

        private bool IsClientFaulted(string logMessage)
        {
            if (client == null || client.State != CommunicationState.Faulted)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(logMessage))
            {
                Logger.Warn(logMessage);
            }

            return true;
        }

        private static string ResolveEndpointName(string requestedEndpointName)
        {
            string safeName = (requestedEndpointName ?? string.Empty).Trim();

            if (string.Equals(safeName, ENDPOINT_GAMEPLAY_WSDUAL_LEGACY, StringComparison.Ordinal))
            {
                Logger.WarnFormat(LOG_ENDPOINT_REMAPPED, safeName, ENDPOINT_GAMEPLAY_NETTCP);
                return ENDPOINT_GAMEPLAY_NETTCP;
            }

            return safeName;
        }

        private bool CanUseDispatcher()
        {
            return dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished;
        }
    }
}
