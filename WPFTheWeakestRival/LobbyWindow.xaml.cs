using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival
{
    /// <summary>
    /// Main lobby window. Hosts overlay pages and handles lobby callbacks.
    /// </summary>
    public partial class LobbyWindow : Window, ILobbyServiceCallback
    {
        // ===================== Constants (no magic numbers) =====================
        private const double OverlayBlurMaxRadius = 6.0;
        private const int OverlayFadeInDurationMs = 180;
        private const int OverlayFadeOutDurationMs = 160;

        // ===================== State / Dependencies =====================
        private readonly LobbyServiceClient lobbyServiceClient;
        private readonly BlurEffect overlayBlurEffect = new BlurEffect { Radius = 0 };

        public LobbyWindow()
        {
            InitializeComponent();

            var serviceContext = new InstanceContext(this);
            lobbyServiceClient = new LobbyServiceClient(serviceContext, "WSDualHttpBinding_ILobbyService");

            Unloaded += OnWindowUnloaded;
        }

        // ========== Wrappers para mantener los nombres de handlers del XAML ==========
        private void btnModifyProfile_Click(object sender, RoutedEventArgs e) => OnModifyProfileClick(sender, e);
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) => OnCloseOverlayClick(sender, e);
        private void Window_KeyDown(object sender, KeyEventArgs e) => OnWindowKeyDown(sender, e);

        // ===================== Handlers con buen naming =====================

        private void OnModifyProfileClick(object sender, RoutedEventArgs e)
        {
            // AppSession/CurrentToken estático: sin null-conditional en AppSession
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }

            var modifyProfilePage = new ModifyProfilePage(lobbyServiceClient, token)
            {
                Title = "Modificar Perfil" // idealmente desde recursos
            };

            ShowOverlayPage(modifyProfilePage);
        }

        private void OnCloseOverlayClick(object sender, RoutedEventArgs e)
        {
            HideOverlayPanel();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && pnlOverlayHost.Visibility == Visibility.Visible)
            {
                HideOverlayPanel();
            }
        }

        // ===================== Overlay helpers =====================

        private void ShowOverlayPage(Page page)
        {
            frmOverlayFrame.Content = page;
            grdMainArea.Effect = overlayBlurEffect;
            pnlOverlayHost.Visibility = Visibility.Visible;

            var overlayFadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs),
                EasingFunction = new QuadraticEase()
            };
            pnlOverlayHost.BeginAnimation(OpacityProperty, overlayFadeInAnimation);

            var overlayBlurInAnimation = new DoubleAnimation
            {
                From = 0,
                To = OverlayBlurMaxRadius,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeInDurationMs),
                EasingFunction = new QuadraticEase()
            };
            overlayBlurEffect.BeginAnimation(BlurEffect.RadiusProperty, overlayBlurInAnimation);
        }

        private void HideOverlayPanel()
        {
            var overlayFadeOutAnimation = new DoubleAnimation
            {
                From = pnlOverlayHost.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs),
                EasingFunction = new QuadraticEase()
            };

            overlayFadeOutAnimation.Completed += (_, __) =>
            {
                pnlOverlayHost.Visibility = Visibility.Collapsed;
                frmOverlayFrame.Content = null;
                grdMainArea.Effect = null;
            };

            pnlOverlayHost.BeginAnimation(OpacityProperty, overlayFadeOutAnimation);

            if (grdMainArea.Effect is BlurEffect currentBlurEffect)
            {
                var overlayBlurOutAnimation = new DoubleAnimation
                {
                    From = currentBlurEffect.Radius,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(OverlayFadeOutDurationMs),
                    EasingFunction = new QuadraticEase()
                };
                currentBlurEffect.BeginAnimation(BlurEffect.RadiusProperty, overlayBlurOutAnimation);
            }
        }

        // ===================== Lobby callbacks (WCF) =====================

        /// <summary>Raised when lobby state changes.</summary>
        public void OnLobbyUpdated(LobbyInfo lobbyInfo)
        {
            // TODO: refresh UI if needed
        }

        /// <summary>Raised when a player joins.</summary>
        public void OnPlayerJoined(PlayerSummary playerSummary)
        {
            // TODO: update players list
        }

        /// <summary>Raised when a player leaves.</summary>
        public void OnPlayerLeft(Guid playerId)
        {
            // TODO: update players list
        }

        /// <summary>Raised when a chat message arrives.</summary>
        public void OnChatMessageReceived(ChatMessage chatMessage)
        {
            // TODO: append message to chat
        }

        // ===================== Cleanup =====================

        /// <summary>Closes the WCF client to release resources.</summary>
        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lobbyServiceClient.State == CommunicationState.Faulted)
                {
                    lobbyServiceClient.Abort();
                }
                else
                {
                    lobbyServiceClient.Close();
                }
            }
            catch
            {
                lobbyServiceClient.Abort();
            }
        }
    }
}
