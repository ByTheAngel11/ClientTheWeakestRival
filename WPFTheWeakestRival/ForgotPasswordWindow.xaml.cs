using log4net;
using System;
using System.ComponentModel;
using System.Globalization;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Helpers;

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

        private const string MSG_SEND_CODE_NETWORK_ERROR = "Error de red/servicio al enviar el código.";
        private const string MSG_SEND_CODE_UNEXPECTED_ERROR = "Ocurrió un error al enviar el código de recuperación.";

        private const string MSG_RESET_PASSWORD_NETWORK_ERROR = "Error de red/servicio al restablecer la contraseña.";
        private const string MSG_RESET_PASSWORD_UNEXPECTED_ERROR = "Ocurrió un error al restablecer la contraseña.";

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

            this.email = GetTrimmedEmail(email);
            txtEmail.Text = this.email;

            this.resendCooldownSeconds = resendCooldownSeconds > 0
                ? resendCooldownSeconds
                : DEFAULT_RESEND_COOLDOWN_SECONDS;

            txtCode.Focus();

            UiValidationHelper.ApplyMaxLength(txtEmail, UiValidationHelper.EMAIL_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(txtCode, UiValidationHelper.CODE_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(pwdNewPassword, UiValidationHelper.PASSWORD_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(pwdConfirmPassword, UiValidationHelper.PASSWORD_MAX_LENGTH);

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
            string currentEmail = GetTrimmedEmail(txtEmail.Text);
            if (!EnsureEmailForSend(currentEmail, isInitial))
            {
                return;
            }

            btnResend.IsEnabled = false;

            AuthServiceClient authClient = new AuthServiceClient();
            try
            {
                BeginPasswordResetResponse response = await BeginPasswordResetAsync(authClient, currentEmail);
                ApplyBeginResetSuccess(response, isInitial);
            }
            catch (FaultException<ServiceFault> ex)
            {
                authClient.Abort();

                HandleBeginResetFault(
                    ex,
                    new BeginResetFaultUiHandlers(
                        resendCooldownSeconds,
                        StartResendCooldown,
                        () => EnableResendButton(this)));
            }
            catch (TimeoutException ex)
            {
                authClient.Abort();
                HandleBeginResetConnectivityError(
                    "Timeout al enviar el código de recuperación.",
                    ex,
                    () => EnableResendButton(this));
            }
            catch (CommunicationException ex)
            {
                authClient.Abort();
                HandleBeginResetConnectivityError(
                    "Error de comunicación al enviar el código de recuperación.",
                    ex,
                    () => EnableResendButton(this));
            }
            catch (Exception ex)
            {
                authClient.Abort();

                Logger.Error("Error inesperado al enviar el código de recuperación.", ex);

                MessageBox.Show(
                    MSG_SEND_CODE_UNEXPECTED_ERROR,
                    WINDOW_TITLE_PASSWORD_RESET,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                EnableResendButton(this);
            }
            finally
            {
                CloseOrAbortSafely(authClient);
            }
        }

        private static string GetTrimmedEmail(string rawEmail)
        {
            return (rawEmail ?? string.Empty).Trim();
        }

        private static bool EnsureEmailForSend(string currentEmail, bool isInitial)
        {
            if (!string.IsNullOrWhiteSpace(currentEmail))
            {
                return true;
            }

            if (!isInitial)
            {
                ShowWarning(MSG_EMAIL_REQUIRED_RESEND);
            }

            return false;
        }

        private static void EnableResendButton(ForgotPasswordWindow window)
        {
            if (window?.btnResend != null)
            {
                window.btnResend.IsEnabled = true;
            }
        }

        private static Task<BeginPasswordResetResponse> BeginPasswordResetAsync(
            AuthServiceClient authClient,
            string currentEmail)
        {
            var request = new BeginPasswordResetRequest
            {
                Email = currentEmail
            };

            return authClient.BeginPasswordResetAsync(request);
        }

        private void ApplyBeginResetSuccess(BeginPasswordResetResponse response, bool isInitial)
        {
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

        private static void HandleBeginResetFault(
            FaultException<ServiceFault> ex,
            BeginResetFaultUiHandlers uiHandlers)
        {
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
                int seconds = uiHandlers != null ? uiHandlers.CooldownSeconds : 0;
                uiHandlers?.StartCooldown?.Invoke(seconds);
                return;
            }

            uiHandlers?.EnableResend?.Invoke();
        }

        private static void HandleBeginResetConnectivityError(
            string logMessage,
            Exception ex,
            Action enableResendAction)
        {
            Logger.Error(logMessage, ex);

            MessageBox.Show(
                MSG_SEND_CODE_NETWORK_ERROR,
                WINDOW_TITLE_PASSWORD_RESET,
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            enableResendAction?.Invoke();
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
                ResetFields fields = ReadResetFieldsFromUi(this);
                ResetPasswordInput input = ValidateResetInputs(fields);

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

        private static ResetFields ReadResetFieldsFromUi(ForgotPasswordWindow window)
        {
            if (window == null)
            {
                return new ResetFields(string.Empty, string.Empty, string.Empty, string.Empty);
            }

            return new ResetFields(
                GetTrimmedEmail(window.txtEmail.Text),
                UiValidationHelper.TrimOrEmpty(window.txtCode.Text),
                window.pwdNewPassword.Password ?? string.Empty,
                window.pwdConfirmPassword.Password ?? string.Empty);
        }

        private static ResetPasswordInput ValidateResetInputs(ResetFields fields)
        {
            if (fields == null)
            {
                return ResetPasswordInput.Invalid(MSG_EMAIL_AND_CODE_REQUIRED);
            }

            if (string.IsNullOrWhiteSpace(fields.Email) || string.IsNullOrWhiteSpace(fields.Code))
            {
                return ResetPasswordInput.Invalid(MSG_EMAIL_AND_CODE_REQUIRED);
            }

            if (string.IsNullOrWhiteSpace(fields.NewPassword) || string.IsNullOrWhiteSpace(fields.ConfirmPassword))
            {
                return ResetPasswordInput.Invalid(MSG_PASSWORDS_REQUIRED);
            }

            if (!string.Equals(fields.NewPassword, fields.ConfirmPassword, StringComparison.Ordinal))
            {
                return ResetPasswordInput.Invalid(MSG_PASSWORDS_MISMATCH);
            }

            if (fields.NewPassword.Length < MIN_PASSWORD_LENGTH)
            {
                return ResetPasswordInput.Invalid(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        MSG_PASSWORD_MIN_LENGTH_FORMAT,
                        MIN_PASSWORD_LENGTH));
            }

            return ResetPasswordInput.Valid(fields.Email, fields.Code, fields.NewPassword);
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

                HandleResetFault(
                    ex,
                    new ResetFaultUiHandlers(
                        () => FocusInvalidCode(this),
                        () => FocusExpiredCode(this)));

                return false;
            }
            catch (TimeoutException ex)
            {
                authClient.Abort();

                Logger.Error("Timeout al restablecer la contraseña.", ex);

                MessageBox.Show(
                    MSG_RESET_PASSWORD_NETWORK_ERROR,
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
                    MSG_RESET_PASSWORD_NETWORK_ERROR,
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
                    MSG_RESET_PASSWORD_UNEXPECTED_ERROR,
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

        private static void FocusInvalidCode(ForgotPasswordWindow window)
        {
            if (window?.txtCode == null)
            {
                return;
            }

            window.txtCode.SelectAll();
            window.txtCode.Focus();
        }

        private static void FocusExpiredCode(ForgotPasswordWindow window)
        {
            if (window == null)
            {
                return;
            }

            window.txtCode?.Clear();
            window.btnResend?.Focus();
        }

        private static void HandleResetFault(
            FaultException<ServiceFault> ex,
            ResetFaultUiHandlers uiHandlers)
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
                uiHandlers?.OnInvalidCode?.Invoke();
                return;
            }

            if (string.Equals(faultCode, FAULT_CODE_EXPIRED, StringComparison.OrdinalIgnoreCase))
            {
                uiHandlers?.OnExpiredCode?.Invoke();
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
                Logger.Warn("CloseOrAbortSafely timeout.", ex);
                client.Abort();
            }
            catch (CommunicationException ex)
            {
                Logger.Warn("CloseOrAbortSafely communication error.", ex);
                client.Abort();
            }
            catch (Exception ex)
            {
                Logger.Error("CloseOrAbortSafely unexpected error.", ex);
                client.Abort();
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

        private sealed class ResetFields
        {
            public ResetFields(string email, string code, string newPassword, string confirmPassword)
            {
                Email = email ?? string.Empty;
                Code = code ?? string.Empty;
                NewPassword = newPassword ?? string.Empty;
                ConfirmPassword = confirmPassword ?? string.Empty;
            }

            public string Email { get; }
            public string Code { get; }
            public string NewPassword { get; }
            public string ConfirmPassword { get; }
        }

        private sealed class BeginResetFaultUiHandlers
        {
            public BeginResetFaultUiHandlers(int cooldownSeconds, Action<int> startCooldown, Action enableResend)
            {
                CooldownSeconds = cooldownSeconds;
                StartCooldown = startCooldown;
                EnableResend = enableResend;
            }

            public int CooldownSeconds { get; }
            public Action<int> StartCooldown { get; }
            public Action EnableResend { get; }
        }

        private sealed class ResetFaultUiHandlers
        {
            public ResetFaultUiHandlers(Action onInvalidCode, Action onExpiredCode)
            {
                OnInvalidCode = onInvalidCode;
                OnExpiredCode = onExpiredCode;
            }

            public Action OnInvalidCode { get; }
            public Action OnExpiredCode { get; }
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
