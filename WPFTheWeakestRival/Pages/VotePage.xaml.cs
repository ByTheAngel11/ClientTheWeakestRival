using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Pages
{
    public sealed partial class VotePage : Page
    {
        private const int VOTE_DURATION_SECONDS = 15;

        private readonly int matchId;
        private readonly int myUserId;
        private readonly ObservableCollection<PlayerVoteItem> players;

        private readonly DispatcherTimer voteTimer;
        private int remainingVoteSeconds;
        private bool voteSent;

        public event EventHandler<VoteCompletedEventArgs> VoteCompleted;

        public VotePage(
            int matchId,
            int myUserId,
            IEnumerable<PlayerVoteItem> playersSource)
        {
            InitializeComponent();

            this.matchId = matchId;
            this.myUserId = myUserId;

            players = new ObservableCollection<PlayerVoteItem>();

            if (playersSource != null)
            {
                // Ya NO filtramos aquí por myUserId.
                foreach (var player in playersSource)
                {
                    players.Add(player);
                }
            }

            LstVotePlayers.ItemsSource = players;

            remainingVoteSeconds = VOTE_DURATION_SECONDS;
            UpdateCountdownLabel();

            voteTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            voteTimer.Tick += VoteTimerTick;
            voteTimer.Start();
        }

        private void VoteTimerTick(object sender, EventArgs e)
        {
            remainingVoteSeconds--;
            UpdateCountdownLabel();

            if (remainingVoteSeconds > 0)
            {
                return;
            }

            voteTimer.Stop();

            if (!voteSent)
            {
                // Se acabó el tiempo sin voto → se envía "skip"
                OnVoteCompleted(null);
            }

            CloseHostWindow();
        }

        private void UpdateCountdownLabel()
        {
            if (LblVoteCountdown != null)
            {
                LblVoteCountdown.Text = remainingVoteSeconds.ToString();
            }
        }

        private void BtnConfirmVote_Click(object sender, RoutedEventArgs e)
        {
            if (voteSent)
            {
                return;
            }

            var selected = LstVotePlayers.SelectedItem as PlayerVoteItem;

            if (selected == null)
            {
                MessageBox.Show(
                    "Debes seleccionar a un jugador para votar.",
                    "Votación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            voteSent = true;
            voteTimer.Stop();

            OnVoteCompleted(selected.UserId);
            CloseHostWindow();
        }

        private void BtnSkipVote_Click(object sender, RoutedEventArgs e)
        {
            if (voteSent)
            {
                return;
            }

            voteSent = true;
            voteTimer.Stop();

            OnVoteCompleted(null);
            CloseHostWindow();
        }

        private void OnVoteCompleted(int? targetUserId)
        {
            VoteCompleted?.Invoke(
                this,
                new VoteCompletedEventArgs(
                    matchId,
                    myUserId,
                    targetUserId));
        }

        private void CloseHostWindow()
        {
            var hostWindow = Window.GetWindow(this);

            if (hostWindow != null)
            {
                hostWindow.Close();
            }
        }
    }

    public sealed class VoteCompletedEventArgs : EventArgs
    {
        public VoteCompletedEventArgs(
            int matchId,
            int voterUserId,
            int? targetUserId)
        {
            MatchId = matchId;
            VoterUserId = voterUserId;
            TargetUserId = targetUserId;
        }

        public int MatchId { get; }

        public int VoterUserId { get; }

        public int? TargetUserId { get; }
    }
}
