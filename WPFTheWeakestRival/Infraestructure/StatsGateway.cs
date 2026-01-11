using log4net;
using System;
using System.ServiceModel;
using WPFTheWeakestRival.StatsService;

namespace WPFTheWeakestRival.Infrastructure
{
    internal sealed class StatsGateway
    {
        private const string STATS_ENDPOINT_NAME = "WSHttpBinding_IStatsService";

        private const string CONTEXT_GET_TOP = "StatsGateway.GetTop";
        private const string CONTEXT_CLOSE_CLIENT = "StatsGateway.CloseClientSafely";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(StatsGateway));

        public GetLeaderboardResponse GetTop(int top)
        {
            if (top <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(top));
            }

            StatsServiceClient client = null;

            try
            {
                client = new StatsServiceClient(STATS_ENDPOINT_NAME);

                var request = new GetLeaderboardRequest
                {
                    Top = top
                };

                return client.GetLeaderboard(request);
            }
            catch (FaultException ex)
            {
                Logger.Warn(CONTEXT_GET_TOP, ex);
                throw;
            }
            catch (CommunicationException ex)
            {
                Logger.Error(CONTEXT_GET_TOP, ex);
                throw;
            }
            catch (TimeoutException ex)
            {
                Logger.Error(CONTEXT_GET_TOP, ex);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(CONTEXT_GET_TOP, ex);
                throw;
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
                throw;
            }
        }
    }
}
