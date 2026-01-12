using System;
using log4net;
using WPFTheWeakestRival.Infrastructure.Gameplay;

namespace WPFTheWeakestRival.Infrastructure
{
    public static class AppServices
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AppServices));

        private const string LOBBY_ENDPOINT_CONFIGURATION_NAME = "WSDualHttpBinding_ILobbyService";
        private const string FRIEND_ENDPOINT_CONFIGURATION_NAME = "WSHttpBinding_IFriendService";

        private const string GAMEPLAY_ENDPOINT_CONFIGURATION_NAME = "NetTcpBinding_IGameplayService";

        private const string CONTEXT_STOP_FRIENDS = "AppServices.StopAll.Friends";
        private const string CONTEXT_STOP_LOBBY = "AppServices.StopAll.Lobby";
        private const string CONTEXT_STOP_GAMEPLAY = "AppServices.StopAll.Gameplay";

        private const string CONTEXT_DISPOSE_FRIENDS = "AppServices.ResetAll.Friends";
        private const string CONTEXT_DISPOSE_LOBBY = "AppServices.ResetAll.Lobby";
        private const string CONTEXT_DISPOSE_GAMEPLAY = "AppServices.ResetAll.Gameplay";

        private static LobbyHub lobby;
        private static FriendManager friends;
        private static GameplayHub gameplay;

        public static event Action<Exception> LobbyChatSendFailed;

        public static event Action<Exception> GameplayConnectionLost;
        public static event Action GameplayConnectionRestored;

        public static LobbyHub Lobby
        {
            get
            {
                if (lobby == null)
                {
                    lobby = new LobbyHub(LOBBY_ENDPOINT_CONFIGURATION_NAME);
                    lobby.ChatSendFailed += OnLobbyChatSendFailed;

                    SessionLogoutCoordinator.Attach(lobby);
                }

                return lobby;
            }
        }

        public static FriendManager Friends =>
            friends ?? (friends = new FriendManager(FRIEND_ENDPOINT_CONFIGURATION_NAME));

        public static GameplayHub Gameplay
        {
            get
            {
                if (gameplay == null)
                {
                    gameplay = new GameplayHub(GAMEPLAY_ENDPOINT_CONFIGURATION_NAME);
                    gameplay.ConnectionLost += OnGameplayConnectionLost;
                    gameplay.ConnectionRestored += OnGameplayConnectionRestored;
                }

                return gameplay;
            }
        }

        public static void StopAll()
        {
            StopSafe(friends, CONTEXT_STOP_FRIENDS);
            StopSafe(lobby, CONTEXT_STOP_LOBBY);
            StopSafe(gameplay, CONTEXT_STOP_GAMEPLAY);
        }

        public static void ResetAll()
        {
            DisposeSafe(friends, CONTEXT_DISPOSE_FRIENDS);
            friends = null;

            if (lobby != null)
            {
                lobby.ChatSendFailed -= OnLobbyChatSendFailed;
            }

            DisposeSafe(lobby, CONTEXT_DISPOSE_LOBBY);
            lobby = null;

            if (gameplay != null)
            {
                gameplay.ConnectionLost -= OnGameplayConnectionLost;
                gameplay.ConnectionRestored -= OnGameplayConnectionRestored;
            }

            DisposeSafe(gameplay, CONTEXT_DISPOSE_GAMEPLAY);
            gameplay = null;
        }

        private static void OnLobbyChatSendFailed(Exception ex)
        {
            try
            {
                LobbyChatSendFailed?.Invoke(ex);
            }
            catch (Exception forwardEx)
            {
                Logger.Warn("AppServices.OnLobbyChatSendFailed forward error.", forwardEx);
            }
        }

        private static void OnGameplayConnectionLost(Exception ex)
        {
            try
            {
                GameplayConnectionLost?.Invoke(ex);
            }
            catch (Exception forwardEx)
            {
                Logger.Warn("AppServices.OnGameplayConnectionLost forward error.", forwardEx);
            }
        }

        private static void OnGameplayConnectionRestored()
        {
            try
            {
                GameplayConnectionRestored?.Invoke();
            }
            catch (Exception forwardEx)
            {
                Logger.Warn("AppServices.OnGameplayConnectionRestored forward error.", forwardEx);
            }
        }

        private static void StopSafe(IStoppable stoppable, string context)
        {
            if (stoppable == null)
            {
                return;
            }

            try
            {
                stoppable.Stop();
                Logger.InfoFormat("{0}: stopped OK.", context);
            }
            catch (Exception ex)
            {
                Logger.Error(context, ex);
            }
        }

        private static void DisposeSafe(IDisposable disposable, string context)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
                Logger.InfoFormat("{0}: disposed OK.", context);
            }
            catch (Exception ex)
            {
                Logger.Error(context, ex);
            }
        }
    }
}
