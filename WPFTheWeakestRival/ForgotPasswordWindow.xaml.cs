using System;
using System.ComponentModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WPFTheWeakestRival.AuthService;

namespace WPFTheWeakestRival
{
    public partial class ForgotPasswordWindow : Window
    {
        private const int DEFAULT_RESEND_COOLDOWN_SECONDS = 60;
        private const int MIN_PASSWORD_LENGTH = 8;

        private readonly string email;

        private int resendCooldownSeconds;
        private DispatcherTimer cooldownTimer;
        private int secondsRemaining;
        private bool isNavigationHandled;

        public ForgotPasswordWindow(string email, int resendCooldownSeconds)
        {
            InitializeComponent();

            this.email = (email ?? string.Empty).Trim();
            txtEmail.Text = this.email;

            this.resendCooldownSeconds = resendCooldownSeconds > 0
                ? resendCooldownSeconds
                : DEFAULT_RESEND_COOLDOWN_SECONDS;

            txtCode.Focus();

            Loaded += ForgotPasswordWindow_Loaded;
            Closing += ForgotPasswordWindow_Closing;
        }

        private async void ForgotPasswordWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await SendPasswordResetCodeAsync(isInitial: true);
        }

        private void ForgotPasswordWindow_Closing(object sender, CancelEventArgs e)
        {
            if (isNavigationHandled)
            {
                return;
            }

            if (Owner != null)
            {
                Owner.Show();
            }
            else
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
            }

            isNavigationHandled = true;
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

            cooldownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
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

        private async Task SendPasswordResetCodeAsync(bool isInitial)
        {
            string currentEmail = (txtEmail.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(currentEmail))
            {
                if (!isInitial)
                {
                    MessageBox.Show(
                        "Debes ingresar tu correo para reenviar el código.",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                return;
            }

            btnResend.IsEnabled = false;

            var authClient = new AuthServiceClient();

            try
            {
                var request = new BeginPasswordResetRequest
                {
                    Email = currentEmail
                };

                BeginPasswordResetResponse response =
                    await authClient.BeginPasswordResetAsync(request);

                if (authClient.State != CommunicationState.Faulted)
                {
                    authClient.Close();
                }
                else
                {
                    authClient.Abort();
                }

                if (response != null && response.ResendAfterSeconds > 0)
                {
                    resendCooldownSeconds = response.ResendAfterSeconds;
                }

                StartResendCooldown(resendCooldownSeconds);
                txtCode.Clear();
                txtCode.Focus();

                string message = isInitial
                    ? "Se envió un código de recuperación a tu correo."
                    : "Se envió un nuevo código de recuperación a tu correo.";

                MessageBox.Show(
                    message,
                    "Recuperar contraseña",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (FaultException<ServiceFault> fx)
            {
                authClient.Abort();

                MessageBox.Show(
                    $"{fx.Detail.Code}: {fx.Detail.Message}",
                    "Recuperar contraseña",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (string.Equals(fx.Detail.Code, "TOO_SOON", StringComparison.OrdinalIgnoreCase))
                {
                    StartResendCooldown(resendCooldownSeconds);
                }
                else
                {
                    btnResend.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                authClient.Abort();

                MessageBox.Show(
                    $"Error de red/servicio al enviar el código: {ex.Message}",
                    "Recuperar contraseña",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnResend.IsEnabled = true;
            }
        }

        private async void ResendClick(object sender, RoutedEventArgs e)
        {
            await SendPasswordResetCodeAsync(isInitial: false);
        }

        private async void ConfirmClick(object sender, RoutedEventArgs e)
        {
            btnConfirm.IsEnabled = false;

            try
            {
                string currentEmail = (txtEmail.Text ?? string.Empty).Trim();
                string code = (txtCode.Text ?? string.Empty).Trim();
                string newPassword = pwdNewPassword.Password ?? string.Empty;
                string confirmPassword = pwdConfirmPassword.Password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(currentEmail) || string.IsNullOrWhiteSpace(code))
                {
                    MessageBox.Show(
                        "Debes ingresar tu correo y el código de recuperación.",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    btnConfirm.IsEnabled = true;
                    return;
                }

                if (string.IsNullOrWhiteSpace(newPassword) ||
                    string.IsNullOrWhiteSpace(confirmPassword))
                {
                    MessageBox.Show(
                        "Debes ingresar y confirmar la nueva contraseña.",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    btnConfirm.IsEnabled = true;
                    return;
                }

                if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "La nueva contraseña y su confirmación no coinciden.",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    btnConfirm.IsEnabled = true;
                    return;
                }

                if (newPassword.Length < MIN_PASSWORD_LENGTH)
                {
                    MessageBox.Show(
                        $"La contraseña debe tener al menos {MIN_PASSWORD_LENGTH} caracteres.",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    btnConfirm.IsEnabled = true;
                    return;
                }

                var request = new CompletePasswordResetRequest
                {
                    Email = currentEmail,
                    Code = code,
                    NewPassword = newPassword
                };

                var authClient = new AuthServiceClient();

                try
                {
                    await authClient.CompletePasswordResetAsync(request);

                    if (authClient.State != CommunicationState.Faulted)
                    {
                        authClient.Close();
                    }
                    else
                    {
                        authClient.Abort();
                    }

                    MessageBox.Show(
                        "Tu contraseña ha sido restablecida correctamente.\n\n" +
                        "Para continuar, debes ganar una mano de blackjack.",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    var blackjackWindow = new BlackjackWindow
                    {
                        Owner = this
                    };

                    bool? blackjackResult = blackjackWindow.ShowDialog();

                    if (blackjackResult == true)
                    {
                        isNavigationHandled = true;

                        var loginWindow = new LoginWindow();
                        loginWindow.Show();

                        if (Owner != null)
                        {
                            Owner.Close();
                        }

                        Close();
                    }
                }
                catch (FaultException<ServiceFault> fx)
                {
                    authClient.Abort();

                    MessageBox.Show(
                        $"{fx.Detail.Code}: {fx.Detail.Message}",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

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

                    MessageBox.Show(
                        $"Error al restablecer la contraseña: {ex.Message}",
                        "Recuperar contraseña",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            finally
            {
                btnConfirm.IsEnabled = true;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            if (cooldownTimer != null)
            {
                cooldownTimer.Stop();
                cooldownTimer.Tick -= OnCooldownTick;
            }

            isNavigationHandled = true;

            if (Owner != null)
            {
                Owner.Show();
            }
            else
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
            }

            Close();
        }
    }
}
