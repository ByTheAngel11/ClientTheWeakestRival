using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Infrastructure
{
    public sealed class FriendsDrawerView
    {
        public FrameworkElement BlurTarget { get; }
        public Grid DrawerHost { get; }
        public TranslateTransform DrawerTransform { get; }
        public Panel EmptyPanel { get; }
        public ListBox FriendsList { get; }
        public TextBlock RequestsCountText { get; }
        public Storyboard OpenStoryboard { get; }
        public Storyboard CloseStoryboard { get; }

        public FriendsDrawerView(
            FrameworkElement blurTarget,
            Grid drawerHost,
            TranslateTransform drawerTransform,
            Panel emptyPanel,
            ListBox friendsList,
            TextBlock requestsCountText,
            Storyboard openStoryboard,
            Storyboard closeStoryboard)
        {
            BlurTarget = blurTarget ?? throw new ArgumentNullException(nameof(blurTarget));
            DrawerHost = drawerHost ?? throw new ArgumentNullException(nameof(drawerHost));
            DrawerTransform = drawerTransform ?? throw new ArgumentNullException(nameof(drawerTransform));
            EmptyPanel = emptyPanel ?? throw new ArgumentNullException(nameof(emptyPanel));
            FriendsList = friendsList ?? throw new ArgumentNullException(nameof(friendsList));
            RequestsCountText = requestsCountText ?? throw new ArgumentNullException(nameof(requestsCountText));
            OpenStoryboard = openStoryboard ?? throw new ArgumentNullException(nameof(openStoryboard));
            CloseStoryboard = closeStoryboard ?? throw new ArgumentNullException(nameof(closeStoryboard));
        }
    }

    public sealed class FriendsDrawerOptions
    {
        private const double DEFAULT_DRAWER_INITIAL_TRANSLATION_X = 340.0;

        public Func<bool> CanClearEffect { get; set; }

        public double BlurInRadius { get; set; } = 3.0;

        public int AnimInMs { get; set; } = 180;

        public int AnimOutMs { get; set; } = 160;

        public double DrawerInitialTranslationX { get; set; } = DEFAULT_DRAWER_INITIAL_TRANSLATION_X;
    }

    public sealed class FriendsDrawer : IDisposable
    {
        private const double BLUR_OUT_RADIUS = 0.0;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(FriendsDrawer));

        private readonly FriendManager manager;
        private readonly FriendsDrawerView view;
        private readonly FriendsDrawerOptions options;

        private readonly BlurEffect blurEffect = new BlurEffect { Radius = BLUR_OUT_RADIUS };
        private readonly ObservableCollection<FriendItem> items = new ObservableCollection<FriendItem>();

        public FriendsDrawer(FriendManager manager, FriendsDrawerView view, FriendsDrawerOptions options = null)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.options = options ?? new FriendsDrawerOptions();

            this.view.FriendsList.ItemsSource = items;
            this.manager.FriendsUpdated += OnFriendsUpdated;
        }

        public async System.Threading.Tasks.Task OpenAsync()
        {
            if (view.DrawerHost.Visibility == Visibility.Visible)
            {
                return;
            }

            view.BlurTarget.Effect = blurEffect;

            blurEffect.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(blurEffect.Radius, options.BlurInRadius, TimeSpan.FromMilliseconds(options.AnimInMs))
                {
                    EasingFunction = new QuadraticEase()
                });

            await manager.ManualRefreshAsync();

            view.DrawerTransform.X = options.DrawerInitialTranslationX;
            view.DrawerHost.Opacity = 0;
            view.DrawerHost.Visibility = Visibility.Visible;

            view.OpenStoryboard.Begin(view.BlurTarget, true);
        }

        public void Close()
        {
            if (view.DrawerHost.Visibility != Visibility.Visible)
            {
                return;
            }

            view.CloseStoryboard.Completed += CloseStoryboardCompleted;
            view.CloseStoryboard.Begin(view.BlurTarget, true);

            blurEffect.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation(blurEffect.Radius, BLUR_OUT_RADIUS, TimeSpan.FromMilliseconds(options.AnimOutMs))
                {
                    EasingFunction = new QuadraticEase()
                });
        }

        private void CloseStoryboardCompleted(object sender, EventArgs e)
        {
            view.CloseStoryboard.Completed -= CloseStoryboardCompleted;

            view.DrawerHost.Visibility = Visibility.Collapsed;

            if (options.CanClearEffect == null || options.CanClearEffect())
            {
                view.BlurTarget.Effect = null;
            }
        }

        private void OnFriendsUpdated(IReadOnlyList<FriendItem> list, int pendingCount)
        {
            items.Clear();

            if (list != null)
            {
                foreach (FriendItem item in list)
                {
                    items.Add(item);
                }
            }

            int pending = Math.Max(0, pendingCount);

            view.EmptyPanel.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            view.FriendsList.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            view.RequestsCountText.Text = pending.ToString(CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            try
            {
                manager.FriendsUpdated -= OnFriendsUpdated;
            }
            catch (Exception ex)
            {
                Logger.Warn("Error detaching FriendsUpdated handler.", ex);
            }
        }
    }
}
