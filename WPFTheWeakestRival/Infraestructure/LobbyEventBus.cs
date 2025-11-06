using System;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infrastructure
{
    public static class LobbyEventBus
    {
        public static event Action<LobbyInfo> LobbyUpdated;
        public static event Action<PlayerSummary> PlayerJoined;
        public static event Action<Guid> PlayerLeft;
        public static event Action<ChatMessage> ChatMessageReceived;

        public static void RaiseLobbyUpdated(LobbyInfo info) => LobbyUpdated?.Invoke(info);
        public static void RaisePlayerJoined(PlayerSummary player) => PlayerJoined?.Invoke(player);
        public static void RaisePlayerLeft(Guid playerId) => PlayerLeft?.Invoke(playerId);
        public static void RaiseChatMessageReceived(ChatMessage msg) => ChatMessageReceived?.Invoke(msg);
    }
}
