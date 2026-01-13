using log4net;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyPlayersController : IDisposable
    {
        private const string ErrorCopyCodeNoCode = "No hay un código de lobby para copiar.";
        private const string ErrorCopyCodeGeneric = "Ocurrió un error al copiar el código al portapapeles.";

        private readonly LobbyUiDispatcher ui;
        private readonly LobbyRuntimeState state;
        private readonly ILog logger;

        private readonly TextBlock lobbyHeaderText;
        private readonly TextBlock accessCodeText;
        private readonly ListBox playersList;

        private readonly ObservableCollection<LobbyPlayerItem> players;

        internal LobbyPlayersController(
            LobbyUiDispatcher ui,
            LobbyRuntimeState state,
            TextBlock lobbyHeaderText,
            TextBlock accessCodeText,
            ListBox playersList,
            ILog logger)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
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
                        ErrorCopyCodeNoCode,
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
                    ErrorCopyCodeGeneric,
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

                if (item == null || item.IsMe || !state.CurrentLobbyId.HasValue || state.CurrentLobbyId.Value == Guid.Empty)
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

        internal void MenuItemReportPlayerClick(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    Lang.UiGenericError,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error("MenuItemReportPlayerClick error.", ex);

                MessageBox.Show(
                    Lang.UiGenericError,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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

        private void OnPlayerJoinedFromHub(PlayerSummary _)
        {
        }

        private void OnPlayerLeftFromHub(Guid _)
        {
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

        public void Dispose()
        {
            AppServices.Lobby.LobbyUpdated -= OnLobbyUpdatedFromHub;
            AppServices.Lobby.PlayerJoined -= OnPlayerJoinedFromHub;
            AppServices.Lobby.PlayerLeft -= OnPlayerLeftFromHub;
        }
    }
}
