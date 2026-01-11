using log4net;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infrastructure
{
    public sealed class FriendManager : IDisposable, IStoppable
    {
        private const int HEARTBEAT_SECONDS = 30;
        private const int REFRESH_SECONDS = 45;

        private const int DEFAULT_AVATAR_SIZE = 36;
        private const string DEVICE_NAME = "WPF";

        private const string LOG_CTX_ABORT_CLIENT = "FriendManager.AbortClient";
        private const string LOG_CTX_CLOSE_CLIENT = "FriendManager.CloseClient";
        private const string LOG_CTX_GET_OR_CREATE_CLIENT = "FriendManager.GetOrCreateClient";
        private const string LOG_CTX_SEND_HEARTBEAT = "FriendManager.SendHeartbeatSafeAsync";
        private const string LOG_CTX_REFRESH_FRIENDS = "FriendManager.RefreshFriendsSafeAsync";
        private const string LOG_CTX_WARM_UP_AVATARS = "FriendManager.WarmUpAvatarsAsync";
        private const string LOG_CTX_DISPOSE = "FriendManager.Dispose";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(FriendManager));

        private readonly string endpointName;
        private readonly DispatcherTimer heartbeatTimer;
        private readonly DispatcherTimer refreshTimer;

        private FriendServiceClient client;
        private bool isDisposed;

        private readonly object lastStateLock = new object();
        private List<FriendItem> lastItems = new List<FriendItem>();
        private int lastPendingCount;

        public event Action<IReadOnlyList<FriendItem>, int> FriendsUpdated;

        public FriendManager(string endpointName)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException("Endpoint name cannot be null or whitespace.", nameof(endpointName));
            }

            this.endpointName = endpointName;
            client = CreateClient();

            heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HEARTBEAT_SECONDS) };
            heartbeatTimer.Tick += async (_, __) => await SendHeartbeatSafeAsync();

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(REFRESH_SECONDS) };
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
                AbortClientSafe(client, LOG_CTX_GET_OR_CREATE_CLIENT);
                client = CreateClient();
            }

            return client;
        }

        private void CloseClientSafe()
        {
            FriendServiceClient local = client;
            client = null;

            CloseOrAbortClientSafe(local);
        }

        private static void CloseOrAbortClientSafe(ICommunicationObject communicationObject)
        {
            if (communicationObject == null)
            {
                return;
            }

            try
            {
                if (communicationObject.State == CommunicationState.Faulted)
                {
                    communicationObject.Abort();
                    return;
                }

                communicationObject.Close();
            }
            catch (TimeoutException ex)
            {
                Logger.Debug(LOG_CTX_CLOSE_CLIENT, ex);
                AbortClientSafe(communicationObject, LOG_CTX_CLOSE_CLIENT);
            }
            catch (CommunicationException ex)
            {
                Logger.Debug(LOG_CTX_CLOSE_CLIENT, ex);
                AbortClientSafe(communicationObject, LOG_CTX_CLOSE_CLIENT);
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_CTX_CLOSE_CLIENT, ex);
                AbortClientSafe(communicationObject, LOG_CTX_CLOSE_CLIENT);
            }
        }

        private static void AbortClientSafe(ICommunicationObject communicationObject, string context)
        {
            if (communicationObject == null)
            {
                return;
            }

            try
            {
                communicationObject.Abort();
            }
            catch (TimeoutException ex)
            {
                Logger.Debug(context ?? LOG_CTX_ABORT_CLIENT, ex);
            }
            catch (CommunicationException ex)
            {
                Logger.Debug(context ?? LOG_CTX_ABORT_CLIENT, ex);
            }
            catch (Exception ex)
            {
                Logger.Warn(context ?? LOG_CTX_ABORT_CLIENT, ex);
            }
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
            catch (TimeoutException ex)
            {
                Logger.Debug(LOG_CTX_SEND_HEARTBEAT, ex);
            }
            catch (CommunicationException ex)
            {
                Logger.Debug(LOG_CTX_SEND_HEARTBEAT, ex);
            }
            catch (Exception ex)
            {
                Logger.Debug(LOG_CTX_SEND_HEARTBEAT, ex);
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

                var builtItems = BuildFriendItems(response);
                var pendingCount = Math.Max(0, response?.PendingIncoming?.Length ?? 0);

                lock (lastStateLock)
                {
                    lastItems = new List<FriendItem>(builtItems);
                    lastPendingCount = pendingCount;
                }

                FriendsUpdated?.Invoke(builtItems, pendingCount);

                _ = WarmUpAvatarsAsync(token, response?.Friends ?? Array.Empty<FriendSummary>());
            }
            catch (FaultException<FriendService.ServiceFault> ex)
            {
                string code = ex.Detail?.Code;

                if (SessionLogoutCoordinator.IsSessionFault(code))
                {
                    Logger.WarnFormat("FriendManager: session fault received. Code={0}", code ?? string.Empty);
                    SessionLogoutCoordinator.ForceLogout(code);
                    return;
                }

                Logger.Warn("Friend service fault while refreshing friends.", ex);
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("Communication error while refreshing friends.", ex);
            }
            catch (TimeoutException ex)
            {
                Logger.Warn("Timeout while refreshing friends.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error while refreshing friends.", ex);
            }
        }

        private async Task WarmUpAvatarsAsync(string token, FriendSummary[] friends)
        {
            if (friends == null || friends.Length == 0)
            {
                return;
            }

            var proxy = GetOrCreateClient();
            if (proxy == null)
            {
                return;
            }

            for (int i = 0; i < friends.Length; i++)
            {
                FriendSummary f = friends[i];
                if (f == null || !f.HasProfileImage || string.IsNullOrWhiteSpace(f.ProfileImageCode))
                {
                    continue;
                }

                try
                {
                    ImageSource image = await ProfileImageCache.Current.GetOrFetchAsync(
                        f.AccountId,
                        f.ProfileImageCode,
                        DEFAULT_AVATAR_SIZE,
                        async () =>
                        {
                            var resp = await proxy.GetProfileImageAsync(new FriendService.GetProfileImageRequest
                            {
                                Token = token,
                                AccountId = f.AccountId,
                                ProfileImageCode = f.ProfileImageCode
                            });

                            return resp?.ImageBytes ?? Array.Empty<byte>();
                        });

                    if (image != null)
                    {
                        ApplyAvatarToLastItems(f.AccountId, image);
                    }
                }
                catch (TimeoutException ex)
                {
                    Logger.Debug(LOG_CTX_WARM_UP_AVATARS, ex);
                }
                catch (CommunicationException ex)
                {
                    Logger.Debug(LOG_CTX_WARM_UP_AVATARS, ex);
                }
                catch (Exception ex)
                {
                    Logger.Debug(LOG_CTX_WARM_UP_AVATARS, ex);
                }
            }
        }

        private void ApplyAvatarToLastItems(int accountId, ImageSource avatar)
        {
            if (avatar == null)
            {
                return;
            }

            List<FriendItem> snapshot;
            int pending;

            lock (lastStateLock)
            {
                for (int i = 0; i < lastItems.Count; i++)
                {
                    if (lastItems[i] != null && lastItems[i].AccountId == accountId)
                    {
                        lastItems[i].Avatar = avatar;
                    }
                }

                snapshot = new List<FriendItem>(lastItems);
                pending = lastPendingCount;
            }

            FriendsUpdated?.Invoke(snapshot, pending);
        }

        private static IReadOnlyList<FriendItem> BuildFriendItems(ListFriendsResponse response)
        {
            var items = new List<FriendItem>();

            foreach (var friend in response?.Friends ?? Array.Empty<FriendSummary>())
            {
                FriendItem item = BuildFriendItem(friend);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private static FriendItem BuildFriendItem(FriendSummary friend)
        {
            if (friend == null)
            {
                return null;
            }

            var displayName = string.IsNullOrWhiteSpace(friend.DisplayName)
                ? friend.Username
                : friend.DisplayName;

            var presenceText = friend.IsOnline
                ? Lang.statusAvailable
                : Lang.statusOffline;

            var avatarImage = UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

            return new FriendItem
            {
                AccountId = friend.AccountId,
                DisplayName = displayName ?? string.Empty,
                StatusText = presenceText,
                Presence = presenceText,
                Avatar = avatarImage,
                IsOnline = friend.IsOnline
            };
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            try
            {
                heartbeatTimer.Stop();
                refreshTimer.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn(LOG_CTX_DISPOSE, ex);
            }

            CloseClientSafe();
        }
    }
}