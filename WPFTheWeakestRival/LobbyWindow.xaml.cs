using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WPFTheWeakestRival.LobbyService;
using static WPFTheWeakestRival.LoginWindow;

namespace WPFTheWeakestRival
{
    public partial class LobbyWindow : Window, ILobbyServiceCallback
    {
        private LobbyServiceClient _lobbyClient;
        private readonly BlurEffect _blur = new BlurEffect { Radius = 0 };

        public LobbyWindow()
        {
            InitializeComponent();
            var ctx = new InstanceContext(this);
            _lobbyClient = new LobbyServiceClient(ctx, "WSDualHttpBinding_ILobbyService");
        }

        private void btnModifyProfile_Click(object sender, RoutedEventArgs e)
        {
            var token = AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Sesión no válida.");
                return;
            }
            var page = new ModifyProfilePage(_lobbyClient, token) { Title = "Modificar Perfil" };
            ShowOverlay(page);
        }

        private void ShowOverlay(System.Windows.Controls.Page page)
        {
            OverlayFrame.Content = page;
            MainArea.Effect = _blur;
            OverlayHost.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new QuadraticEase()
            };
            OverlayHost.BeginAnimation(OpacityProperty, fadeIn);

            var blurAnim = new DoubleAnimation
            {
                From = 0,
                To = 6,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new QuadraticEase()
            };
            _blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
        }

        private void HideOverlay()
        {
            var fadeOut = new DoubleAnimation
            {
                From = OverlayHost.Opacity,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                EasingFunction = new QuadraticEase()
            };
            fadeOut.Completed += (_, __) =>
            {
                OverlayHost.Visibility = Visibility.Collapsed;
                OverlayFrame.Content = null;
                MainArea.Effect = null;
            };
            OverlayHost.BeginAnimation(OpacityProperty, fadeOut);

            if (MainArea.Effect is BlurEffect be)
            {
                var blurAnim = new DoubleAnimation
                {
                    From = be.Radius,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                    EasingFunction = new QuadraticEase()
                };
                be.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }
        }

        private void CloseOverlay_Click(object sender, RoutedEventArgs e) => HideOverlay();

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && OverlayHost.Visibility == Visibility.Visible)
                HideOverlay();
        }

        public void OnLobbyUpdated(LobbyInfo lobby) { }
        public void OnPlayerJoined(PlayerSummary player) { }
        public void OnPlayerLeft(Guid playerId) { }
        public void OnChatMessageReceived(ChatMessage message) { }
    }
}
