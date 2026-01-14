using log4net;
using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;
using WPFTheWeakestRival.Windows;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyPlayersController : IDisposable
    {
        private const string ERROR_COPY_CODE_NO_CODE = "No hay un código de lobby para copiar.";
        private const string ERROR_COPY_CODE_GENERIC = "Ocurrió un error al copiar el código al portapapeles.";

        private const string LOG_PLAYER_DELTA_IGNORED_TEMPLATE =
            "Player delta ignored (UI updates via LobbyUpdated snapshot). Event={0}.";

        private const string PLAYER_DELTA_EVENT_JOINED = "PlayerJoined";
        private const string PLAYER_DELTA_EVENT_LEFT = "PlayerLeft";

        private const string REPORT_ENDPOINT_NAME = "WSHttpBinding_IReportService";
        private const string FAULT_REPORT_COOLDOWN = "REPORT_COOLDOWN";

        private const byte ACCOUNT_STATUS_SUSPENDED = 3;
        private const byte ACCOUNT_STATUS_BANNED = 4;

        private const string SANCTION_END_TIME_FORMAT = "g";

        private readonly LobbyUiDispatcher ui;
        private readonly LobbyRuntimeState state;
        private readonly ILog logger;

        private readonly Window ownerWindow;

        private readonly TextBlock lobbyHeaderText;
        private readonly TextBlock accessCodeText;
        private readonly ListBox playersList;

        private readonly ObservableCollection<LobbyPlayerItem> players;

        internal LobbyPlayersController(
            LobbyUiDispatcher ui,
            LobbyRuntimeState state,
            Window ownerWindow,
            TextBlock lobbyHeaderText,
            TextBlock accessCodeText,
            ListBox playersList,
            ILog logger)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            this.lobbyHeaderText = lobbyHeaderText;
            this.accessCodeText = accessCodeText;
            this.playersList = playersList;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            players = new ObservableCollection<LobbyPlayerItem>();

            if (this.playersList != null)
            {
                this.playersList.ItemsSource = players;
            }

            AppServices.Lobby.LobbyUpdated += OnLobbyUpdatedFromHub;
            AppServices.Lobby.PlayerJoined += OnPlayerJoinedFromHub;
            AppServices.Lobby.PlayerLeft += OnPlayerLeftFromHub;
        }

        internal void InitializeExistingLobby(Guid lobbyId, string accessCode, string lobbyName)
        {
            state.CurrentLobbyId = lobbyId;
            state.CurrentAccessCode = accessCode ?? string.Empty;

            UpdateLobbyHeader(lobbyName, state.CurrentAccessCode);
        }

        internal void InitializeExistingLobby(LobbyInfo info)
        {
            if (info == null)
            {
                return;
            }

            state.CurrentLobbyId = info.LobbyId;

            state.CurrentAccessCode = string.IsNullOrWhiteSpace(info.AccessCode)
                ? string.Empty
                : info.AccessCode;

            UpdateLobbyHeader(info.LobbyName, state.CurrentAccessCode);

            LobbyAvatarHelper.RebuildLobbyPlayers(players, info.Players, MapPlayerToLobbyItem);
        }

        internal void CopyCodeToClipboard()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(state.CurrentAccessCode))
                {
                    MessageBox.Show(
                        ERROR_COPY_CODE_NO_CODE,
                        Lang.lobbyTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                Clipboard.SetText(state.CurrentAccessCode);
            }
            catch (Exception ex)
            {
                logger.Error("CopyCodeToClipboard error.", ex);

                MessageBox.Show(
                    ERROR_COPY_CODE_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        internal void LobbyPlayerContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                var element = sender as FrameworkElement;
                var item = element?.DataContext as LobbyPlayerItem;

                if (item == null ||
                    item.IsMe ||
                    !state.CurrentLobbyId.HasValue ||
                    state.CurrentLobbyId.Value == Guid.Empty)
                {
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                logger.Error("LobbyPlayerContextMenuOpening error.", ex);
                e.Handled = true;
            }
        }

        internal async void MenuItemReportPlayerClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var menuItem = sender as MenuItem;
                var targetPlayer = menuItem?.CommandParameter as LobbyPlayerItem;

                if (targetPlayer == null || targetPlayer.IsMe)
                {
                    return;
                }

                var session = LoginWindow.AppSession.CurrentToken;
                string token = session != null ? session.Token : null;

                if (string.IsNullOrWhiteSpace(token))
                {
                    MessageBox.Show(Lang.noValidSessionCode);
                    return;
                }

                var dialog = new ReportPlayerWindow(targetPlayer.DisplayName)
                {
                    Owner = ownerWindow
                };

                bool? dialogResult = dialog.ShowDialog();
                if (dialogResult != true)
                {
                    return;
                }

                var client = new ReportService.ReportServiceClient(REPORT_ENDPOINT_NAME);

                try
                {
                    var request = new ReportService.SubmitPlayerReportRequest
                    {
                        Token = token,
                        ReportedAccountId = targetPlayer.AccountId,
                        LobbyId = state.CurrentLobbyId,
                        ReasonCode = (ReportService.ReportReasonCode)dialog.SelectedReasonCode,
                        Comment = string.IsNullOrWhiteSpace(dialog.Comment) ? null : dialog.Comment
                    };

                    ReportService.SubmitPlayerReportResponse response =
                        await Task.Run(() => client.SubmitPlayerReport(request)).ConfigureAwait(false);

                    ui.Ui(() => ShowReportResult(response));
                }
                finally
                {
                    TryCloseClient(client);
                }
            }
            catch (FaultException<ReportService.ServiceFault> ex)
            {
                if (ex.Detail != null &&
                    string.Equals(ex.Detail.Code, FAULT_REPORT_COOLDOWN, StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        Lang.reportCooldown,
                        Lang.reportPlayer,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                logger.Warn("Report fault in lobby.", ex);

                MessageBox.Show(
                    Lang.reportFailed,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                logger.Error("Communication error while submitting report.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                logger.Error("Unexpected error while submitting report.", ex);

                MessageBox.Show(
                    Lang.reportUnexpectedError,
                    Lang.reportPlayer,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowReportResult(ReportService.SubmitPlayerReportResponse response)
        {
            if (response != null && response.SanctionApplied)
            {
                if (response.SanctionType == ACCOUNT_STATUS_SUSPENDED && response.SanctionEndAtUtc.HasValue)
                {
                    string localEnd = response.SanctionEndAtUtc.Value
                        .ToLocalTime()
                        .ToString(SANCTION_END_TIME_FORMAT);

                    MessageBox.Show(
                        string.Format(Lang.reportSanctionTemporary, localEnd),
                        Lang.reportPlayer,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                if (response.SanctionType == ACCOUNT_STATUS_BANNED)
                {
                    MessageBox.Show(
                        Lang.reportSanctionPermanent,
                        Lang.reportPlayer,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }
            }

            MessageBox.Show(
                Lang.reportSent,
                Lang.reportPlayer,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void UpdateLobbyHeader(string lobbyName, string accessCode)
        {
            if (lobbyHeaderText != null)
            {
                lobbyHeaderText.Text = string.IsNullOrWhiteSpace(lobbyName)
                    ? Lang.lobbyTitle
                    : lobbyName;
            }

            if (accessCodeText != null)
            {
                accessCodeText.Text = string.IsNullOrWhiteSpace(accessCode)
                    ? string.Empty
                    : Lang.lobbyCodePrefix + accessCode;
            }
        }

        private void OnLobbyUpdatedFromHub(LobbyInfo info)
        {
            try
            {
                if (info == null)
                {
                    return;
                }

                state.CurrentLobbyId = info.LobbyId;

                if (!string.IsNullOrWhiteSpace(info.AccessCode))
                {
                    state.CurrentAccessCode = info.AccessCode;
                }

                ui.Ui(() =>
                {
                    UpdateLobbyHeader(info.LobbyName, state.CurrentAccessCode);
                    LobbyAvatarHelper.RebuildLobbyPlayers(players, info.Players, MapPlayerToLobbyItem);
                });
            }
            catch (Exception ex)
            {
                logger.Error("OnLobbyUpdatedFromHub error.", ex);
            }
        }

        private void OnPlayerJoinedFromHub(PlayerSummary player)
        {
            try
            {
                logger.DebugFormat(LOG_PLAYER_DELTA_IGNORED_TEMPLATE, PLAYER_DELTA_EVENT_JOINED);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
            }
        }

        private void OnPlayerLeftFromHub(Guid playerIdOrLobbyId)
        {
            try
            {
                logger.DebugFormat(LOG_PLAYER_DELTA_IGNORED_TEMPLATE, PLAYER_DELTA_EVENT_LEFT);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
            }
        }

        private static LobbyPlayerItem MapPlayerToLobbyItem(AccountMini account)
        {
            var item = LobbyAvatarHelper.BuildFromAccountMini(account);
            if (item == null)
            {
                return null;
            }

            var session = LoginWindow.AppSession.CurrentToken;
            if (session != null && session.UserId == item.AccountId)
            {
                item.IsMe = true;
            }

            return item;
        }

        private void TryCloseClient(ICommunicationObject client)
        {
            try
            {
                client.Close();
            }
            catch (Exception ex)
            {
                logger.Warn("Error closing report client.", ex);
                client.Abort();
            }
        }

        public void Dispose()
        {
            AppServices.Lobby.LobbyUpdated -= OnLobbyUpdatedFromHub;
            AppServices.Lobby.PlayerJoined -= OnPlayerJoinedFromHub;
            AppServices.Lobby.PlayerLeft -= OnPlayerLeftFromHub;
        }
    }
}
