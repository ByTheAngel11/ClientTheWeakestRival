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
        private const int DEFAULT_VOTE_DURATION_SECONDS = 15;
        private const int MIN_VOTE_DURATION_SECONDS = 1;
        private const int MAX_VOTE_DURATION_SECONDS = 120;

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
            IEnumerable<PlayerVoteItem> playersSource,
            int voteDurationSeconds)
        {
            InitializeComponent();

            this.matchId = matchId;
            this.myUserId = myUserId;

            players = new ObservableCollection<PlayerVoteItem>();

            if (playersSource != null)
            {
                foreach (var player in playersSource)
                {
                    players.Add(player);
                }
            }

            LstVotePlayers.ItemsSource = players;

            remainingVoteSeconds = NormalizeVoteDurationSeconds(voteDurationSeconds);
            UpdateCountdownLabel();

            voteTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            voteTimer.Tick += VoteTimerTick;
            voteTimer.Start();
        }

        private static int NormalizeVoteDurationSeconds(int seconds)
        {
            if (seconds < MIN_VOTE_DURATION_SECONDS)
            {
                return DEFAULT_VOTE_DURATION_SECONDS;
            }

            if (seconds > MAX_VOTE_DURATION_SECONDS)
            {
                return MAX_VOTE_DURATION_SECONDS;
            }

            return seconds;
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
