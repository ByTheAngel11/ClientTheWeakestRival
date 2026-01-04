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

        private static LobbyHub _lobby;
        private static FriendManager _friends;

        public static LobbyHub Lobby => _lobby ?? (_lobby = new LobbyHub(LOBBY_ENDPOINT_CONFIGURATION_NAME));
        public static FriendManager Friends => _friends ?? (_friends = new FriendManager(FRIEND_ENDPOINT_CONFIGURATION_NAME));

        public static void StopAll()
        {
            StopSafe(_friends, CONTEXT_STOP_FRIENDS);
            StopSafe(_lobby, CONTEXT_STOP_LOBBY);
        }

        public static void ResetAll()
        {
            DisposeSafe(_friends, CONTEXT_DISPOSE_FRIENDS);
            _friends = null;

            DisposeSafe(_lobby, CONTEXT_DISPOSE_LOBBY);
            _lobby = null;
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
