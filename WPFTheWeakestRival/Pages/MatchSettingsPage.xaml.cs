using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Pages
{
    public partial class MatchSettingsPage : Page
    {
        private const int MAX_PLAYERS_MIN = 1;
        private const int MAX_PLAYERS_MAX = 16;

        private const string DECIMAL_FORMAT = "0.##";

        public bool IsPrivate { get; private set; }
        public int MaxPlayers { get; private set; }

        public decimal StartingScore { get; private set; }
        public decimal MaxScore { get; private set; }
        public decimal PointsPerCorrect { get; private set; }
        public decimal PointsPerWrong { get; private set; }
        public decimal PointsPerEliminationGain { get; private set; }
        public bool AllowTiebreakCoinflip { get; private set; }

        public MatchSettingsPage(MatchSettingsDefaults defaults)
        {
            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            InitializeComponent();

            ApplyTextLimits(this);
            LoadDefaults(this, defaults);
        }

        private static void ApplyTextLimits(MatchSettingsPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            UiValidationHelper.ApplyMaxLength(page.txtMaxPlayers, UiValidationHelper.NUMBER_TEXT_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(page.txtStartingScore, UiValidationHelper.GENERIC_TEXT_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(page.txtMaxScore, UiValidationHelper.GENERIC_TEXT_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(page.txtPointsCorrect, UiValidationHelper.GENERIC_TEXT_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(page.txtPointsWrong, UiValidationHelper.GENERIC_TEXT_MAX_LENGTH);
            UiValidationHelper.ApplyMaxLength(page.txtPointsElimination, UiValidationHelper.GENERIC_TEXT_MAX_LENGTH);
        }

        private static void LoadDefaults(MatchSettingsPage page, MatchSettingsDefaults defaults)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            page.chkPrivate.IsChecked = defaults.IsPrivate;

            page.txtMaxPlayers.Text = defaults.MaxPlayers.ToString(CultureInfo.InvariantCulture);

            page.txtStartingScore.Text = defaults.StartingScore.ToString(DECIMAL_FORMAT, CultureInfo.InvariantCulture);
            page.txtMaxScore.Text = defaults.MaxScore.ToString(DECIMAL_FORMAT, CultureInfo.InvariantCulture);

            page.txtPointsCorrect.Text = defaults.PointsPerCorrect.ToString(DECIMAL_FORMAT, CultureInfo.InvariantCulture);
            page.txtPointsWrong.Text = defaults.PointsPerWrong.ToString(DECIMAL_FORMAT, CultureInfo.InvariantCulture);
            page.txtPointsElimination.Text =
                defaults.PointsPerEliminationGain.ToString(DECIMAL_FORMAT, CultureInfo.InvariantCulture);

            page.chkCoinflip.IsChecked = defaults.AllowTiebreakCoinflip;
        }

        private void BtnAcceptClick(object sender, RoutedEventArgs e)
        {
            if (!TryReadMaxPlayers(this, out int maxPlayers))
            {
                ShowValidationErrorAndFocus(txtMaxPlayers, Lang.matchSettingsInvalidMaxPlayers);
                return;
            }

            if (!TryReadDecimal(txtStartingScore, out decimal startingScore))
            {
                ShowValidationErrorAndFocus(txtStartingScore, Lang.matchSettingsInvalidStartingScore);
                return;
            }

            if (!TryReadDecimal(txtMaxScore, out decimal maxScore))
            {
                ShowValidationErrorAndFocus(txtMaxScore, Lang.matchSettingsInvalidMaxScore);
                return;
            }

            if (!TryReadDecimal(txtPointsCorrect, out decimal pointsCorrect))
            {
                ShowValidationErrorAndFocus(txtPointsCorrect, Lang.matchSettingsInvalidPointsCorrect);
                return;
            }

            if (!TryReadDecimal(txtPointsWrong, out decimal pointsWrong))
            {
                ShowValidationErrorAndFocus(txtPointsWrong, Lang.matchSettingsInvalidPointsWrong);
                return;
            }

            if (!TryReadDecimal(txtPointsElimination, out decimal pointsElimination))
            {
                ShowValidationErrorAndFocus(txtPointsElimination, Lang.matchSettingsInvalidPointsElimination);
                return;
            }

            IsPrivate = chkPrivate.IsChecked.GetValueOrDefault();
            MaxPlayers = maxPlayers;

            StartingScore = startingScore;
            MaxScore = maxScore;

            PointsPerCorrect = pointsCorrect;
            PointsPerWrong = pointsWrong;
            PointsPerEliminationGain = pointsElimination;

            AllowTiebreakCoinflip = chkCoinflip.IsChecked.GetValueOrDefault();

            CloseDialog(true);
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            CloseDialog(false);
        }

        private static bool TryReadDecimal(TextBox textBox, out decimal value)
        {
            if (textBox == null)
            {
                value = default(decimal);
                return false;
            }

            return TryParseDecimal(textBox.Text, out value);
        }

        private static bool TryReadMaxPlayers(MatchSettingsPage page, out int maxPlayers)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            bool parsed = int.TryParse(
                (page.txtMaxPlayers.Text ?? string.Empty).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out maxPlayers);

            if (!parsed)
            {
                return false;
            }

            return maxPlayers >= MAX_PLAYERS_MIN && maxPlayers <= MAX_PLAYERS_MAX;
        }

        private static bool TryParseDecimal(string text, out decimal value)
        {
            return decimal.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static void ShowValidationErrorAndFocus(TextBox textBox, string message)
        {
            MessageBox.Show(message, Lang.matchSettingsTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

            if (textBox != null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void CloseDialog(bool dialogResult)
        {
            Window win = Window.GetWindow(this);
            if (win == null)
            {
                return;
            }

            win.DialogResult = dialogResult;
            win.Close();
        }
    }

    public sealed class MatchSettingsDefaults
    {
        public bool IsPrivate { get; }
        public int MaxPlayers { get; }

        public decimal StartingScore { get; }
        public decimal MaxScore { get; }

        public decimal PointsPerCorrect { get; }
        public decimal PointsPerWrong { get; }
        public decimal PointsPerEliminationGain { get; }

        public bool AllowTiebreakCoinflip { get; }

        public MatchSettingsDefaults(
            bool isPrivate,
            int maxPlayers,
            decimal startingScore,
            decimal maxScore,
            decimal pointsPerCorrect,
            decimal pointsPerWrong,
            decimal pointsPerEliminationGain,
            bool allowTiebreakCoinflip)
        {
            IsPrivate = isPrivate;
            MaxPlayers = maxPlayers;

            StartingScore = startingScore;
            MaxScore = maxScore;

            PointsPerCorrect = pointsPerCorrect;
            PointsPerWrong = pointsPerWrong;
            PointsPerEliminationGain = pointsPerEliminationGain;

            AllowTiebreakCoinflip = allowTiebreakCoinflip;
        }
    }
}
