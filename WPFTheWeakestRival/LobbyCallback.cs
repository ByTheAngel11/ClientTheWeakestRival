using System;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Callbacks
{
    public sealed class LobbyCallback : ILobbyServiceCallback
    {
        public void OnLobbyUpdated(LobbyInfo lobby)
        {
        }

        public void OnPlayerJoined(PlayerSummary player)
        {
        }

        public void OnPlayerLeft(Guid playerId)
        {
        }

        public void OnChatMessageReceived(ChatMessage message)
        {
        }

        // NUEVO: requerido por ILobbyServiceCallback
        public void OnMatchStarted(MatchInfo match)
        {
        }
    }
}
