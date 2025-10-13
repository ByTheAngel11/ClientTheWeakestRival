// LobbyCallback.cs
using System;
using WPFTheWeakestRival.LobbyService;

public sealed class LobbyCallback : ILobbyServiceCallback
{
    public void OnLobbyUpdated(LobbyInfo lobby) { }
    public void OnPlayerJoined(PlayerSummary player) { }
    public void OnPlayerLeft(Guid playerId) { }
    public void OnChatMessageReceived(ChatMessage message) { }
}
