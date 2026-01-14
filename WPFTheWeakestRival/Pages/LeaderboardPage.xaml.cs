using log4net;
using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.Infrastructure.Faults;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;
using WPFTheWeakestRival.StatsService;

namespace WPFTheWeakestRival.Pages
{
    public partial class LeaderboardPage : Page
    {
        private const int DEFAULT_TOP = 10;

        private const string DIALOG_TITLE = "Marcadores";

        private const string MSG_SERVER_UNAVAILABLE =
            "No se encuentra el servidor o no hay conexión.";

        private const string MSG_DATABASE_ERROR =
            "El servidor no pudo acceder a la base de datos.";

        private const string MSG_LOAD_FAILED =
            "No se pudo cargar el marcador por error en base de datos.";

        private const string MSG_GENERIC_ERROR =
            "Ocurrió un error al cargar el marcador.";

        private const string DB_FAULT_TOKEN = "DB";

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

                var request = new GetLeaderboardRequest
                {
                    Top = DEFAULT_TOP
                };

                GetLeaderboardResponse response =
                    await Task.Run(() => statsClient.GetLeaderboard(request));

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
                string code = ex.Detail != null ? (ex.Detail.Code ?? string.Empty) : string.Empty;
                string message = ex.Detail != null ? (ex.Detail.Message ?? string.Empty) : (ex.Message ?? string.Empty);

                if (AuthTokenInvalidUiHandler.TryHandleInvalidToken(
                        code,
                        message,
                        "LeaderboardPage.LoadLeaderboardAsync",
                        Logger,
                        this))
                {
                    return;
                }

                Logger.WarnFormat(
                    "LeaderboardPage.LoadLeaderboardAsync fault. Code={0}, Message={1}",
                    code ?? string.Empty,
                    message ?? string.Empty);

                if (IsDatabaseFault(ex.Detail))
                {
                    MessageBox.Show(
                        MSG_DATABASE_ERROR,
                        DIALOG_TITLE,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                MessageBox.Show(
                    MSG_LOAD_FAILED,
                    DIALOG_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("LeaderboardPage.LoadLeaderboardAsync: communication error.", ex);

                MessageBox.Show(
                    MSG_SERVER_UNAVAILABLE,
                    DIALOG_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                Logger.Error("LeaderboardPage.LoadLeaderboardAsync: timeout.", ex);

                MessageBox.Show(
                    MSG_SERVER_UNAVAILABLE,
                    DIALOG_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("LeaderboardPage.LoadLeaderboardAsync: unexpected error.", ex);

                MessageBox.Show(
                    MSG_GENERIC_ERROR,
                    DIALOG_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static bool IsDatabaseFault(ServiceFault fault)
        {
            if (fault == null)
            {
                return false;
            }

            string code = fault.Code ?? string.Empty;

            return code.IndexOf(DB_FAULT_TOKEN, StringComparison.OrdinalIgnoreCase) >= 0;
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
                Logger.Warn(string.Concat(context, ": WCF client close failed. Aborting."), ex);

                try
                {
                    client.Abort();
                }
                catch (Exception abortEx)
                {
                    Logger.Warn(string.Concat(context, ": WCF client abort failed."), abortEx);
                }
            }
        }
    }
}
