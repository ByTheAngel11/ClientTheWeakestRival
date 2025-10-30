using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using WPFTheWeakestRival.FriendService;
using WPFTheWeakestRival.Helpers;

namespace WPFTheWeakestRival.Infrastructure
{
    public sealed class FriendManager : IDisposable
    {
        private const int HEARTBEAT_SECONDS = 30;
        private const int REFRESH_SECONDS = 45;

        private readonly FriendServiceClient client;
        private readonly DispatcherTimer heartbeatTimer;
        private readonly DispatcherTimer refreshTimer;

        public event Action<IReadOnlyList<Models.FriendItem>, int> FriendsUpdated;

        public FriendManager(string endpointName)
        {
            client = new FriendServiceClient(endpointName);
            heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HEARTBEAT_SECONDS) };
            heartbeatTimer.Tick += async (_, __) => await SendHeartbeatSafeAsync();
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(REFRESH_SECONDS) };
            refreshTimer.Tick += async (_, __) => await RefreshFriendsSafeAsync();
        }

        public void Start()
        {
            heartbeatTimer.Start();
            refreshTimer.Start();
            _ = RefreshFriendsSafeAsync();
        }

        public void Stop()
        {
            heartbeatTimer.Stop();
            refreshTimer.Stop();
        }

        public Task ManualRefreshAsync()
        {
            return RefreshFriendsSafeAsync();
        }

        private async Task SendHeartbeatSafeAsync()
        {
            try
            {
                var token = LoginWindow.AppSession.CurrentToken?.Token;
                if (string.IsNullOrWhiteSpace(token)) return;

                await client.PresenceHeartbeatAsync(new HeartbeatRequest
                {
                    Token = token,
                    Device = "WPF"
                });
            }
            catch { }
        }

        private async Task RefreshFriendsSafeAsync()
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token)) return;

            try
            {
                var res = await Task.Run(() =>
                    client.ListFriends(new ListFriendsRequest
                    {
                        Token = token,
                        IncludePendingIncoming = true,
                        IncludePendingOutgoing = false
                    }));

                var list = new List<Models.FriendItem>();
                foreach (var f in res.Friends ?? Array.Empty<FriendSummary>())
                {
                    var name = string.IsNullOrWhiteSpace(f.DisplayName) ? f.Username : f.DisplayName;
                    var img = UiImageHelper.TryCreateFromUrlOrPath(f.AvatarUrl) ?? UiImageHelper.DefaultAvatar(36);

                    list.Add(new Models.FriendItem
                    {
                        DisplayName = name ?? string.Empty,
                        StatusText = f.IsOnline ? Properties.Langs.Lang.statusAvailable : Properties.Langs.Lang.statusOffline,
                        Presence = f.IsOnline ? "Online" : "Offline",
                        Avatar = img,
                        IsOnline = f.IsOnline
                    });
                }

                var pending = Math.Max(0, res.PendingIncoming?.Length ?? 0);
                FriendsUpdated?.Invoke(list, pending);
            }
            catch (FaultException<FriendService.ServiceFault>) { }
            catch (CommunicationException) { }
            catch (Exception) { }
        }

        public void Dispose()
        {
            Stop();
            try
            {
                if (client.State == System.ServiceModel.CommunicationState.Faulted) client.Abort();
                else client.Close();
            }
            catch
            {
                try { client.Abort(); } catch { }
            }
        }
    }
}
