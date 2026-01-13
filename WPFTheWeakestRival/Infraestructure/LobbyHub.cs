using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using log4net;
using WPFTheWeakestRival.LobbyService;
using System.Net.NetworkInformation;
using System.Globalization;

namespace WPFTheWeakestRival.Infrastructure
{
    [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public sealed class LobbyHub : ILobbyServiceCallback, IDisposable, IStoppable
    {
        private const byte DefaultMaxPlayers = 8;
        private const byte MinAllowedMaxPlayers = 1;

        private const string ErrorTokenRequired = "Token requerido.";
        private const string ErrorEndpointRequired = "Endpoint name cannot be null or whitespace.";

        private const string LogUiError = "LobbyHub.Ui error.";
        private const string LogChannelFaultedCreate = "CreateLobbyAsync called with channel Faulted.";
        private const string LogChannelFaultedJoin = "JoinByCodeAsync called with channel Faulted.";
        private const string LogChannelFaultedStart = "StartLobbyMatchAsync called with channel Faulted.";
        private const string ReconnectFaultDialogTitle = "Lobby";
        private const string ReconnectFaultDialogFormat = "{0}: {1}";
        private const string ReconnectFaultDialogGeneric = "Ocurrió un error durante la reconexión.";

        private bool hasShownReconnectFaultDialog;

        private const int ReconnectIntervalSeconds = 2;
        private const int ReconnectTestTimeoutSeconds = 3;
        private const int MaxReconnectAttempts = 3;

        private const string EndpointLobbyNetTcp = "NetTcpBinding_ILobbyService";
        private const string EndpointLobbyWsDualLegacy = "WSDualHttpBinding_ILobbyService";

        private const string LogEndpointRemapped = "LobbyHub endpoint remapped from {0} to {1}.";

        private const string FaultCodeDatabase = "Error de base de datos";
        private const string FaultMessageDatabaseMarker = "base de datos";

        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(ReconnectIntervalSeconds);
        private static readonly TimeSpan ReconnectTestTimeout = TimeSpan.FromSeconds(ReconnectTestTimeoutSeconds);

        private static readonly DispatcherPriority UiDispatcherPriority = DispatcherPriority.Send;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyHub));

        private readonly Dispatcher dispatcher;
        private readonly object reconnectSyncRoot = new object();

        private LobbyServiceClient client;
        private DispatcherTimer reconnectTimer;

        private bool isStoppedOrDisposed;
        private bool isReconnectInProgress;

        private readonly string endpointName;

        private string lastToken = string.Empty;
        private string lastAccessCode = string.Empty;

        public Guid? CurrentLobbyId { get; private set; }

        public string CurrentAccessCode { get; private set; } = string.Empty;

        public event Action<LobbyInfo> LobbyUpdated;
        public event Action<PlayerSummary> PlayerJoined;
        public event Action<Guid> PlayerLeft;
        public event Action<ChatMessage> ChatMessageReceived;
        public event Action<MatchInfo> MatchStarted;
        public event Action<ForcedLogoutNotification> ForcedLogout;
        public event Action ReconnectStarted;
        public event Action ReconnectStopped;
        public event Action<int> ReconnectAttempted;
        public event Action ReconnectExhausted;
        public event Action<ServiceFault> DatabaseErrorDetected;

        private int reconnectAttemptCount;
        public event Action<Exception> ChatSendFailed;

