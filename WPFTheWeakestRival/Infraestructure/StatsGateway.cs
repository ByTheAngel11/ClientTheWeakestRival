using System;
using System.ServiceModel;
using WPFTheWeakestRival.StatsService;

public sealed class StatsGateway
{
    public GetLeaderboardResponse GetTop(int top)
    {
        StatsServiceClient client = null;

        try
        {
            client = new StatsServiceClient("WSHttpBinding_IStatsService");
            return client.GetLeaderboard(new GetLeaderboardRequest { Top = top });
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
        catch
        {
            client.Abort();
            throw;
        }
    }
}
