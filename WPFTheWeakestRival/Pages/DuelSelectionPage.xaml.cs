using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Pages
{
    public partial class DuelSelectionPage : Page
    {
        private readonly int matchId;
        private readonly int weakestRivalUserId;

        public event EventHandler<DuelSelectionCompletedEventArgs> DuelSelectionCompleted;

        public DuelSelectionPage(
            int matchId,
            int weakestRivalUserId,
            IReadOnlyCollection<DuelCandidateItem> candidates)
        {
            InitializeComponent();

            this.matchId = matchId;
            this.weakestRivalUserId = weakestRivalUserId;

            lstCandidates.ItemsSource = candidates ?? Array.Empty<DuelCandidateItem>();
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            OnDuelSelectionCompleted(null);
            CloseHostWindow();
        }

        private void BtnConfirmClick(object sender, RoutedEventArgs e)
        {
            if (!(lstCandidates.SelectedItem is DuelCandidateItem selected))
            {
                MessageBox.Show(
                    "Selecciona a un jugador para retarlo.",
                    "Duelo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            OnDuelSelectionCompleted(selected.UserId);
            CloseHostWindow();
        }

        private void OnDuelSelectionCompleted(int? targetUserId)
        {
            var handler = DuelSelectionCompleted;
            if (handler == null)
            {
                return;
            }

            var args = new DuelSelectionCompletedEventArgs(
                matchId,
                weakestRivalUserId,
                targetUserId);

            handler(this, args);
        }

        private void CloseHostWindow()
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.Close();
            }
        }
    }
}
