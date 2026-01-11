using log4net;
using System;
using System.ComponentModel;
using System.Globalization;
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
        private const int COOLDOWN_TICK_SECONDS = 1;

        private const int MIN_PASSWORD_LENGTH = 8;

        private const string WINDOW_TITLE_PASSWORD_RESET = "Recuperar contraseña";

        private const string MSG_EMAIL_REQUIRED_RESEND = "Debes ingresar tu correo para reenviar el código.";
        private const string MSG_EMAIL_AND_CODE_REQUIRED = "Debes ingresar tu correo y el código de recuperación.";
        private const string MSG_PASSWORDS_REQUIRED = "Debes ingresar y confirmar la nueva contraseña.";
        private const string MSG_PASSWORDS_MISMATCH = "La nueva contraseña y su confirmación no coinciden.";
        private const string MSG_PASSWORD_MIN_LENGTH_FORMAT = "La contraseña debe tener al menos {0} caracteres.";

        private const string MSG_CODE_SENT_INITIAL = "Se envió un código de recuperación a tu correo.";
        private const string MSG_CODE_SENT_RESEND = "Se envió un nuevo código de recuperación a tu correo.";

        private const string MSG_RESET_SUCCESS =
            "Tu contraseña ha sido restablecida correctamente.\n\n" +
            "Para continuar, debes ganar una mano de blackjack.";

        private const string FAULT_TOO_SOON = "TOO_SOON";
        private const string FAULT_CODE_INVALID = "CODE_INVALID";
        private const string FAULT_CODE_EXPIRED = "CODE_EXPIRED";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(ForgotPasswordWindow));

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
            StopCooldownTimer();

            if (isNavigationHandled)
            {
                return;
            }

            ShowOwnerOrLoginWindow();
            isNavigationHandled = true;
        }

        private void StartResendCooldown(int seconds)
        {
            secondsRemaining = seconds;

            btnResend.IsEnabled = false;
            lblCooldown.Text = BuildCooldownText(secondsRemaining);

            StopCooldownTimer();

            cooldownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(COOLDOWN_TICK_SECONDS)
            };

            cooldownTimer.Tick += OnCooldownTick;
            cooldownTimer.Start();
        }

        private void OnCooldownTick(object sender, EventArgs e)
        {
            secondsRemaining--;

            if (secondsRemaining <= 0)
            {
                StopCooldownTimer();
                lblCooldown.Text = string.Empty;
                btnResend.IsEnabled = true;
                return;
            }

            lblCooldown.Text = BuildCooldownText(secondsRemaining);
        }

        private static string BuildCooldownText(int seconds)
        {
            return string.Format(CultureInfo.CurrentCulture, "Puedes reenviar en {0}s", seconds);
        }

        private void StopCooldownTimer()
        {
            if (cooldownTimer == null)
            {
                return;
            }

            try
            {
                cooldownTimer.Stop();
                cooldownTimer.Tick -= OnCooldownTick;
            }
            catch (Exception ex)
            {
                Logger.Warn("StopCooldownTimer error.", ex);
            }
            finally
            {
                cooldownTimer = null;
            }
        }

        private async Task SendPasswordResetCodeAsync(bool isInitial)
        {
            string currentEmail = (txtEmail.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(currentEmail))
            {
                if (!isInitial)
                {
                    ShowWarning(MSG_EMAIL_REQUIRED_RESEND);
                }

                return;
            }

            btnResend.IsEnabled = false;

            AuthServiceClient authClient = new AuthServiceClient();

            try
            {
                var request = new BeginPasswordResetRequest
                {
                    Email = currentEmail
                };

                BeginPasswordResetResponse response = await authClient.BeginPasswordResetAsync(request);

                int resendAfterSeconds = response != null ? response.ResendAfterSeconds : 0;
                if (resendAfterSeconds > 0)
                {
                    resendCooldownSeconds = resendAfterSeconds;
                }

                StartResendCooldown(resendCooldownSeconds);

                txtCode.Clear();
                txtCode.Focus();

                string message = isInitial ? MSG_CODE_SENT_INITIAL : MSG_CODE_SENT_RESEND;
                ShowInfo(message);
            }
            catch (FaultException<ServiceFault> ex)
            {
                authClient.Abort();

                string faultCode = ex.Detail != null ? ex.Detail.Code : string.Empty;
                string faultMessage = ex.Detail != null ? ex.Detail.Message : ex.Message;

                Logger.Warn("Fault al obtener/enviar código de recuperación.", ex);

                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, "{0}: {1}", faultCode, faultMessage),
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (string.Equals(faultCode, FAULT_TOO_SOON, StringComparison.OrdinalIgnoreCase))
                {
                    StartResendCooldown(resendCooldownSeconds);
                }
                else
                {
                    btnResend.IsEnabled = true;
                }
            }
            catch (TimeoutException ex)
            {
                authClient.Abort();

                Logger.Error("Timeout al enviar el código de recuperación.", ex);

                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Error de red/servicio al enviar el código: {0}",
                        ex.Message),
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnResend.IsEnabled = true;
            }
            catch (CommunicationException ex)
            {
                authClient.Abort();

                Logger.Error("Error de comunicación al enviar el código de recuperación.", ex);

                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Error de red/servicio al enviar el código: {0}",
                        ex.Message),
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnResend.IsEnabled = true;
            }
            catch (Exception ex)
            {
                authClient.Abort();

                Logger.Error("Error inesperado al enviar el código de recuperación.", ex);

                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Error de red/servicio al enviar el código: {0}",
                        ex.Message),
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnResend.IsEnabled = true;
            }
            finally
            {
                CloseOrAbortSafely(authClient);
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
                ResetPasswordInput input = ValidateResetInputs();
                if (!input.IsValid)
                {
                    ShowWarning(input.ValidationMessage);
                    return;
                }

                var request = new CompletePasswordResetRequest
                {
                    Email = input.Email,
                    Code = input.Code,
                    NewPassword = input.NewPassword
                };

                bool resetCompleted = await TryCompletePasswordResetAsync(request);
                if (!resetCompleted)
                {
                    return;
                }

                ShowInfo(MSG_RESET_SUCCESS);

                if (ShowBlackjackAndCheckWin())
                {
                    NavigateToLoginAndClose();
                }
            }
            finally
            {
                btnConfirm.IsEnabled = true;
            }
        }

        private ResetPasswordInput ValidateResetInputs()
        {
            string currentEmail = (txtEmail.Text ?? string.Empty).Trim();
            string code = (txtCode.Text ?? string.Empty).Trim();
            string newPassword = pwdNewPassword.Password ?? string.Empty;
            string confirmPassword = pwdConfirmPassword.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentEmail) || string.IsNullOrWhiteSpace(code))
            {
                return ResetPasswordInput.Invalid(MSG_EMAIL_AND_CODE_REQUIRED);
            }

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                return ResetPasswordInput.Invalid(MSG_PASSWORDS_REQUIRED);
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return ResetPasswordInput.Invalid(MSG_PASSWORDS_MISMATCH);
            }

            if (newPassword.Length < MIN_PASSWORD_LENGTH)
            {
                return ResetPasswordInput.Invalid(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        MSG_PASSWORD_MIN_LENGTH_FORMAT,
                        MIN_PASSWORD_LENGTH));
            }

            return ResetPasswordInput.Valid(currentEmail, code, newPassword);
        }

        private async Task<bool> TryCompletePasswordResetAsync(CompletePasswordResetRequest request)
        {
            AuthServiceClient authClient = new AuthServiceClient();

            try
            {
                await authClient.CompletePasswordResetAsync(request);
                return true;
            }
            catch (FaultException<ServiceFault> ex)
            {
                authClient.Abort();
                HandleResetFault(ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                authClient.Abort();

                Logger.Error("Timeout al restablecer la contraseña.", ex);

                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Error al restablecer la contraseña: {0}",
                        ex.Message),
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
            catch (CommunicationException ex)
            {
                authClient.Abort();

                Logger.Error("Error de comunicación al restablecer la contraseña.", ex);

                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Error al restablecer la contraseña: {0}",
                        ex.Message),
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
            catch (Exception ex)
            {
                authClient.Abort();

                Logger.Error("Error inesperado al restablecer la contraseña.", ex);

                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Error al restablecer la contraseña: {0}",
                        ex.Message),
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
            finally
            {
                CloseOrAbortSafely(authClient);
            }
        }

        private void HandleResetFault(FaultException<ServiceFault> ex)
        {
            string faultCode = ex.Detail != null ? ex.Detail.Code : string.Empty;
            string faultMessage = ex.Detail != null ? ex.Detail.Message : ex.Message;

            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, "{0}: {1}", faultCode, faultMessage),
                WINDOW_TITLE_PASSWORD_RESET,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            if (string.Equals(faultCode, FAULT_CODE_INVALID, StringComparison.OrdinalIgnoreCase))
            {
                txtCode.SelectAll();
                txtCode.Focus();
                return;
            }

            if (string.Equals(faultCode, FAULT_CODE_EXPIRED, StringComparison.OrdinalIgnoreCase))
            {
                txtCode.Clear();
                btnResend.Focus();
            }
        }

        private static void CloseOrAbortSafely(ICommunicationObject client)
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
            catch (TimeoutException ex)
            {
                LogManager.GetLogger(typeof(ForgotPasswordWindow)).Warn("CloseOrAbortSafely timeout.", ex);
                client.Abort();
            }
            catch (CommunicationException ex)
            {
                LogManager.GetLogger(typeof(ForgotPasswordWindow)).Warn("CloseOrAbortSafely communication error.", ex);
                client.Abort();
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(typeof(ForgotPasswordWindow)).Error("CloseOrAbortSafely unexpected error.", ex);
                client.Abort();
                throw;
            }
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(
                message,
                WINDOW_TITLE_PASSWORD_RESET,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private static void ShowInfo(string message)
        {
            MessageBox.Show(
                message,
                WINDOW_TITLE_PASSWORD_RESET,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private bool ShowBlackjackAndCheckWin()
        {
            var blackjackWindow = new BlackjackWindow
            {
                Owner = this
            };

            bool? blackjackResult = blackjackWindow.ShowDialog();
            return blackjackResult == true;
        }

        private void NavigateToLoginAndClose()
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

        private void ShowOwnerOrLoginWindow()
        {
            if (Owner != null)
            {
                Owner.Show();
                return;
            }

            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            StopCooldownTimer();

            isNavigationHandled = true;

            ShowOwnerOrLoginWindow();
            Close();
        }

        private sealed class ResetPasswordInput
        {
            private ResetPasswordInput(bool isValid, string email, string code, string newPassword, string validationMessage)
            {
                IsValid = isValid;
                Email = email;
                Code = code;
                NewPassword = newPassword;
                ValidationMessage = validationMessage;
            }

            public bool IsValid { get; }
            public string Email { get; }
            public string Code { get; }
            public string NewPassword { get; }
            public string ValidationMessage { get; }

            public static ResetPasswordInput Invalid(string message)
            {
                return new ResetPasswordInput(false, string.Empty, string.Empty, string.Empty, message);
            }

            public static ResetPasswordInput Valid(string email, string code, string newPassword)
            {
                return new ResetPasswordInput(true, email, code, newPassword, string.Empty);
            }
        }
    }
}
