using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPFTheWeakestRival.Pages
{
    public partial class MatchSettingsPage : Page
    {
        public bool IsPrivate { get; private set; }
        public int MaxPlayers { get; private set; }

        public MatchSettingsPage(bool isPrivate, int maxPlayers)
        {
            InitializeComponent();

            IsPrivate = isPrivate;
            MaxPlayers = maxPlayers;

            if (chkPrivate != null)
            {
                chkPrivate.IsChecked = isPrivate;
            }

            if (txtMaxPlayers != null)
            {
                txtMaxPlayers.Text = maxPlayers.ToString();
            }
        }

        private void BtnSaveClick(object sender, RoutedEventArgs e)
        {
            int parsedMaxPlayers;
            if (txtMaxPlayers == null || !int.TryParse(txtMaxPlayers.Text, out parsedMaxPlayers) || parsedMaxPlayers <= 0)
            {
                MessageBox.Show("Ingresa un número válido de jugadores (mayor que 0).",
                    "Configuración de partida",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MaxPlayers = parsedMaxPlayers;
            IsPrivate = chkPrivate != null && chkPrivate.IsChecked == true;

            var win = Window.GetWindow(this);
            if (win != null)
            {
                win.DialogResult = true;
                win.Close();
            }
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win != null)
            {
                win.DialogResult = false;
                win.Close();
            }
        }
    }
}
