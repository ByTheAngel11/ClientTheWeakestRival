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
        private readonly string _displayName;
        private readonly string _password;
        private readonly string _profileImagePath;
        private int _resendCooldown;
        private DispatcherTimer _timer;
        private int _secondsLeft;

        public EmailVerificationWindow(string email, string displayName, string password, string profileImagePath, int resendCooldownSeconds)
        {
            InitializeComponent();
            txtEmail.Text = email;
            _displayName = displayName;
            _password = password;
            _profileImagePath = profileImagePath;
            _resendCooldown = (resendCooldownSeconds > 0) ? resendCooldownSeconds : 60;

            txtCode.Focus();
            StartCooldown(_resendCooldown);
        }

        private void StartCooldown(int seconds)
        {
            _secondsLeft = seconds;
            btnResend.IsEnabled = false;
            lblCooldown.Text = $"Puedes reenviar en {_secondsLeft}s";

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _secondsLeft--;
            if (_secondsLeft <= 0)
            {
                _timer.Stop();
                lblCooldown.Text = "";
                btnResend.IsEnabled = true;
            }
            else
            {
                lblCooldown.Text = $"Puedes reenviar en {_secondsLeft}s";
            }
        }

        private async void ResendClick(object sender, RoutedEventArgs e)
        {
            btnResend.IsEnabled = false;
            try
            {
                var client = new AuthServiceClient();
                BeginRegisterResponse resp;
                try
                {
                    resp = await client.BeginRegisterAsync(new BeginRegisterRequest { Email = txtEmail.Text?.Trim() });
                    if (client.State != CommunicationState.Faulted) client.Close(); else client.Abort();
                }
                catch (FaultException<ServiceFault> fx)
                {
                    client.Abort();
                    MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Auth", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Si el server dijo TOO_SOON, vuelve a iniciar el cooldown
                    if (string.Equals(fx.Detail.Code, "TOO_SOON", StringComparison.OrdinalIgnoreCase))
                        StartCooldown(_resendCooldown);
                    return;
                }
                catch (Exception ex)
                {
                    client.Abort();
                    MessageBox.Show("Error de red/servicio: " + ex.Message, "Auth", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Usa el cooldown que devolvió el server si viene distinto
                if (resp != null && resp.ResendAfterSeconds > 0)
                    _resendCooldown = resp.ResendAfterSeconds;

                StartCooldown(_resendCooldown);
                txtCode.Clear();
                txtCode.Focus();

                MessageBox.Show("Se envió un nuevo código a tu correo.", "Auth", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                // se re-habilita cuando termine el cooldown
            }
        }

        private async void ConfirmClick(object sender, RoutedEventArgs e)
        {
            btnConfirm.IsEnabled = false;
            try
            {
                string email = txtEmail.Text?.Trim();
                string code = txtCode.Text?.Trim();

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                {
                    MessageBox.Show(Lang.errorInvalidEmail, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var req = new CompleteRegisterRequest
                {
                    Email = email,
                    Code = code,
                    Password = _password,
                    DisplayName = _displayName,
                    ProfileImageUrl = _profileImagePath
                };

                var client = new AuthServiceClient();
                RegisterResponse resp;
                try
                {
                    resp = await client.CompleteRegisterAsync(req);
                    if (client.State != CommunicationState.Faulted) client.Close(); else client.Abort();
                }
                catch (FaultException<ServiceFault> fx)
                {
                    client.Abort();
                    MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Auth", MessageBoxButton.OK, MessageBoxImage.Warning);

                    if (string.Equals(fx.Detail.Code, "CODE_INVALID", StringComparison.OrdinalIgnoreCase))
                    {
                        txtCode.SelectAll();
                        txtCode.Focus();
                    }
                    if (string.Equals(fx.Detail.Code, "CODE_EXPIRED", StringComparison.OrdinalIgnoreCase))
                    {
                        // guía al usuario a reenviar
                        txtCode.Clear();
                        btnResend.Focus();
                    }
                    return;
                }
                catch (Exception ex)
                {
                    client.Abort();
                    MessageBox.Show("Error de red/servicio: " + ex.Message, "Auth", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show($"{Lang.registSucces}\nUserId: {resp.UserId}\nExp: {resp.Token.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC",
                    "Auth", MessageBoxButton.OK, MessageBoxImage.Information);

                // Volver a login (mismo comportamiento que tenías)
                var login = new LoginWindow();
                login.Show();

                // cierra RegistrationWindow (owner) y esta ventana
                if (Owner != null) Owner.Close();
                Close();
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
