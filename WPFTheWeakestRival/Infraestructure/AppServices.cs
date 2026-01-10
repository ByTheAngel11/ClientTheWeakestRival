using System;
using log4net;

namespace WPFTheWeakestRival.Infrastructure
{
    public static class AppServices
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AppServices));

        private const string LOBBY_ENDPOINT_CONFIGURATION_NAME = "WSDualHttpBinding_ILobbyService";
        private const string FRIEND_ENDPOINT_CONFIGURATION_NAME = "WSHttpBinding_IFriendService";

        private const string CONTEXT_STOP_FRIENDS = "AppServices.StopAll.Friends";
        private const string CONTEXT_STOP_LOBBY = "AppServices.StopAll.Lobby";

        private const string CONTEXT_DISPOSE_FRIENDS = "AppServices.ResetAll.Friends";
        private const string CONTEXT_DISPOSE_LOBBY = "AppServices.ResetAll.Lobby";

        private static LobbyHub lobby;
        private static FriendManager friends;

        public static LobbyHub Lobby
        {
            get
            {
                if (lobby == null)
                {
                    lobby = new LobbyHub(LOBBY_ENDPOINT_CONFIGURATION_NAME);
                    SessionLogoutCoordinator.Attach(lobby);
                }

                return lobby;
            }
        }

        public static FriendManager Friends => friends ?? (friends = new FriendManager(FRIEND_ENDPOINT_CONFIGURATION_NAME));

        public static void StopAll()
        {
            StopSafe(friends, CONTEXT_STOP_FRIENDS);
            StopSafe(lobby, CONTEXT_STOP_LOBBY);
        }

        public static void ResetAll()
        {
            DisposeSafe(friends, CONTEXT_DISPOSE_FRIENDS);
            friends = null;

            DisposeSafe(lobby, CONTEXT_DISPOSE_LOBBY);
            lobby = null;
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
