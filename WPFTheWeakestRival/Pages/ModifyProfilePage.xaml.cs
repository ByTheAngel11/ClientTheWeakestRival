using Microsoft.Win32;
using System;
using System.IO;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival
{
    public partial class ModifyProfilePage : Page
    {
        private readonly LobbyServiceClient _svc;
        private readonly string _token;

        public event EventHandler Closed;

        public ModifyProfilePage(LobbyServiceClient svc, string token)
        {
            InitializeComponent();
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _token = token ?? throw new ArgumentNullException(nameof(token));
            CargarPerfil();
        }

        private void CargarPerfil()
        {
            try
            {
                var profile = _svc.GetMyProfile(_token);
                txtEmail.Text = profile.Email ?? string.Empty;
                txtDisplayName.Text = profile.DisplayName ?? string.Empty;
                txtAvatarUrl.Text = profile.ProfileImageUrl ?? string.Empty;
                CargarPreviewDesdeUrl(txtAvatarUrl.Text);
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Perfil",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Perfil",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => Closed?.Invoke(this, EventArgs.Empty);

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var req = new UpdateAccountRequest
                {
                    Token = _token,
                    Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text,
                    DisplayName = string.IsNullOrWhiteSpace(txtDisplayName.Text) ? null : txtDisplayName.Text,
                    ProfileImageUrl = string.IsNullOrWhiteSpace(txtAvatarUrl.Text) ? null : txtAvatarUrl.Text
                };

                _svc.UpdateAccount(req);
                MessageBox.Show("Perfil actualizado.", "OK",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Closed?.Invoke(this, EventArgs.Empty);
            }
            catch (FaultException<ServiceFault> fx)
            {
                MessageBox.Show($"{fx.Detail.Code}: {fx.Detail.Message}", "Modificar Perfil",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Modificar Perfil",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Elegir imagen de avatar",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp",
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                CargarPreviewDesdeArchivo(dlg.FileName);
                txtAvatarUrl.Text = new Uri(dlg.FileName, UriKind.Absolute).AbsoluteUri;
            }
        }

        private void BtnClearImage_Click(object sender, RoutedEventArgs e)
        {
            txtAvatarUrl.Text = string.Empty;
            imgPreview.Source = null;
        }


        private void CargarPreviewDesdeArchivo(string path)
        {
            imgPreview.Source = UiImageHelper.TryCreateFromUrlOrPath(path, decodePixelWidth: 128);
        }

        private void CargarPreviewDesdeUrl(string url)
        {
            imgPreview.Source = UiImageHelper.TryCreateFromUrlOrPath(url, decodePixelWidth: 128);
        }
    }
}
