using System;
using System.Collections.Generic;
using System.Windows;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Windows
{
    public partial class ReportPlayerWindow : Window
    {
        private const int COMMENT_MAX_LENGTH = 500;

        private const byte REASON_HARASSMENT = 1;
        private const byte REASON_CHEATING = 2;
        private const byte REASON_SPAM = 3;
        private const byte REASON_INAPPROPRIATE_NAME = 4;
        private const byte REASON_OTHER = 5;

        private sealed class ReasonItem
        {
            public byte Code { get; set; }
            public string Text { get; set; }
        }

        public byte SelectedReasonCode { get; private set; }
        public string Comment { get; private set; }

        public ReportPlayerWindow(string targetDisplayName)
        {
            InitializeComponent();

            if (txtTarget != null)
            {
                txtTarget.Text = string.IsNullOrWhiteSpace(targetDisplayName)
                    ? Lang.player
                    : Lang.player + ": " + targetDisplayName;
            }

            if (txtComment != null)
            {
                txtComment.MaxLength = COMMENT_MAX_LENGTH;
            }

            if (cmbReasons != null)
            {
                cmbReasons.ItemsSource = BuildReasons();
                cmbReasons.SelectedIndex = 0;
            }
        }

        private static IReadOnlyList<ReasonItem> BuildReasons()
        {
            return new List<ReasonItem>
            {
                new ReasonItem { Code = REASON_HARASSMENT, Text = Lang.reportReasonHarassment },
                new ReasonItem { Code = REASON_CHEATING, Text = Lang.reportReasonCheating },
                new ReasonItem { Code = REASON_SPAM, Text = Lang.reportReasonSpam },
                new ReasonItem { Code = REASON_INAPPROPRIATE_NAME, Text = Lang.reportReasonInappropriateName },
                new ReasonItem { Code = REASON_OTHER, Text = Lang.reportReasonOther }
            };
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSubmitClick(object sender, RoutedEventArgs e)
        {
            object selectedValue = cmbReasons != null ? cmbReasons.SelectedValue : null;
            if (selectedValue == null)
            {
                MessageBox.Show(Lang.reportPlayer, Lang.reportPlayer, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedReasonCode = Convert.ToByte(selectedValue);
            Comment = txtComment != null ? (txtComment.Text ?? string.Empty).Trim() : string.Empty;

            DialogResult = true;
            Close();
        }
    }
}
