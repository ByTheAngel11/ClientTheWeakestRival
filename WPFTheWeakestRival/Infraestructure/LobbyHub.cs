using System;
using System.ServiceModel;
using System.Threading.Tasks;
using log4net;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure
{
    [CallbackBehavior(UseSynchronizationContext = true, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public sealed class LobbyHub : ILobbyServiceCallback, IDisposable, IStoppable
    {
        private const byte DEFAULT_MAX_PLAYERS = 8;
        private const byte MIN_ALLOWED_MAX_PLAYERS = 1;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyHub));

        private readonly LobbyServiceClient _client;

        public Guid? CurrentLobbyId { get; private set; }
        public string CurrentAccessCode { get; private set; }

        public event Action<LobbyInfo> LobbyUpdated;
        public event Action<PlayerSummary> PlayerJoined;
        public event Action<Guid> PlayerLeft;
        public event Action<ChatMessage> ChatMessageReceived;
        public event Action<MatchInfo> MatchStarted;
        public event Action<ForcedLogoutNotification> ForcedLogout;

        public LobbyHub(string endpointName)
        {
            var instanceContext = new InstanceContext(this);
            _client = new LobbyServiceClient(instanceContext, endpointName);
        }

        public LobbyServiceClient RawClient => _client;

        public async Task<CreateLobbyResponse> CreateLobbyAsync(string token,string lobbyName,byte maxPlayers = DEFAULT_MAX_PLAYERS)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token requerido.", nameof(token));
            }

            if (client.State == CommunicationState.Faulted)
            {
                Logger.Warn("CreateLobbyAsync llamado con el canal en estado Faulted.");
                return null;
            }

            var request = new CreateLobbyRequest
            {
                Token = token,
                LobbyName = NormalizeLobbyName(lobbyName),
                MaxPlayers = NormalizeMaxPlayers(maxPlayers)
            };

            try
            {
                var response = await Task
                    .Run(() => client.CreateLobby(request))
                    .ConfigureAwait(false);

                if (response?.Lobby != null)
                {
                    CurrentLobbyId = response.Lobby.LobbyId;
                    CurrentAccessCode = response.Lobby.AccessCode;
                }
            var response = await Task.Run(() => _client.CreateLobby(request)).ConfigureAwait(false);
            if (response?.Lobby != null)
            {
                CurrentLobbyId = response.Lobby.LobbyId;
                CurrentAccessCode = response.Lobby.AccessCode;
            }

                return response;
            }
            catch (FaultException<ServiceFault> fault)
            {
                Logger.WarnFormat(
                    "CreateLobbyAsync fault. Code={0}, Message={1}",
                    fault.Detail.Code,
                    fault.Detail.Message);
                return null;
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CreateLobbyAsync communication error.", ex);
                return null;
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("CreateLobbyAsync timeout.", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("CreateLobbyAsync unexpected error.", ex);
                return null;
            }
        }


        public async Task<JoinByCodeResponse> JoinByCodeAsync(string token, string accessCode)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token requerido.", nameof(token));
            }

            var normalizedCode = NormalizeAccessCode(accessCode);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                throw new ArgumentException("Código de acceso requerido.", nameof(accessCode));
            }

            if (client.State == CommunicationState.Faulted)
            {
                Logger.Warn("JoinByCodeAsync llamado con el canal en estado Faulted.");
                return null;
            }

            var request = new JoinByCodeRequest
            {
                Token = token,
                AccessCode = normalizedCode
            };

            try
            {
                var response = await Task
                    .Run(() => client.JoinByCode(request))
                    .ConfigureAwait(false);

                if (response?.Lobby != null)
                {
                    CurrentLobbyId = response.Lobby.LobbyId;
                    CurrentAccessCode = string.IsNullOrWhiteSpace(response.Lobby.AccessCode)
                        ? normalizedCode
                        : response.Lobby.AccessCode;
                }
            var response = await Task.Run(() => _client.JoinByCode(request)).ConfigureAwait(false);
            if (response?.Lobby != null)
            {
                CurrentLobbyId = response.Lobby.LobbyId;
                CurrentAccessCode = string.IsNullOrWhiteSpace(response.Lobby.AccessCode)
                    ? normalizedCode
                    : response.Lobby.AccessCode;
            }

                return response;
            }
            catch (FaultException<ServiceFault> fault)
            {
                Logger.WarnFormat(
                    "JoinByCodeAsync fault. Code={0}, Message={1}",
                    fault.Detail.Code,
                    fault.Detail.Message);
                return null;
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("JoinByCodeAsync communication error.", ex);
                return null;
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("JoinByCodeAsync timeout.", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("JoinByCodeAsync unexpected error.", ex);
                return null;
            }
        }


        public async Task LeaveLobbyAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !CurrentLobbyId.HasValue)
            {
                return;
            }

            try
            {
                if (_client.State == CommunicationState.Faulted)
                {
                    return;
                }

                var request = new LeaveLobbyRequest
                {
                    Token = token,
                    LobbyId = CurrentLobbyId.Value
                };

                await Task.Run(() => _client.LeaveLobby(request)).ConfigureAwait(false);
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
                CurrentLobbyId = null;
                CurrentAccessCode = null;
            }
        }

        public Task SendMessageAsync(string token, Guid lobbyId, string message)
        {
            var hasValidToken = !string.IsNullOrWhiteSpace(token);
            var hasValidLobbyId = lobbyId != Guid.Empty;
            var hasValidMessage = !string.IsNullOrWhiteSpace(message);

            if (!hasValidToken || !hasValidLobbyId || !hasValidMessage)
            {
                return Task.CompletedTask;
            }

            var request = new SendLobbyMessageRequest
            {
                Token = token,
                LobbyId = lobbyId,
                Message = message
            };

            return Task.Run(() => _client.SendChatMessage(request));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            return _client.GetMyProfile(token);
        }

        public async Task<StartLobbyMatchResponse> StartLobbyMatchAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token requerido.", nameof(token));
            }

            if (client.State == CommunicationState.Faulted)
            {
                Logger.Warn("StartLobbyMatchAsync llamado con el canal en estado Faulted.");
                return null;
            }

            var request = new StartLobbyMatchRequest
            {
                Token = token
            };

            try
            {
                var response = await Task
                    .Run(() => client.StartLobbyMatch(request))
                    .ConfigureAwait(false);

                return response;
            }
            catch (FaultException<ServiceFault> fault)
            {
                Logger.WarnFormat(
                    "StartLobbyMatchAsync fault. Code={0}, Message={1}",
                    fault.Detail.Code,
                    fault.Detail.Message);
                return null;
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("StartLobbyMatchAsync communication error.", ex);
                return null;
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("StartLobbyMatchAsync timeout.", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("StartLobbyMatchAsync unexpected error.", ex);
                return null;
            }
            return Task.Run(() => _client.StartLobbyMatch(request));
        }


        void ILobbyServiceCallback.OnLobbyUpdated(LobbyInfo lobby)
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
        }

        void ILobbyServiceCallback.OnPlayerJoined(PlayerSummary player)
        {
            PlayerJoined?.Invoke(player);
        }

        void ILobbyServiceCallback.OnPlayerLeft(Guid playerId)
        {
            PlayerLeft?.Invoke(playerId);
        }

        void ILobbyServiceCallback.OnChatMessageReceived(ChatMessage message)
        {
            ChatMessageReceived?.Invoke(message);
        }

        void ILobbyServiceCallback.OnMatchStarted(MatchInfo match)
        {
            MatchStarted?.Invoke(match);
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

            ForcedLogout?.Invoke(notification);
        }

        public void Dispose()
        {
            try
            {
                if (_client.State == CommunicationState.Faulted)
                {
                    _client.Abort();
                }
                else
                {
                    _client.Close();
                }
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CommunicationException while disposing LobbyHub client. Aborting.", ex);
                SafeAbort();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("TimeoutException while disposing LobbyHub client. Aborting.", ex);
                SafeAbort();
            }
            catch (Exception ex)
            {
                Logger.Warn("Unexpected error while disposing LobbyHub client. Aborting.", ex);
                SafeAbort();
            }
        }

        private void SafeAbort()
        {
            try
            {
                _client.Abort();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error while aborting LobbyHub client.", ex);
            }
        }

        private static string NormalizeLobbyName(string lobbyName)
        {
            if (string.IsNullOrWhiteSpace(lobbyName))
            {
                return null;
            }

            return lobbyName.Trim();
        }

        private static byte NormalizeMaxPlayers(byte requestedMaxPlayers)
        {
            if (requestedMaxPlayers < MIN_ALLOWED_MAX_PLAYERS)
            {
                return DEFAULT_MAX_PLAYERS;
            }

            return requestedMaxPlayers;
        }

        private static string NormalizeAccessCode(string accessCode)
        {
            return (accessCode ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        public void Stop()
        {
            try
            {
                SafeAbort();
            }
            finally
            {
                CurrentLobbyId = null;
                CurrentAccessCode = null;
            }
        }

    }
}
