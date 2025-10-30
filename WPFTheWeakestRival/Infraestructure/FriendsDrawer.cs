using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Infrastructure
{
    public sealed class FriendsDrawer : IDisposable
    {
        private readonly FriendManager manager;
        private readonly FrameworkElement blurTarget;
        private readonly Grid drawerHost;
        private readonly TranslateTransform drawerTransform;
        private readonly Panel emptyPanel;
        private readonly ListBox friendsList;
        private readonly TextBlock requestsCountText;
        private readonly Storyboard openStoryboard;
        private readonly Storyboard closeStoryboard;
        private readonly Func<bool> canClearEffect;
        private readonly double blurInRadius;
        private readonly int animInMs;
        private readonly int animOutMs;

        private readonly BlurEffect blurEffect = new BlurEffect { Radius = 0 };
        private readonly ObservableCollection<FriendItem> items = new ObservableCollection<FriendItem>();
        private int pending;

        public FriendsDrawer(
            FriendManager manager,
            FrameworkElement blurTarget,
            Grid drawerHost,
            TranslateTransform drawerTransform,
            Panel emptyPanel,
            ListBox friendsList,
            TextBlock requestsCountText,
            Storyboard openStoryboard,
            Storyboard closeStoryboard,
            Func<bool> canClearEffect = null,
            double blurInRadius = 3.0,
            int animInMs = 180,
            int animOutMs = 160)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.blurTarget = blurTarget ?? throw new ArgumentNullException(nameof(blurTarget));
            this.drawerHost = drawerHost ?? throw new ArgumentNullException(nameof(drawerHost));
            this.drawerTransform = drawerTransform ?? throw new ArgumentNullException(nameof(drawerTransform));
            this.emptyPanel = emptyPanel ?? throw new ArgumentNullException(nameof(emptyPanel));
            this.friendsList = friendsList ?? throw new ArgumentNullException(nameof(friendsList));
            this.requestsCountText = requestsCountText ?? throw new ArgumentNullException(nameof(requestsCountText));
            this.openStoryboard = openStoryboard ?? throw new ArgumentNullException(nameof(openStoryboard));
            this.closeStoryboard = closeStoryboard ?? throw new ArgumentNullException(nameof(closeStoryboard));
            this.canClearEffect = canClearEffect;
            this.blurInRadius = blurInRadius;
            this.animInMs = animInMs;
            this.animOutMs = animOutMs;

            friendsList.ItemsSource = items;
            manager.FriendsUpdated += OnFriendsUpdated;
        }

        public async Task OpenAsync()
        {
            if (drawerHost.Visibility == Visibility.Visible) return;

            blurTarget.Effect = blurEffect;
            blurEffect.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(blurEffect.Radius, blurInRadius, TimeSpan.FromMilliseconds(animInMs))
                {
                    EasingFunction = new QuadraticEase()
                });

            await manager.ManualRefreshAsync();

            drawerTransform.X = 340;
            drawerHost.Opacity = 0;
            drawerHost.Visibility = Visibility.Visible;
            openStoryboard.Begin((FrameworkElement)blurTarget, true);
        }

        public void Close()
        {
            if (drawerHost.Visibility != Visibility.Visible) return;

            closeStoryboard.Completed += CloseStoryboardCompleted;
            closeStoryboard.Begin((FrameworkElement)blurTarget, true);

            blurEffect.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(blurEffect.Radius, 0.0, TimeSpan.FromMilliseconds(animOutMs))
                {
                    EasingFunction = new QuadraticEase()
                });
        }

        private void CloseStoryboardCompleted(object sender, EventArgs e)
        {
            closeStoryboard.Completed -= CloseStoryboardCompleted;
            drawerHost.Visibility = Visibility.Collapsed;
            if (canClearEffect == null || canClearEffect())
            {
                blurTarget.Effect = null;
            }
        }

        private void OnFriendsUpdated(IReadOnlyList<FriendItem> list, int pendingCount)
        {
            items.Clear();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    items.Add(list[i]);
                }
            }
            pending = Math.Max(0, pendingCount);
            emptyPanel.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            friendsList.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            requestsCountText.Text = pending.ToString();
        }

        public void Dispose()
        {
            try { manager.FriendsUpdated -= OnFriendsUpdated; } catch { }
        }
    }
}
