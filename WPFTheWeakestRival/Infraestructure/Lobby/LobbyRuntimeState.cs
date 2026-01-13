using System;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyRuntimeState
    {
        internal Guid? CurrentLobbyId { get; set; }

        internal string CurrentAccessCode { get; set; } = string.Empty;

        internal string MyDisplayName { get; set; } = string.Empty;

        internal bool IsOpeningMatchWindow { get; set; }

        internal bool IsNavigatingToLogin { get; set; }

        internal bool IsAutoWaitingForReconnect { get; set; }
    }
}
