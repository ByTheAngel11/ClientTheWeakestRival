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
    public sealed class LobbyHub : ILobbyServiceCallback, IDisposable
    {
        private const byte DEFAULT_MAX_PLAYERS = 8;
        private const byte MIN_ALLOWED_MAX_PLAYERS = 1;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyHub));

        private readonly LobbyServiceClient client;
        private readonly Dispatcher dispatcher;

        public Guid? CurrentLobbyId { get; private set; }
        public string CurrentAccessCode { get; private set; }

        public event Action<LobbyInfo> LobbyUpdated;
        public event Action<PlayerSummary> PlayerJoined;
        public event Action<Guid> PlayerLeft;
        public event Action<ChatMessage> ChatMessageReceived;
        public event Action<MatchInfo> MatchStarted;

        public LobbyHub(string endpointName)
        {
            dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            var instanceContext = new InstanceContext(this);
            client = new LobbyServiceClient(instanceContext, endpointName);
        }

        public LobbyServiceClient RawClient => client;

        private void Ui(Action action)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                dispatcher.BeginInvoke(action, DispatcherPriority.Send);
            }
            catch (Exception ex)
            {
                Logger.Warn("LobbyHub.Ui error.", ex);
            }
        }

        public async Task<CreateLobbyResponse> CreateLobbyAsync(string token, string lobbyName, byte maxPlayers = DEFAULT_MAX_PLAYERS)
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
                var response = await Task.Run(() => client.CreateLobby(request)).ConfigureAwait(false);

                if (response != null && response.Lobby != null)
                {
                    CurrentLobbyId = response.Lobby.LobbyId;
                    CurrentAccessCode = response.Lobby.AccessCode;
                }

                return response;
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "CreateLobbyAsync fault. Code={0}, Message={1}",
                    ex.Detail != null ? ex.Detail.Code : string.Empty,
                    ex.Detail != null ? ex.Detail.Message : ex.Message);

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
                var response = await Task.Run(() => client.JoinByCode(request)).ConfigureAwait(false);

                if (response != null && response.Lobby != null)
                {
                    CurrentLobbyId = response.Lobby.LobbyId;
                    CurrentAccessCode = string.IsNullOrWhiteSpace(response.Lobby.AccessCode)
                        ? normalizedCode
                        : response.Lobby.AccessCode;
                }

                return response;
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "JoinByCodeAsync fault. Code={0}, Message={1}",
                    ex.Detail != null ? ex.Detail.Code : string.Empty,
                    ex.Detail != null ? ex.Detail.Message : ex.Message);

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
                if (client.State == CommunicationState.Faulted)
                {
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

            return Task.Run(() => client.SendChatMessage(request));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            return client.GetMyProfile(token);
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
                return await Task.Run(() => client.StartLobbyMatch(request)).ConfigureAwait(false);
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.WarnFormat(
                    "StartLobbyMatchAsync fault. Code={0}, Message={1}",
                    ex.Detail != null ? ex.Detail.Code : string.Empty,
                    ex.Detail != null ? ex.Detail.Message : ex.Message);

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
        }

        void ILobbyServiceCallback.OnLobbyUpdated(LobbyInfo lobby)
        {
            Ui(() =>
            {
                try
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
                catch (Exception ex)
                {
                    Logger.Warn("OnLobbyUpdated handler error.", ex);
                }
            });
        }

        void ILobbyServiceCallback.OnPlayerJoined(PlayerSummary player)
        {
            Ui(() =>
            {
                try
                {
                    PlayerJoined?.Invoke(player);
                }
                catch (Exception ex)
                {
                    Logger.Warn("OnPlayerJoined handler error.", ex);
                }
            });
        }

        void ILobbyServiceCallback.OnPlayerLeft(Guid playerId)
        {
            Ui(() =>
            {
                try
                {
                    PlayerLeft?.Invoke(playerId);
                }
                catch (Exception ex)
                {
                    Logger.Warn("OnPlayerLeft handler error.", ex);
                }
            });
        }

        void ILobbyServiceCallback.OnChatMessageReceived(ChatMessage message)
        {
            Ui(() =>
            {
                try
                {
                    ChatMessageReceived?.Invoke(message);
                }
                catch (Exception ex)
                {
                    Logger.Warn("OnChatMessageReceived handler error.", ex);
                }
            });
        }

        void ILobbyServiceCallback.OnMatchStarted(MatchInfo match)
        {
            Ui(() =>
            {
                try
                {
                    Logger.InfoFormat(
                        "OnMatchStarted received. MatchId={0}, PlayersCount={1}",
                        match != null ? match.MatchId : Guid.Empty,
                        match != null && match.Players != null ? match.Players.Length : 0);

                    MatchStarted?.Invoke(match);
                }
                catch (Exception ex)
                {
                    Logger.Warn("OnMatchStarted handler error.", ex);
                }
            });
        }

        public void Dispose()
        {
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
                client.Abort();
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
    }
}
