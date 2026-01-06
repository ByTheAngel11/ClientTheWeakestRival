using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;

namespace WPFTheWeakestRival.Infrastructure
{
    public sealed class FriendManager : IDisposable, IStoppable
    {
        private const int HEARTBEAT_SECONDS = 30;
        private const int REFRESH_SECONDS = 45;
        private const int DEFAULT_AVATAR_SIZE = 36;
        private const string DEVICE_NAME = "WPF";

        private readonly string endpointName;
        private readonly DispatcherTimer heartbeatTimer;
        private readonly DispatcherTimer refreshTimer;

        private FriendServiceClient client;
        private bool isDisposed;

        public event Action<IReadOnlyList<Models.FriendItem>, int> FriendsUpdated;

        public FriendManager(string endpointName)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException("Endpoint name cannot be null or whitespace.", nameof(endpointName));
            }

            this.endpointName = endpointName;
            client = CreateClient();

            heartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(HEARTBEAT_SECONDS)
            };

            heartbeatTimer.Tick += async (_, __) => await SendHeartbeatSafeAsync();

            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(REFRESH_SECONDS)
            };

            refreshTimer.Tick += async (_, __) => await RefreshFriendsSafeAsync();
        }

        public void Start()
        {
            if (isDisposed)
            {
                return;
            }

            heartbeatTimer.Start();
            refreshTimer.Start();

            _ = RefreshFriendsSafeAsync();
        }

        public void Stop()
        {
            if (isDisposed)
            {
                return;
            }

            heartbeatTimer.Stop();
            refreshTimer.Stop();

            CloseClientSafe();
        }

        private void CloseClientSafe()
        {
            var local = client;
            client = null;

            if (local == null)
            {
                return;
            }

            try
            {
                if (local.State == CommunicationState.Faulted)
                {
                    local.Abort();
                }
                else
                {
                    local.Close();
                }
            }
            catch
            {
                try { local.Abort(); } catch { }
            }
        }


        public Task ManualRefreshAsync()
        {
            return RefreshFriendsSafeAsync();
        }

        private FriendServiceClient CreateClient()
        {
            return new FriendServiceClient(endpointName);
        }

        private FriendServiceClient GetOrCreateClient()
        {
            if (isDisposed)
            {
                return null;
            }

            if (client == null ||
                client.State == CommunicationState.Closed ||
                client.State == CommunicationState.Faulted)
            {
                try
                {
                    client?.Abort();
                }
                catch
                {
                    // Ignore
                }

                client = CreateClient();
            }

            return client;
        }

        private async Task SendHeartbeatSafeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                var proxy = GetOrCreateClient();
                if (proxy == null)
                {
                    return;
                }

                await proxy.PresenceHeartbeatAsync(new HeartbeatRequest
                {
                    Token = token,
                    Device = DEVICE_NAME
                });
            }
            catch
            {
                // Ignore: heartbeat es best-effort
            }
        }

        private async Task RefreshFriendsSafeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            try
            {
                var proxy = GetOrCreateClient();
                if (proxy == null)
                {
                    return;
                }

                var response = await Task.Run(() =>
                    proxy.ListFriends(new ListFriendsRequest
                    {
                        Token = token,
                        IncludePendingIncoming = true,
                        IncludePendingOutgoing = false
                    }));

                var items = new List<Models.FriendItem>();

                foreach (var friend in response.Friends ?? Array.Empty<FriendSummary>())
                {
                    var displayName = string.IsNullOrWhiteSpace(friend.DisplayName)
                        ? friend.Username
                        : friend.DisplayName;

                    var avatarImage = UiImageHelper.TryCreateFromUrlOrPath(friend.AvatarUrl)
                                      ?? UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

                    items.Add(new Models.FriendItem
                    {
                        AccountId = friend.AccountId,
                        DisplayName = displayName ?? string.Empty,
                        StatusText = friend.IsOnline
                        ? Properties.Langs.Lang.statusAvailable
                        : Properties.Langs.Lang.statusOffline,
                        Presence = friend.IsOnline ? "Online" : "Offline",
                        Avatar = avatarImage,
                        IsOnline = friend.IsOnline
                    });

                }

                var pendingCount = Math.Max(0, response.PendingIncoming?.Length ?? 0);

                FriendsUpdated?.Invoke(items, pendingCount);
            }
            catch (FaultException<FriendService.ServiceFault>)
            {
                // Ignorar: errores de negocio ya se manejan en UI si hace falta
            }
            catch (CommunicationException)
            {
                // Ignorar: caída de red / canal, se reintentará en el siguiente tick
            }
            catch (Exception)
            {
                // Ignorar cualquier otra cosa para no reventar el timer
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            Stop();

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
            catch
            {
                try
                {
                    client?.Abort();
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }
}
