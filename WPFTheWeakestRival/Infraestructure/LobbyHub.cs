using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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

        private readonly LobbyServiceClient client;
        private bool isStoppedOrDisposed;

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
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException("Endpoint name cannot be null or whitespace.", nameof(endpointName));
            }

            var instanceContext = new InstanceContext(this);
            client = new LobbyServiceClient(instanceContext, endpointName);
        }

        public LobbyServiceClient RawClient => client;

        public async Task<CreateLobbyResponse> CreateLobbyAsync(
            string token,
            string lobbyName,
            byte maxPlayers = DEFAULT_MAX_PLAYERS)
        {
            var request = new CreateLobbyRequest
            {
                Token = token,
                LobbyName = NormalizeLobbyName(lobbyName),
                MaxPlayers = NormalizeMaxPlayers(maxPlayers)
            };

            CreateLobbyResponse createResponse =
                await Task.Run(() => client.CreateLobby(request)).ConfigureAwait(false);

            if (createResponse?.Lobby != null)
            {
                CurrentLobbyId = createResponse.Lobby.LobbyId;

                if (!string.IsNullOrWhiteSpace(createResponse.Lobby.AccessCode))
                {
                    CurrentAccessCode = createResponse.Lobby.AccessCode;
                }
            }

            return createResponse;
        }

        public async Task<JoinByCodeResponse> JoinByCodeAsync(string token, string accessCode)
        {
            string normalizedCode = NormalizeAccessCode(accessCode);

            var request = new JoinByCodeRequest
            {
                Token = token,
                AccessCode = normalizedCode
            };

            JoinByCodeResponse joinResponse =
                await Task.Run(() => client.JoinByCode(request)).ConfigureAwait(false);

            if (joinResponse?.Lobby != null)
            {
                CurrentLobbyId = joinResponse.Lobby.LobbyId;
                CurrentAccessCode = string.IsNullOrWhiteSpace(joinResponse.Lobby.AccessCode)
                    ? normalizedCode
                    : joinResponse.Lobby.AccessCode;
            }

            return joinResponse;
        }

        public async Task LeaveLobbyAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !CurrentLobbyId.HasValue)
            {
                CurrentLobbyId = null;
                CurrentAccessCode = null;
                return;
            }

            if (isStoppedOrDisposed)
            {
                CurrentLobbyId = null;
                CurrentAccessCode = null;
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

            return Task.Run(() => client.SendChatMessage(request));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            if (isStoppedOrDisposed)
            {
                return new UpdateAccountResponse();
            }

            return client.GetMyProfile(token);
        }

        public async Task<StartLobbyMatchResponse> StartLobbyMatchAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token requerido.", nameof(token));
            }

            if (isStoppedOrDisposed)
            {
                return new StartLobbyMatchResponse();
            }

            var request = new StartLobbyMatchRequest
            {
                Token = token
            };

            StartLobbyMatchResponse startResponse =
                await Task.Run(() => client.StartLobbyMatch(request)).ConfigureAwait(false);

            return startResponse;
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
                CurrentLobbyId = null;
                CurrentAccessCode = null;
            }
        }

        public void Dispose()
        {
            Stop();
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
