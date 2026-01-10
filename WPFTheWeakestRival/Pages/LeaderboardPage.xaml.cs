using log4net;
using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.StatsService;

namespace WPFTheWeakestRival.Pages
{
    public partial class LeaderboardPage : Page
    {
        private const int DEFAULT_TOP = 10;

        private const string WINDOW_TITLE = "Marcadores";
        private const string MSG_LOAD_FAILED = "No se pudo cargar el marcador.";
        private const string MSG_NO_CONNECTION = "Sin conexión con el servidor.";
        private const string MSG_GENERIC_ERROR = "Ocurrió un error al cargar el marcador.";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LeaderboardPage));

        private readonly StatsServiceClient statsClient;

        public event EventHandler CloseRequested;

        public ObservableCollection<LeaderboardRow> LeaderboardEntries { get; } =
            new ObservableCollection<LeaderboardRow>();

        public LeaderboardPage()
        {
            InitializeComponent();

            DataContext = this;

            statsClient = new StatsServiceClient("WSHttpBinding_IStatsService");

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadLeaderboardAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CloseClientSafely(statsClient, "LeaderboardPage.OnUnloaded");
        }

        private async Task LoadLeaderboardAsync()
        {
            try
            {
                LeaderboardEntries.Clear();

                GetLeaderboardResponse response = await Task.Run(() =>
                    statsClient.GetLeaderboard(new GetLeaderboardRequest { Top = DEFAULT_TOP }));

                LeaderboardEntry[] entries = response?.Entries ?? Array.Empty<LeaderboardEntry>();

                foreach (LeaderboardEntry entry in entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    LeaderboardEntries.Add(new LeaderboardRow
                    {
                        Username = entry.PlayerName ?? string.Empty,
                        Score = entry.BestBank
                    });
                }
            }
            catch (FaultException<ServiceFault> ex)
            {
                Logger.Warn("LeaderboardPage.LoadLeaderboardAsync: stats fault.", ex);
                MessageBox.Show(MSG_LOAD_FAILED, WINDOW_TITLE, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("LeaderboardPage.LoadLeaderboardAsync: communication error.", ex);
                MessageBox.Show(MSG_NO_CONNECTION, WINDOW_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("LeaderboardPage.LoadLeaderboardAsync: timeout.", ex);
                MessageBox.Show(MSG_NO_CONNECTION, WINDOW_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("LeaderboardPage.LoadLeaderboardAsync: unexpected error.", ex);
                MessageBox.Show(MSG_GENERIC_ERROR, WINDOW_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private static void CloseClientSafely(ICommunicationObject client, string context)
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
                Logger.Warn(context + ": WCF client close failed. Aborting.", ex);

                try
                {
                    client.Abort();
                }
                catch (Exception abortEx)
                {
                    Logger.Warn(context + ": WCF client abort failed.", abortEx);
                }
            }
        }
    }
}
