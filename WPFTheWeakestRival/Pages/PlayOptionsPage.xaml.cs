using System;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.Properties.Langs;
using WPFTheWeakestRival.Helpers;

namespace WPFTheWeakestRival.Pages
{
    public partial class PlayOptionsPage : Page
    {
        public event EventHandler CreateRequested;
        public event EventHandler<string> JoinRequested;

        public PlayOptionsPage()
        {
            InitializeComponent();

            UiValidationHelper.ApplyMaxLength(txtCode, UiValidationHelper.GENERIC_TEXT_MAX_LENGTH);
        }

        private void BtnCreateClick(object sender, RoutedEventArgs e)
        {
            CreateRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnJoinClick(object sender, RoutedEventArgs e)
        {
            var code = (txtCode?.Text ?? string.Empty).Trim();
            if (code.Length == 0)
            {
                MessageBox.Show(Lang.lobbyEnterAccessCode);
                return;
            }

            JoinRequested?.Invoke(this, code);
        }
    }
}
