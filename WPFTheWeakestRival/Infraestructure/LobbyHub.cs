using System;
using System.ServiceModel;
using System.Threading.Tasks;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure
{
    [CallbackBehavior(UseSynchronizationContext = true, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public sealed class LobbyHub : ILobbyServiceCallback, IDisposable
    {
        private readonly LobbyServiceClient client;

        public Guid? CurrentLobbyId { get; private set; }
        public string CurrentAccessCode { get; private set; }

        public event Action<LobbyInfo> LobbyUpdated;
        public event Action<PlayerSummary> PlayerJoined;
        public event Action<Guid> PlayerLeft;
        public event Action<ChatMessage> ChatMessageReceived;

        // ⬇⬇ NUEVO: evento para cuando el servidor arranca la partida
        public event Action<MatchInfo> MatchStarted;

        public LobbyHub(string endpointName)
        {
            var ctx = new InstanceContext(this);
            client = new LobbyServiceClient(ctx, endpointName);
        }

        public LobbyServiceClient RawClient => client;

        public async Task<CreateLobbyResponse> CreateLobbyAsync(string token, string lobbyName, byte maxPlayers = 8)
        {
            var req = new CreateLobbyRequest
            {
                Token = token,
                LobbyName = string.IsNullOrWhiteSpace(lobbyName) ? null : lobbyName.Trim(),
                MaxPlayers = maxPlayers > 0 ? maxPlayers : (byte)8
            };

            var res = await Task.Run(() => client.CreateLobby(req));
            if (res?.Lobby != null)
            {
                CurrentLobbyId = res.Lobby.LobbyId;
                CurrentAccessCode = res.Lobby.AccessCode;
            }
            return res;
        }

        public async Task<JoinByCodeResponse> JoinByCodeAsync(string token, string accessCode)
        {
            var req = new JoinByCodeRequest
            {
                Token = token,
                AccessCode = (accessCode ?? string.Empty).Trim().ToUpperInvariant()
            };

            var res = await Task.Run(() => client.JoinByCode(req));
            if (res?.Lobby != null)
            {
                CurrentLobbyId = res.Lobby.LobbyId;
                CurrentAccessCode = string.IsNullOrWhiteSpace(res.Lobby.AccessCode) ? req.AccessCode : res.Lobby.AccessCode;
            }
            return res;
        }

        public async Task LeaveLobbyAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !CurrentLobbyId.HasValue)
            {
                return;
            }

            try
            {
                // Si el canal ya está en Faulted, no intentes llamar al servicio.
                if (client.State == CommunicationState.Faulted)
                {
                    return;
                }

                await Task.Run(() => client.LeaveLobby(new LeaveLobbyRequest
                {
                    Token = token,
                    LobbyId = CurrentLobbyId.Value
                }));
            }
            catch (CommunicationObjectFaultedException)
            {
                // El canal ya estaba roto, ignoramos de forma controlada.
            }
            catch (CommunicationException)
            {
                // Puedes loguear si quieres, pero no reventamos la app.
            }
            catch (TimeoutException)
            {
                // Igual, opcional log.
            }
            finally
            {
                // Siempre limpiamos el estado local del hub.
                CurrentLobbyId = null;
                CurrentAccessCode = null;
            }
        }


        public Task SendMessageAsync(string token, Guid lobbyId, string message)
        {
            if (string.IsNullOrWhiteSpace(token) || lobbyId == Guid.Empty || string.IsNullOrWhiteSpace(message))
            {
                return Task.CompletedTask;
            }

            var req = new SendLobbyMessageRequest
            {
                Token = token,
                LobbyId = lobbyId,
                Message = message
            };

            return Task.Run(() => client.SendChatMessage(req));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            return client.GetMyProfile(token);
        }

        // ⬇⬇ NUEVO: helper para llamar StartLobbyMatch desde el cliente
        public Task<StartLobbyMatchResponse> StartLobbyMatchAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token requerido.", nameof(token));
            }

            var req = new StartLobbyMatchRequest
            {
                Token = token
            };

            return Task.Run(() => client.StartLobbyMatch(req));
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

        // ⬇⬇ NUEVO: implementación del callback de partida iniciada
        void ILobbyServiceCallback.OnMatchStarted(MatchInfo match)
        {
            MatchStarted?.Invoke(match);
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
            catch
            {
                try
                {
                    client.Abort();
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }
}
