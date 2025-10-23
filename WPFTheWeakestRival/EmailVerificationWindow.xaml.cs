using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Threading;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class EmailVerificationWindow : Window
    {
        private readonly string displayName;
        private readonly string password;
        private readonly string profileImagePath;

        private int resendCooldownSeconds;
        private DispatcherTimer cooldownTimer;
        private int secondsRemaining;

        public EmailVerificationWindow(string email, string displayName, string password, string profileImagePath, int resendCooldownSeconds)
        {
            InitializeComponent();
            txtEmail.Text = email ?? string.Empty;

            this.displayName = displayName ?? string.Empty;
            this.password = password ?? string.Empty;
            this.profileImagePath = profileImagePath ?? string.Empty;
            this.resendCooldownSeconds = resendCooldownSeconds > 0 ? resendCooldownSeconds : 60;

            txtCode.Focus();
            StartResendCooldown(this.resendCooldownSeconds);
        }

        private void StartResendCooldown(int seconds)
        {
            secondsRemaining = seconds;
            btnResend.IsEnabled = false;
            lblCooldown.Text = $"Puedes reenviar en {secondsRemaining}s";

            if (cooldownTimer != null)
            {
                cooldownTimer.Stop();
                cooldownTimer.Tick -= OnCooldownTick;
            }

            cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            cooldownTimer.Tick += OnCooldownTick;
            cooldownTimer.Start();
        }

        private void OnCooldownTick(object sender, EventArgs e)
        {
            secondsRemaining--;

            if (secondsRemaining <= 0)
            {
                cooldownTimer.Stop();
                lblCooldown.Text = string.Empty;
                btnResend.IsEnabled = true;
            }
            else
            {
                lblCooldown.Text = $"Puedes reenviar en {secondsRemaining}s";
            }
        }

        private async void ResendClick(object sender, RoutedEventArgs e)
        {
            btnResend.IsEnabled = false;

            var authClient = new AuthServiceClient();
            try
            {
                BeginRegisterResponse response = await authClient.BeginRegisterAsync(new BeginRegisterRequest
                {
                    Email = (txtEmail.Text ?? string.Empty).Trim()
                });

                if (authClient.State != CommunicationState.Faulted) authClient.Close(); else authClient.Abort();

                if (response != null && response.ResendAfterSeconds > 0)
                {
                    resendCooldownSeconds = response.ResendAfterSeconds;
                }

                StartResendCooldown(resendCooldownSeconds);
                txtCode.Clear();
                txtCode.Focus();

                MessageBox.Show("Se envió un nuevo código a tu correo.", "Auth", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (FaultException<ServiceFault> fx)
            {
                authClient.Abort();
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Auth", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (string.Equals(fx.Detail.Code, "TOO_SOON", StringComparison.OrdinalIgnoreCase))
                {
                    StartResendCooldown(resendCooldownSeconds);
                }
            }
            catch (Exception ex)
            {
                authClient.Abort();
                MessageBox.Show($"Error de red/servicio: {ex.Message}", "Auth", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ConfirmClick(object sender, RoutedEventArgs e)
        {
            btnConfirm.IsEnabled = false;

            try
            {
                string email = (txtEmail.Text ?? string.Empty).Trim();
                string code = (txtCode.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                {
                    MessageBox.Show(Lang.errorInvalidEmail, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var request = new CompleteRegisterRequest
                {
                    Email = email,
                    Code = code,
                    Password = password,
                    DisplayName = displayName,
                    ProfileImageUrl = profileImagePath
                };

                var authClient = new AuthServiceClient();
                try
                {
                    RegisterResponse response = await authClient.CompleteRegisterAsync(request);
                    if (authClient.State != CommunicationState.Faulted) authClient.Close(); else authClient.Abort();

                    MessageBox.Show($"{Lang.registSucces}\nUserId: {response.UserId}\nExp: {response.Token.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC",
                        "Auth", MessageBoxButton.OK, MessageBoxImage.Information);

                    var loginWindow = new LoginWindow();
                    loginWindow.Show();

                    if (Owner != null) Owner.Close();
                    Close();
                }
                catch (FaultException<ServiceFault> fx)
                {
                    authClient.Abort();
                    MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Auth", MessageBoxButton.OK, MessageBoxImage.Warning);

                    if (string.Equals(fx.Detail.Code, "CODE_INVALID", StringComparison.OrdinalIgnoreCase))
                    {
                        txtCode.SelectAll();
                        txtCode.Focus();
                    }
                    else if (string.Equals(fx.Detail.Code, "CODE_EXPIRED", StringComparison.OrdinalIgnoreCase))
                    {
                        txtCode.Clear();
                        btnResend.Focus();
                    }
                }
                catch (Exception ex)
                {
                    authClient.Abort();
                    MessageBox.Show($"Error de red/servicio: {ex.Message}", "Auth", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                btnConfirm.IsEnabled = true;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            if (Owner != null)
            {
                Owner.Show();
            }

            Close();
        }
    }
}