        public LobbyHub(string endpointName)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException(ErrorEndpointRequired, nameof(endpointName));
            }

            dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            this.endpointName = ResolveEndpointName(endpointName);

            RecreateClient();
        }

        public LobbyServiceClient RawClient => client;

        public async Task<CreateLobbyResponse> CreateLobbyAsync(string token, string lobbyName, byte maxPlayers = DefaultMaxPlayers)
        {
            EnsureToken(token);

            lastToken = token ?? string.Empty;

            if (isStoppedOrDisposed)
            {
                return new CreateLobbyResponse();
            }

            if (IsClientFaulted(LogChannelFaultedCreate))
            {
                StartReconnectLoop();
                return new CreateLobbyResponse();
            }

            var request = new CreateLobbyRequest
            {
                Token = token,
                LobbyName = NormalizeLobbyName(lobbyName),
                MaxPlayers = NormalizeMaxPlayers(maxPlayers)
            };

            try
            {
                CreateLobbyResponse response =
                    await Task.Run(() => client.CreateLobby(request)).ConfigureAwait(false);

                if (response != null && response.Lobby != null)
                {
                    CurrentLobbyId = response.Lobby.LobbyId;
                    CurrentAccessCode = response.Lobby.AccessCode ?? string.Empty;
                    lastAccessCode = CurrentAccessCode;
                }

                StopReconnectLoop();

                return response ?? new CreateLobbyResponse();
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "CreateLobbyAsync fault. Code={0}, Message={1}",
                    ex.Detail != null ? ex.Detail.Code : string.Empty,
                    ex.Detail != null ? ex.Detail.Message : ex.Message);

                return new CreateLobbyResponse();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CreateLobbyAsync communication error.", ex);
                OnChannelDead("CreateLobbyAsync communication error.");
                return new CreateLobbyResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("CreateLobbyAsync timeout.", ex);
                OnChannelDead("CreateLobbyAsync timeout.");
                return new CreateLobbyResponse();
            }
            catch (Exception ex)
            {
                Logger.Error("CreateLobbyAsync unexpected error.", ex);
                return new CreateLobbyResponse();
            }
        }

        public async Task<JoinByCodeResponse> JoinByCodeAsync(string token, string accessCode)
        {
            EnsureToken(token);

            lastToken = token ?? string.Empty;

            if (isStoppedOrDisposed)
            {
                return new JoinByCodeResponse();
            }

            string normalizedCode = NormalizeAccessCode(accessCode);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return new JoinByCodeResponse();
            }

            lastAccessCode = normalizedCode;

            if (IsClientFaulted(LogChannelFaultedJoin))
            {
                StartReconnectLoop();
                return new JoinByCodeResponse();
            }

            var request = new JoinByCodeRequest
            {
                Token = token,
                AccessCode = normalizedCode
            };

            return await TryJoinByCodeAsync(request, normalizedCode).ConfigureAwait(false);
        }

        private async Task<JoinByCodeResponse> TryJoinByCodeAsync(JoinByCodeRequest request, string normalizedCode)
        {
            try
            {
                JoinByCodeResponse response =
                    await Task.Run(() => client.JoinByCode(request)).ConfigureAwait(false);

                UpdateLobbyContextFromJoinResponse(response, normalizedCode);

                StopReconnectLoop();

                return response ?? new JoinByCodeResponse();
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "JoinByCodeAsync fault. Code={0}, Message={1}",
                    ex.Detail != null ? ex.Detail.Code : string.Empty,
                    ex.Detail != null ? ex.Detail.Message : ex.Message);

                return new JoinByCodeResponse();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("JoinByCodeAsync communication error.", ex);
                OnChannelDead("JoinByCodeAsync communication error.");
                return new JoinByCodeResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("JoinByCodeAsync timeout.", ex);
                OnChannelDead("JoinByCodeAsync timeout.");
                return new JoinByCodeResponse();
            }
            catch (Exception ex)
            {
                Logger.Error("JoinByCodeAsync unexpected error.", ex);
                return new JoinByCodeResponse();
            }
        }

        private void UpdateLobbyContextFromJoinResponse(JoinByCodeResponse response, string normalizedCode)
        {
            if (response == null || response.Lobby == null)
            {
                return;
            }

            CurrentLobbyId = response.Lobby.LobbyId;

            CurrentAccessCode = string.IsNullOrWhiteSpace(response.Lobby.AccessCode)
                ? normalizedCode
                : response.Lobby.AccessCode;

            lastAccessCode = CurrentAccessCode;
        }

        public async Task LeaveLobbyAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !CurrentLobbyId.HasValue)
            {
                ResetLobbyContext();
                return;
            }

            lastToken = token ?? string.Empty;

            if (isStoppedOrDisposed)
            {
                ResetLobbyContext();
                return;
            }

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    ResetLobbyContext();
                    StartReconnectLoop();
                    return;
                }

                var request = new LeaveLobbyRequest
                {
                    Token = token,
                    LobbyId = CurrentLobbyId.Value
                };

                await Task.Run(() => client.LeaveLobby(request)).ConfigureAwait(false);
            }
            catch (CommunicationObjectFaultedException ex)
            {
                Logger.Warn("CommunicationObjectFaultedException while leaving lobby.", ex);
                OnChannelDead("LeaveLobbyAsync channel faulted.");
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CommunicationException while leaving lobby.", ex);
                OnChannelDead("LeaveLobbyAsync communication error.");
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("TimeoutException while leaving lobby.", ex);
                OnChannelDead("LeaveLobbyAsync timeout.");
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while leaving lobby.", ex);
            }
            finally
            {
                ResetLobbyContext();
            }
        }

        public async Task SendMessageAsync(string token, Guid lobbyId, string message)
        {
            bool hasValidToken = !string.IsNullOrWhiteSpace(token);
            bool hasValidLobbyId = lobbyId != Guid.Empty;
            bool hasValidMessage = !string.IsNullOrWhiteSpace(message);

            if (!hasValidToken || !hasValidLobbyId || !hasValidMessage || isStoppedOrDisposed)
            {
                return;
            }

            lastToken = token ?? string.Empty;

            var request = new SendLobbyMessageRequest
            {
                Token = token,
                LobbyId = lobbyId,
                Message = message
            };

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    var ex = new CommunicationException("El cliente de lobby está en estado 'Faulted'.");
                    Logger.Warn("SendMessageAsync: channel faulted.", ex);
                    RaiseChatSendFailed(ex);
                    StartReconnectLoop();
                    throw ex;
                }

                await client.SendChatMessageAsync(request).ConfigureAwait(false);

                StopReconnectLoop();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("SendMessageAsync communication error.", ex);
                RaiseChatSendFailed(ex);
                StartReconnectLoop();
                throw;
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("SendMessageAsync timeout.", ex);
                RaiseChatSendFailed(ex);
                StartReconnectLoop();
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error("SendMessageAsync unexpected error.", ex);
                RaiseChatSendFailed(ex);
                throw;
            }
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            if (isStoppedOrDisposed || string.IsNullOrWhiteSpace(token))
            {
                return new UpdateAccountResponse();
            }

            lastToken = token ?? string.Empty;

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    StartReconnectLoop();
                    return new UpdateAccountResponse();
                }

                return client.GetMyProfile(token) ?? new UpdateAccountResponse();
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "GetMyProfile fault. Code={0}, Message={1}",
                    ex.Detail != null ? ex.Detail.Code : string.Empty,
                    ex.Detail != null ? ex.Detail.Message : ex.Message);

                return new UpdateAccountResponse();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("GetMyProfile communication error.", ex);
                OnChannelDead("GetMyProfile communication error.");
                return new UpdateAccountResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("GetMyProfile timeout.", ex);
                OnChannelDead("GetMyProfile timeout.");
                return new UpdateAccountResponse();
            }
            catch (Exception ex)
            {
                Logger.Error("GetMyProfile unexpected error.", ex);
                return new UpdateAccountResponse();
            }
        }

        public async Task<StartLobbyMatchResponse> StartLobbyMatchAsync(string token)
        {
            EnsureToken(token);

            lastToken = token ?? string.Empty;

            if (isStoppedOrDisposed)
            {
                return new StartLobbyMatchResponse();
            }

            if (client.State == CommunicationState.Faulted)
            {
                Logger.Warn(LogChannelFaultedStart);
                StartReconnectLoop();
                return new StartLobbyMatchResponse();
            }

            var request = new StartLobbyMatchRequest
            {
                Token = token
            };

            try
            {
                StartLobbyMatchResponse response =
                    await Task.Run(() => client.StartLobbyMatch(request)).ConfigureAwait(false);

                StopReconnectLoop();

                return response ?? new StartLobbyMatchResponse();
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "StartLobbyMatchAsync fault. Code={0}, Message={1}",
                    ex.Detail != null ? ex.Detail.Code : string.Empty,
                    ex.Detail != null ? ex.Detail.Message : ex.Message);

                return new StartLobbyMatchResponse();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("StartLobbyMatchAsync communication error.", ex);
                OnChannelDead("StartLobbyMatchAsync communication error.");
                return new StartLobbyMatchResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("StartLobbyMatchAsync timeout.", ex);
                OnChannelDead("StartLobbyMatchAsync timeout.");
                return new StartLobbyMatchResponse();
            }
            catch (Exception ex)
            {
                Logger.Error("StartLobbyMatchAsync unexpected error.", ex);
                return new StartLobbyMatchResponse();
            }
        }

        void ILobbyServiceCallback.OnLobbyUpdated(LobbyInfo lobby)
        {
            Ui(() =>
            {
                if (lobby != null)
                {
                    CurrentLobbyId = lobby.LobbyId;

                    if (!string.IsNullOrWhiteSpace(lobby.AccessCode))
                    {
                        CurrentAccessCode = lobby.AccessCode;
                        lastAccessCode = lobby.AccessCode ?? string.Empty;
                    }
                }

                LobbyUpdated?.Invoke(lobby);
            });
        }

        void ILobbyServiceCallback.OnPlayerJoined(PlayerSummary player)
        {
            Ui(() => PlayerJoined?.Invoke(player));
        }

        void ILobbyServiceCallback.OnPlayerLeft(Guid playerId)
        {
            Ui(() => PlayerLeft?.Invoke(playerId));
        }

        void ILobbyServiceCallback.OnChatMessageReceived(ChatMessage message)
        {
            Ui(() => ChatMessageReceived?.Invoke(message));
        }

        void ILobbyServiceCallback.OnMatchStarted(MatchInfo match)
        {
            Ui(() =>
            {
                Logger.InfoFormat(
                    "OnMatchStarted received. MatchId={0}, PlayersCount={1}",
                    match != null ? match.MatchId : Guid.Empty,
                    match != null && match.Players != null ? match.Players.Length : 0);

                MatchStarted?.Invoke(match);
            });
        }

        void ILobbyServiceCallback.ForcedLogout(ForcedLogoutNotification notification)
        {
            try
            {
                Logger.InfoFormat(
                    "ForcedLogout received. SanctionType={0}, EndAtUtc={1}, Code={2}",
                    notification != null ? notification.SanctionType : (byte)0,
                    notification != null ? (object)notification.SanctionEndAtUtc : null,
                    notification != null ? notification.Code : null);
            }
            catch (Exception ex)
            {
                Logger.Warn("Error logging ForcedLogout notification.", ex);
            }

            Ui(() => ForcedLogout?.Invoke(notification));
        }

        public void Stop()
        {
            if (isStoppedOrDisposed)
            {
                return;
            }

            isStoppedOrDisposed = true;

            StopReconnectLoop();

            LobbyServiceClient localClient = client;

            try
            {
                if (localClient == null)
                {
                    return;
                }

                if (localClient.State == CommunicationState.Faulted)
                {
                    localClient.Abort();
                    return;
                }

                localClient.Close();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CommunicationException while stopping LobbyHub. Aborting.", ex);
                SafeAbort();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("TimeoutException while stopping LobbyHub. Aborting.", ex);
                SafeAbort();
            }
            catch (Exception ex)
            {
                Logger.Warn("Unexpected error while stopping LobbyHub. Aborting.", ex);
                SafeAbort();
            }
            finally
            {
                ResetLobbyContext();
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

                dispatcher.BeginInvoke(action, UiDispatcherPriority);
            }
            catch (Exception ex)
            {
                Logger.Warn(LogUiError, ex);
            }
        }

        private void RaiseChatSendFailed(Exception ex)
        {
            try
            {
                Ui(() => ChatSendFailed?.Invoke(ex));
            }
            catch (Exception raiseEx)
            {
                Logger.Warn("LobbyHub.RaiseChatSendFailed failed.", raiseEx);
            }
        }

        private void SafeAbort()
        {
            try
            {
                LobbyServiceClient localClient = client;
                if (localClient == null)
                {
                    return;
                }

                localClient.Abort();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error while aborting LobbyHub client.", ex);
            }
        }

        private void RecreateClient()
        {
            SafeAbort();

            var instanceContext = new InstanceContext(this);
            client = new LobbyServiceClient(instanceContext, endpointName);

            AttachChannelEvents();
        }

        private void AttachChannelEvents()
        {
            try
            {
                ICommunicationObject channel = client.InnerChannel;

                channel.Faulted += (_, __) => OnChannelDead("LobbyHub channel faulted.");
                channel.Closed += (_, __) => OnChannelDead("LobbyHub channel closed.");
            }
            catch (Exception ex)
            {
                Logger.Warn("AttachChannelEvents failed.", ex);
            }
        }

        private void OnChannelDead(string reason)
        {
            if (isStoppedOrDisposed)
            {
                return;
            }

            var ex = new CommunicationException(reason);
            Logger.Warn("LobbyHub channel dead.", ex);

            RaiseChatSendFailed(ex);

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
                dispatcher.BeginInvoke(new Action(StartReconnectLoop), UiDispatcherPriority);
                return;
            }

            lock (reconnectSyncRoot)
            {
                if (reconnectTimer == null)
                {
                    reconnectTimer = new DispatcherTimer(UiDispatcherPriority, dispatcher)
                    {
                        Interval = ReconnectInterval
                    };

                    reconnectTimer.Tick += ReconnectTimerTick;
                }

                if (!reconnectTimer.IsEnabled)
                {
                    reconnectAttemptCount = 0;
                    hasShownReconnectFaultDialog = false;
                    reconnectTimer.Start();
                    RaiseReconnectStarted();
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
                dispatcher.BeginInvoke(new Action(StopReconnectLoop), UiDispatcherPriority);
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

            int attempt;

            lock (reconnectSyncRoot)
            {
                if (isReconnectInProgress)
                {
                    return;
                }

                reconnectAttemptCount++;
                attempt = reconnectAttemptCount;

                if (attempt > MaxReconnectAttempts)
                {
                    StopReconnectLoop();
                    RaiseReconnectExhausted();
                    return;
                }

                isReconnectInProgress = true;
            }

            RaiseReconnectAttempted(attempt);

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

            if (string.IsNullOrWhiteSpace(lastToken))
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
                    client.InnerChannel.OperationTimeout = ReconnectTestTimeout;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Setting OperationTimeout failed.", ex);
                }

                try
                {
                    _ = await Task.Run(() => client.GetMyProfile(lastToken)).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(lastAccessCode))
                    {
                        var request = new JoinByCodeRequest
                        {
                            Token = lastToken,
                            AccessCode = lastAccessCode
                        };

                        JoinByCodeResponse response =
                            await Task.Run(() => client.JoinByCode(request)).ConfigureAwait(false);

                        UpdateLobbyContextFromJoinResponse(response, lastAccessCode);
                    }
                }
                // REEMPLAZA tu catch (FaultException<ServiceFault> ex) dentro de TryReconnectAsync()
                // (el que está justo debajo del GetMyProfile / JoinByCode del reconnect)

                catch (FaultException<ServiceFault> ex)
                {
                    string faultCode = ex.Detail != null ? (ex.Detail.Code ?? string.Empty) : string.Empty;
                    string faultMessage = ex.Detail != null ? (ex.Detail.Message ?? string.Empty) : (ex.Message ?? string.Empty);

                    Logger.WarnFormat(
                        "LobbyHub reconnect fault. Code={0}, Message={1}",
                        faultCode,
                        faultMessage);

                    bool isDatabaseFault =
                        string.Equals(faultCode.Trim(), FaultCodeDatabase, StringComparison.OrdinalIgnoreCase) ||
                        faultMessage.IndexOf(FaultMessageDatabaseMarker, StringComparison.OrdinalIgnoreCase) >= 0;

                    StopReconnectLoop();

                    if (isDatabaseFault)
                    {
                        RaiseReconnectStopped();
                        RaiseDatabaseErrorDetected(ex.Detail);
                        return;
                    }

                    bool mustShow;

                    lock (reconnectSyncRoot)
                    {
                        mustShow = !hasShownReconnectFaultDialog;
                        hasShownReconnectFaultDialog = true;
                    }

                    RaiseReconnectStopped();

                    if (mustShow)
                    {
                        ShowReconnectFaultDialog(faultCode, faultMessage);
                    }

                    return;
                }


                Logger.Info("LobbyHub reconnected successfully.");
                RaiseReconnectStopped(); 
                StopReconnectLoop();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("LobbyHub reconnect attempt communication error.", ex);
                SafeAbort();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("LobbyHub reconnect attempt timeout.", ex);
                SafeAbort();
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyHub reconnect attempt failed.", ex);
                SafeAbort();
            }
        }
        

        private void ResetLobbyContext()
        {
            CurrentLobbyId = null;
            CurrentAccessCode = string.Empty;
            lastAccessCode = string.Empty;
        }

        private static string NormalizeLobbyName(string lobbyName)
        {
            return string.IsNullOrWhiteSpace(lobbyName)
                ? string.Empty
                : lobbyName.Trim();
        }

        private static byte NormalizeMaxPlayers(byte requestedMaxPlayers)
        {
            return requestedMaxPlayers < MinAllowedMaxPlayers
                ? DefaultMaxPlayers
                : requestedMaxPlayers;
        }

        private static string NormalizeAccessCode(string accessCode)
        {
            return (accessCode ?? string.Empty).Trim().ToUpperInvariant();
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

        private static void EnsureToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException(ErrorTokenRequired, nameof(token));
            }
        }

        private static string ResolveEndpointName(string requestedEndpointName)
        {
            string safeName = (requestedEndpointName ?? string.Empty).Trim();

            if (string.Equals(safeName, EndpointLobbyWsDualLegacy, StringComparison.Ordinal))
            {
                Logger.WarnFormat(LogEndpointRemapped, safeName, EndpointLobbyNetTcp);
                return EndpointLobbyNetTcp;
            }

            return safeName;
        }

        private bool CanUseDispatcher()
        {
            return !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished;
        }

        private void RaiseReconnectStarted()
        {
            try
            {
                Ui(() => ReconnectStarted?.Invoke());
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyHub.RaiseReconnectStarted failed.", ex);
            }
        }

        private void RaiseReconnectStopped()
        {
            try
            {
                Ui(() => ReconnectStopped?.Invoke());
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyHub.RaiseReconnectStopped failed.", ex);
            }
        }

        private void RaiseReconnectAttempted(int attempt)
        {
            try
            {
                Ui(() => ReconnectAttempted?.Invoke(attempt));
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyHub.RaiseReconnectAttempted failed.", ex);
            }
        }

        private void RaiseReconnectExhausted()
        {
            try
            {
                Ui(() => ReconnectExhausted?.Invoke());
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyHub.RaiseReconnectExhausted failed.", ex);
            }
        }

        public void ContinueReconnectCycle()
        {
            if (isStoppedOrDisposed)
            {
                return;
            }

            lock (reconnectSyncRoot)
            {
                reconnectAttemptCount = 0;
            }

            StartReconnectLoop();
        }

        private static bool IsDatabaseFault(ServiceFault fault)
        {
            if (fault == null)
            {
                return false;
            }

            string code = (fault.Code ?? string.Empty).Trim();
            string message = (fault.Message ?? string.Empty).Trim();

            if (string.Equals(code, FaultCodeDatabase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return message.IndexOf(FaultMessageDatabaseMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RaiseDatabaseErrorDetected(ServiceFault fault)
        {
            try
            {
                Ui(() => DatabaseErrorDetected?.Invoke(fault));
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyHub.RaiseDatabaseErrorDetected failed.", ex);
            }

        }

        private void ShowReconnectFaultDialog(string faultCode, string faultMessage)
        {
            string safeCode = (faultCode ?? string.Empty).Trim();
            string safeMessage = (faultMessage ?? string.Empty).Trim();

            string text =
                string.IsNullOrWhiteSpace(safeCode) && string.IsNullOrWhiteSpace(safeMessage)
                    ? ReconnectFaultDialogGeneric
                    : string.IsNullOrWhiteSpace(safeCode)
                        ? safeMessage
                        : string.IsNullOrWhiteSpace(safeMessage)
                            ? safeCode
                            : string.Format(CultureInfo.InvariantCulture, ReconnectFaultDialogFormat, safeCode, safeMessage);

            Ui(() =>
            {
                try
                {
                    MessageBox.Show(
                        text,
                        ReconnectFaultDialogTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    Logger.Warn("ShowReconnectFaultDialog failed.", ex);
                }
            });
        }
    }
}
