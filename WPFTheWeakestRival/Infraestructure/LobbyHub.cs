using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using log4net;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure
{
    [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public sealed class LobbyHub : ILobbyServiceCallback, IDisposable, IStoppable
    {
        private const byte DEFAULT_MAX_PLAYERS = 8;
        private const byte MIN_ALLOWED_MAX_PLAYERS = 1;

        private const string ERROR_TOKEN_REQUIRED = "Token requerido.";
        private const string ERROR_ENDPOINT_REQUIRED = "Endpoint name cannot be null or whitespace.";

        private const string LOG_UI_ERROR = "LobbyHub.Ui error.";
        private const string LOG_CHANNEL_FAULTED_CREATE = "CreateLobbyAsync called with channel Faulted.";
        private const string LOG_CHANNEL_FAULTED_JOIN = "JoinByCodeAsync called with channel Faulted.";
        private const string LOG_CHANNEL_FAULTED_START = "StartLobbyMatchAsync called with channel Faulted.";

        private static readonly DispatcherPriority UiDispatcherPriority = DispatcherPriority.Send;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyHub));

        private readonly LobbyServiceClient client;
        private readonly Dispatcher dispatcher;

        private bool isStoppedOrDisposed;

        public Guid? CurrentLobbyId { get; private set; }

        public string CurrentAccessCode { get; private set; } = string.Empty;

        public event Action<LobbyInfo> LobbyUpdated;
        public event Action<PlayerSummary> PlayerJoined;
        public event Action<Guid> PlayerLeft;
        public event Action<ChatMessage> ChatMessageReceived;
        public event Action<MatchInfo> MatchStarted;
        public event Action<ForcedLogoutNotification> ForcedLogout;

        public LobbyHub(string endpointName)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException(ERROR_ENDPOINT_REQUIRED, nameof(endpointName));
            }

            dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            var instanceContext = new InstanceContext(this);
            client = new LobbyServiceClient(instanceContext, endpointName);
        }

        public LobbyServiceClient RawClient => client;

        public async Task<CreateLobbyResponse> CreateLobbyAsync(
            string token,
            string lobbyName,
            byte maxPlayers = DEFAULT_MAX_PLAYERS)
        {
            EnsureToken(token);

            if (isStoppedOrDisposed)
            {
                return new CreateLobbyResponse();
            }

            if (client.State == CommunicationState.Faulted)
            {
                Logger.Warn(LOG_CHANNEL_FAULTED_CREATE);
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
                }

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
                return new CreateLobbyResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("CreateLobbyAsync timeout.", ex);
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

            if (isStoppedOrDisposed)
            {
                return new JoinByCodeResponse();
            }

            string normalizedCode = NormalizeAccessCode(accessCode);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return new JoinByCodeResponse();
            }

            if (IsClientFaulted(LOG_CHANNEL_FAULTED_JOIN))
            {
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
                return new JoinByCodeResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("JoinByCodeAsync timeout.", ex);
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
        }

        public async Task LeaveLobbyAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !CurrentLobbyId.HasValue)
            {
                ResetLobbyContext();
                return;
            }

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
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CommunicationException while leaving lobby.", ex);
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("TimeoutException while leaving lobby.", ex);
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

        public Task SendMessageAsync(string token, Guid lobbyId, string message)
        {
            bool hasValidToken = !string.IsNullOrWhiteSpace(token);
            bool hasValidLobbyId = lobbyId != Guid.Empty;
            bool hasValidMessage = !string.IsNullOrWhiteSpace(message);

            if (!hasValidToken || !hasValidLobbyId || !hasValidMessage || isStoppedOrDisposed)
            {
                return Task.CompletedTask;
            }

            var request = new SendLobbyMessageRequest
            {
                Token = token,
                LobbyId = lobbyId,
                Message = message
            };

            return Task.Run(() => SendChatMessageInternal(request));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            if (isStoppedOrDisposed || string.IsNullOrWhiteSpace(token))
            {
                return new UpdateAccountResponse();
            }

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
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
                return new UpdateAccountResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("GetMyProfile timeout.", ex);
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

            if (isStoppedOrDisposed)
            {
                return new StartLobbyMatchResponse();
            }

            if (client.State == CommunicationState.Faulted)
            {
                Logger.Warn(LOG_CHANNEL_FAULTED_START);
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
                return new StartLobbyMatchResponse();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("StartLobbyMatchAsync timeout.", ex);
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

            try
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
                Logger.Warn(LOG_UI_ERROR, ex);
            }
        }

        private void SafeAbort()
        {
            try
            {
                client.Abort();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error while aborting LobbyHub client.", ex);
            }
        }

        private void SendChatMessageInternal(SendLobbyMessageRequest request)
        {
            if (request == null || isStoppedOrDisposed)
            {
                return;
            }

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    return;
                }

                client.SendChatMessage(request);
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("SendChatMessage communication error.", ex);
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("SendChatMessage timeout.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("SendChatMessage unexpected error.", ex);
            }
        }

        private void ResetLobbyContext()
        {
            CurrentLobbyId = null;
            CurrentAccessCode = string.Empty;
        }

        private static string NormalizeLobbyName(string lobbyName)
        {
            return string.IsNullOrWhiteSpace(lobbyName)
                ? string.Empty
                : lobbyName.Trim();
        }

        private static byte NormalizeMaxPlayers(byte requestedMaxPlayers)
        {
            return requestedMaxPlayers < MIN_ALLOWED_MAX_PLAYERS
                ? DEFAULT_MAX_PLAYERS
                : requestedMaxPlayers;
        }

        private static string NormalizeAccessCode(string accessCode)
        {
            return (accessCode ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        private bool IsClientFaulted(string logMessage)
        {
            if (client.State != CommunicationState.Faulted)
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
                throw new ArgumentException(ERROR_TOKEN_REQUIRED, nameof(token));
            }
        }
    }
}
