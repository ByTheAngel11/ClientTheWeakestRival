using log4net;
using System;
using System.ServiceModel;
using WPFTheWeakestRival.AuthService;

namespace WPFTheWeakestRival.Infrastructure
{
    public static class SessionCleanup
    {
        private const string AUTH_ENDPOINT_CONFIGURATION_NAME = "WSHttpBinding_IAuthService";

        private const string CTX_SHUTDOWN = "SessionCleanup.Shutdown";
        private const string CTX_LOGOUT = "SessionCleanup.LogoutServerBestEffort";

        private const string CTX_CLOSE_CLIENT_SAFE = "SessionCleanup.CloseClientSafe";
        private const string CTX_ABORT_CLIENT_SAFE = "SessionCleanup.AbortClientSafe";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(SessionCleanup));

        public static void Shutdown()
        {
            ShutdownInternal(CTX_SHUTDOWN);
        }

        public static void Shutdown(string context)
        {
            ShutdownInternal(string.IsNullOrWhiteSpace(context) ? CTX_SHUTDOWN : context);
        }

        private static void ShutdownInternal(string context)
        {
            string token = string.Empty;

            token = GetTokenBestEffort(context);

            StopServicesBestEffort(context);

            LogoutServerBestEffort(token);

            ResetServicesBestEffort(context);

            ClearSessionBestEffort(context);
        }

        private static string GetTokenBestEffort(string context)
        {
            try
            {
                return LoginWindow.AppSession.CurrentToken?.Token ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
                return string.Empty;
            }
        }

        private static void StopServicesBestEffort(string context)
        {
            try
            {
                AppServices.StopAll();
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
            }
        }

        private static void ResetServicesBestEffort(string context)
        {
            try
            {
                AppServices.ResetAll();
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
            }
        }

        private static void ClearSessionBestEffort(string context)
        {
            try
            {
                LoginWindow.AppSession.CurrentToken = null;
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
            }
        }

        private static void LogoutServerBestEffort(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            AuthServiceClient client = null;

            try
            {
                client = new AuthServiceClient(AUTH_ENDPOINT_CONFIGURATION_NAME);

                client.Logout(new LogoutRequest
                {
                    Token = token
                });

                CloseClientSafe(client);
            }
            catch (Exception ex)
            {
                Logger.Warn(CTX_LOGOUT, ex);
                AbortClientSafe(client, CTX_LOGOUT, ex);
            }
        }

        private static void CloseClientSafe(ICommunicationObject client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                if (client.State == CommunicationState.Faulted)
                {
                    client.Abort();
                    return;
                }

                client.Close();
            }
            catch (Exception ex)
            {
                Logger.Warn(CTX_CLOSE_CLIENT_SAFE, ex);
                AbortClientSafe(client, CTX_CLOSE_CLIENT_SAFE, ex);
            }
        }

        private static void AbortClientSafe(ICommunicationObject client, string context, Exception rootException)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                client.Abort();
            }
            catch (Exception ex)
            {
                Logger.Warn(context ?? CTX_ABORT_CLIENT_SAFE, ex);
                System.GC.KeepAlive(rootException);
            }
        }
    }
}
