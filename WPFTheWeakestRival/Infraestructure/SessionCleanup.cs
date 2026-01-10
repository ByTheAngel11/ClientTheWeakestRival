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

            try
            {
                token = LoginWindow.AppSession.CurrentToken?.Token ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
            }

            try
            {
                AppServices.StopAll();
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
            }

            LogoutServerBestEffort(token);

            try
            {
                AppServices.ResetAll();
            }
            catch (Exception ex)
            {
                Logger.Warn(context, ex);
            }

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
                AbortClientSafe(client);
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
                }
                else
                {
                    client.Close();
                }
            }
            catch
            {
                try { client.Abort(); } catch { }
            }
        }

        private static void AbortClientSafe(ICommunicationObject client)
        {
            if (client == null)
            {
                return;
            }

            try { client.Abort(); } catch { }
        }
    }
}
