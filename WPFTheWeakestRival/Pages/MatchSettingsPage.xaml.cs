using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace WPFTheWeakestRival.Pages
{
    public partial class MatchSettingsPage : Page
    {
        // Propiedades que el Lobby leerá después del dialog
        public bool IsPrivate { get; private set; }
        public int MaxPlayers { get; private set; }

        public decimal StartingScore { get; private set; }
        public decimal MaxScore { get; private set; }
        public decimal PointsPerCorrect { get; private set; }
        public decimal PointsPerWrong { get; private set; }
        public decimal PointsPerEliminationGain { get; private set; }
        public bool AllowTiebreakCoinflip { get; private set; }

        public MatchSettingsPage(
            bool isPrivate,
            int maxPlayers,
            decimal startingScore,
            decimal maxScore,
            decimal pointsPerCorrect,
            decimal pointsPerWrong,
            decimal pointsPerEliminationGain,
            bool allowTiebreakCoinflip)
        {
            InitializeComponent();

            // Cargamos valores iniciales a los controles
            chkPrivate.IsChecked = isPrivate;
            txtMaxPlayers.Text = maxPlayers.ToString(CultureInfo.InvariantCulture);

            txtStartingScore.Text = startingScore.ToString("0.##", CultureInfo.InvariantCulture);
            txtMaxScore.Text = maxScore.ToString("0.##", CultureInfo.InvariantCulture);
            txtPointsCorrect.Text = pointsPerCorrect.ToString("0.##", CultureInfo.InvariantCulture);
            txtPointsWrong.Text = pointsPerWrong.ToString("0.##", CultureInfo.InvariantCulture);
            txtPointsElimination.Text = pointsPerEliminationGain.ToString("0.##", CultureInfo.InvariantCulture);
            chkCoinflip.IsChecked = allowTiebreakCoinflip;
        }

        private void BtnAcceptClick(object sender, RoutedEventArgs e)
        {
            // Validaciones rápidas y asignación de propiedades
            if (!int.TryParse(txtMaxPlayers.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxPlayers) ||
                maxPlayers <= 0 || maxPlayers > 16)
            {
                MessageBox.Show("Máx. jugadores debe ser un número entre 1 y 16.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtMaxPlayers.Focus();
                txtMaxPlayers.SelectAll();
                return;
            }

            if (!TryParseDecimal(txtStartingScore.Text, out var startingScore))
            {
                MessageBox.Show("Puntaje inicial no es válido.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtStartingScore.Focus();
                txtStartingScore.SelectAll();
                return;
            }

            if (!TryParseDecimal(txtMaxScore.Text, out var maxScore))
            {
                MessageBox.Show("Puntaje máximo no es válido.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtMaxScore.Focus();
                txtMaxScore.SelectAll();
                return;
            }

            if (!TryParseDecimal(txtPointsCorrect.Text, out var pointsCorrect))
            {
                MessageBox.Show("Puntos por acierto no son válidos.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPointsCorrect.Focus();
                txtPointsCorrect.SelectAll();
                return;
            }

            if (!TryParseDecimal(txtPointsWrong.Text, out var pointsWrong))
            {
                MessageBox.Show("Puntos por fallo no son válidos.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPointsWrong.Focus();
                txtPointsWrong.SelectAll();
                return;
            }

            if (!TryParseDecimal(txtPointsElimination.Text, out var pointsElimination))
            {
                MessageBox.Show("Puntos por eliminación no son válidos.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPointsElimination.Focus();
                txtPointsElimination.SelectAll();
                return;
            }

            // Si todo va bien: guardamos en las propiedades públicas
            IsPrivate = chkPrivate.IsChecked == true;
            MaxPlayers = maxPlayers;
            StartingScore = startingScore;
            MaxScore = maxScore;
            PointsPerCorrect = pointsCorrect;
            PointsPerWrong = pointsWrong;
            PointsPerEliminationGain = pointsElimination;
            AllowTiebreakCoinflip = chkCoinflip.IsChecked == true;

            // Cerramos el diálogo padre
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

        private static bool TryParseDecimal(string text, out decimal value)
        {
            return decimal.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
