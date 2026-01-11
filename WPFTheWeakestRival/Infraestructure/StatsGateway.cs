using log4net;
using System;
using System.Globalization;
using System.ServiceModel;
using WPFTheWeakestRival.StatsService;

namespace WPFTheWeakestRival.Infrastructure
{
    internal sealed class StatsGateway
    {
        private const string STATS_ENDPOINT_NAME = "WSHttpBinding_IStatsService";

        private const string CONTEXT_GET_TOP = "StatsGateway.GetTop";
        private const string CONTEXT_CLOSE_CLIENT = "StatsGateway.CloseClientSafely";

        private const string ERROR_GET_TOP_FAILED_FORMAT =
            "StatsGateway: error while calling GetLeaderboard. Top={0}.";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(StatsGateway));

        public GetLeaderboardResponse GetTop(int top)
        {
            if (top <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(top));
            }

            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                ERROR_GET_TOP_FAILED_FORMAT,
                top);

            return ExecuteWithClient(
                client =>
                {
                    var request = new GetLeaderboardRequest
                    {
                        Top = top
                    };

                    return client.GetLeaderboard(request);
                },
                CONTEXT_GET_TOP,
                errorMessage);
        }

        private static T ExecuteWithClient<T>(
            Func<StatsServiceClient, T> operation,
            string logContext,
            string errorMessage)
        {
            StatsServiceClient client = null;

            try
            {
                client = new StatsServiceClient(STATS_ENDPOINT_NAME);
                return operation(client);
            }
            catch (FaultException ex)
            {
                Logger.Warn(logContext, ex);
                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (CommunicationException ex)
            {
                Logger.Error(logContext, ex);
                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (TimeoutException ex)
            {
                Logger.Error(logContext, ex);
                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (Exception ex)
            {
                Logger.Error(logContext, ex);
                throw new InvalidOperationException(errorMessage, ex);
            }
            finally
            {
                CloseClientSafely(client);
            }
        }

        private static void CloseClientSafely(ICommunicationObject client)
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
            catch (CommunicationException ex)
            {
                Logger.Warn(CONTEXT_CLOSE_CLIENT, ex);
                client.Abort();
            }
            catch (TimeoutException ex)
            {
                Logger.Warn(CONTEXT_CLOSE_CLIENT, ex);
                client.Abort();
            }
            catch (Exception ex)
            {
                Logger.Error(CONTEXT_CLOSE_CLIENT, ex);
                client.Abort();
            }
        }
    }
}
