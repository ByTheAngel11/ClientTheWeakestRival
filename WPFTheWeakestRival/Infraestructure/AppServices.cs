using System;

namespace WPFTheWeakestRival.Infrastructure
{
    public static class AppServices
    {
        public static readonly LobbyHub Lobby = new LobbyHub("WSDualHttpBinding_ILobbyService");
        public static readonly FriendManager Friends = new FriendManager("WSHttpBinding_IFriendService");

        public static void DisposeAll()
        {
            try { Friends?.Dispose(); } catch { }
            try { Lobby?.Dispose(); } catch { }
        }
    }
}
