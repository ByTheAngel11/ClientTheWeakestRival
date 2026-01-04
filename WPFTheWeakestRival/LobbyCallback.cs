using System;
using WPFTheWeakestRival.Infraestructure;
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

        public void OnMatchStarted(MatchInfo match) 
        { 
        }

        public void ForcedLogout(ForcedLogoutNotification notification)
        {
            ForcedLogoutCoordinator.Handle(notification);
        }
    }
}
