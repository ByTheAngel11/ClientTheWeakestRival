using log4net;
using System;
using System.ServiceModel;
using System.Windows;
using WPFTheWeakestRival.AuthService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival
{
    public partial class EmailVerificationWindow : Window
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(EmailVerificationWindow));

        private readonly string email;
        private readonly string displayName;
        private readonly string password;

        private readonly byte[] profileImageBytes;
        private readonly string profileImageContentType;

        private readonly int resendCooldownSeconds;

        public EmailVerificationWindow(
            string email,
            string displayName,
            string password,
            byte[] profileImageBytes,
            string profileImageContentType,
            int resendCooldownSeconds)
        {
            InitializeComponent();

            this.email = email ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.password = password ?? string.Empty;

            this.profileImageBytes = profileImageBytes;
            this.profileImageContentType = profileImageContentType ?? string.Empty;

            this.resendCooldownSeconds = resendCooldownSeconds;
        }

        private async void ConfirmClick(object sender, RoutedEventArgs e)
        {
            btnConfirm.IsEnabled = false;

            var authClient = new AuthServiceClient();

            try
            {
                string code = txtCode.Text?.Trim() ?? string.Empty;

                await authClient.CompleteRegisterAsync(new CompleteRegisterRequest
                {
                    Email = email,
                    Code = code,
                    Password = password,
                    DisplayName = displayName,
                    ProfileImageBytes = profileImageBytes,
                    ProfileImageContentType = string.IsNullOrWhiteSpace(profileImageContentType) ? null : profileImageContentType
                });

                SafeClose(authClient);

                MessageBox.Show("Registration completed.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Information);

                new LoginWindow().Show();
                Close();
            }
            catch (FaultException<ServiceFault> fx)
            {
                authClient.Abort();
                MessageBox.Show(fx.Detail.Message, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                authClient.Abort();
                Logger.Error("EmailVerificationWindow.ConfirmClick: unexpected error.", ex);

                MessageBox.Show("Network/service error.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnConfirm.IsEnabled = true;
            }
        }

        private async void ResendClick(object sender, RoutedEventArgs e)
        {
            btnResend.IsEnabled = false;

            var authClient = new AuthServiceClient();

            try
            {
                await authClient.BeginRegisterAsync(new BeginRegisterRequest { Email = email });
                SafeClose(authClient);

                MessageBox.Show("Verification code resent.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (FaultException<ServiceFault> fx)
            {
                authClient.Abort();
                MessageBox.Show(fx.Detail.Message, Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                authClient.Abort();
                Logger.Error("EmailVerificationWindow.ResendClick: unexpected error.", ex);

                MessageBox.Show("Network/service error.", Lang.registerTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnResend.IsEnabled = true;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            new RegistrationWindow().Show();
            Close();
        }

        private static void SafeClose(ICommunicationObject obj)
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                if (obj.State != CommunicationState.Faulted)
                {
                    obj.Close();
                }
                else
                {
                    obj.Abort();
                }
            }
            catch
            {
                obj.Abort();
            }
        }
    }
}
