using log4net;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Pages;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyMatchController : IDisposable
    {
        private const double SETTINGS_WINDOW_WIDTH = 480;
        private const double SETTINGS_WINDOW_HEIGHT = 420;

        private const double PROFILE_WINDOW_WIDTH = 720;
        private const double PROFILE_WINDOW_HEIGHT = 460;

        private const int DEFAULT_MAX_PLAYERS = 4;

        private const decimal DEFAULT_STARTING_SCORE = 0m;
        private const decimal DEFAULT_MAX_SCORE = 100m;
        private const decimal DEFAULT_POINTS_CORRECT = 10m;
        private const decimal DEFAULT_POINTS_WRONG = -5m;
        private const decimal DEFAULT_POINTS_ELIMINATION_GAIN = 5m;

        private const bool DEFAULT_IS_PRIVATE = true;
        private const bool DEFAULT_TIEBREAK_COINFLIP_ALLOWED = true;

        private const string ERROR_START_MATCH_GENERIC = "Ocurrió un error al iniciar la partida.";

        private const string AUTH_ENDPOINT_CONFIGURATION_NAME = "WSHttpBinding_IAuthService";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyMatchController));

        private readonly LobbyUiDispatcher ui;
        private readonly LobbyRuntimeState state;
        private readonly Window lobbyWindow;
        private readonly Button btnStart;
        private readonly LobbyProfileController profileController;
        private readonly ILog logger;

        private bool isPrivate = DEFAULT_IS_PRIVATE;
        private int maxPlayers = DEFAULT_MAX_PLAYERS;

        private decimal startingScore = DEFAULT_STARTING_SCORE;
        private decimal maxScore = DEFAULT_MAX_SCORE;
        private decimal pointsCorrect = DEFAULT_POINTS_CORRECT;
        private decimal pointsWrong = DEFAULT_POINTS_WRONG;
        private decimal pointsEliminationGain = DEFAULT_POINTS_ELIMINATION_GAIN;

        private bool isTiebreakCoinflipAllowed = DEFAULT_TIEBREAK_COINFLIP_ALLOWED;

        internal LobbyMatchController(
            LobbyUiDispatcher ui,
            LobbyRuntimeState state,
            Window lobbyWindow,
            Button btnStart,
            LobbyProfileController profileController,
            ILog logger)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.lobbyWindow = lobbyWindow ?? throw new ArgumentNullException(nameof(lobbyWindow));
            this.btnStart = btnStart;
            this.profileController = profileController ?? throw new ArgumentNullException(nameof(profileController));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            AppServices.Lobby.MatchStarted += OnMatchStartedFromHub;
        }

        internal void OpenSettings()
        {
            var defaults = new MatchSettingsDefaults(
                isPrivate,
                maxPlayers,
                startingScore,
                maxScore,
                pointsCorrect,
                pointsWrong,
                pointsEliminationGain,
                isTiebreakCoinflipAllowed);

            var page = new MatchSettingsPage(defaults);

            var settingsWindow = BuildPageDialog(
                page,
                Lang.lblSettings,
                SETTINGS_WINDOW_WIDTH,
                SETTINGS_WINDOW_HEIGHT,
                true);

            bool? dialogResult = settingsWindow.ShowDialog();
            if (dialogResult == true)
            {
                isPrivate = page.IsPrivate;
                maxPlayers = page.MaxPlayers;
                startingScore = page.StartingScore;
                maxScore = page.MaxScore;
                pointsCorrect = page.PointsPerCorrect;
                pointsWrong = page.PointsPerWrong;
                pointsEliminationGain = page.PointsPerEliminationGain;
                isTiebreakCoinflipAllowed = page.AllowTiebreakCoinflip;
            }
        }

        internal async void StartMatchAsync()
        {
            string token = SessionTokenProvider.GetTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            if (btnStart != null)
            {
                btnStart.IsEnabled = false;
            }

            try
            {
                var client = AppServices.Lobby.RawClient;

                var request = new StartLobbyMatchRequest
                {
                    Token = token
                };

                await Task.Run(() => client.StartLobbyMatch(request));
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                logger.Warn("Fault starting match from lobby.", ex);

                MessageBox.Show(
                    ex.Detail.Code + ": " + ex.Detail.Message,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                logger.Error("Communication error starting match from lobby.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                logger.Error("Unexpected error starting match from lobby.", ex);

                MessageBox.Show(
                    ERROR_START_MATCH_GENERIC,
                    Lang.lobbyTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (btnStart != null)
                {
                    btnStart.IsEnabled = true;
                }
            }
        }

        internal void OpenProfileDialog()
        {
            string token = SessionTokenProvider.GetTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            try
            {
                var page = new ModifyProfilePage(
                    AppServices.Lobby.RawClient,
                    new AuthServiceClient(AUTH_ENDPOINT_CONFIGURATION_NAME),
                    token)
                {
                    Title = Lang.profileTitle
                };

                var profileWindow = BuildPageDialog(
                    page,
                    Lang.profileTitle,
                    PROFILE_WINDOW_WIDTH,
                    PROFILE_WINDOW_HEIGHT,
                    false);

                profileWindow.ShowDialog();
                profileController.RefreshAvatar();
            }
            catch (FaultException<AuthService.ServiceFault> ex)
            {
                logger.Warn("Auth fault while opening ModifyProfilePage from lobby.", ex);

                MessageBox.Show(
                    ex.Detail.Code + ": " + ex.Detail.Message,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                logger.Error("Communication error while opening ModifyProfilePage from lobby.", ex);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                logger.Error("Unexpected error while opening ModifyProfilePage from lobby.", ex);

                MessageBox.Show(
                    Lang.UiGenericError,
                    Lang.profileTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnMatchStartedFromHub(MatchInfo match)
        {
            try
            {
                if (match == null)
                {
                    return;
                }

                ui.Ui(() =>
                {
                    if (state.IsOpeningMatchWindow)
                    {
                        logger.Warn("Match already opening, ignoring MatchStarted.");
                        return;
                    }

                    state.IsOpeningMatchWindow = true;

                    try
                    {
                        var session = LoginWindow.AppSession.CurrentToken;

                        string token = session != null ? (session.Token ?? string.Empty) : string.Empty;
                        int myUserId = session != null ? session.UserId : 0;

                        const bool IS_HOST_DEFAULT = false;

                        var matchWindow = new MatchWindow(match, token, myUserId, IS_HOST_DEFAULT, (LobbyWindow)lobbyWindow);

                        matchWindow.Closed += (_, __) =>
                        {
                            ui.Ui(() =>
                            {
                                try
                                {
                                    lobbyWindow.Show();
                                    lobbyWindow.Activate();
                                }
                                catch (Exception ex)
                                {
                                    logger.Warn("Error restoring lobby after match closed.", ex);
                                }
                                finally
                                {
                                    state.IsOpeningMatchWindow = false;
                                }
                            });
                        };

                        lobbyWindow.Hide();
                        matchWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error opening MatchWindow from lobby.", ex);

                        try
                        {
                            lobbyWindow.Show();
                            lobbyWindow.Activate();
                        }
                        catch (Exception showEx)
                        {
                            logger.Warn("Error restoring lobby after MatchWindow open failure.", showEx);
                        }
                        finally
                        {
                            state.IsOpeningMatchWindow = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error("OnMatchStartedFromHub error.", ex);
            }
        }

        private Window BuildPageDialog(Page page, string title, double width, double height, bool isNoResize)
        {
            var frame = new Frame
            {
                Content = page,
                NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden
            };

            return new Window
            {
                Title = title,
                Content = frame,
                Width = width,
                Height = height,
                Owner = lobbyWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = isNoResize ? ResizeMode.NoResize : ResizeMode.CanResize
            };
        }

        public void Dispose()
        {
            AppServices.Lobby.MatchStarted -= OnMatchStartedFromHub;
        }
    }
}
