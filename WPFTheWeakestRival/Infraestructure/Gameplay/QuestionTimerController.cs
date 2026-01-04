using System;
using System.Windows.Threading;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class QuestionTimerController
    {
        private readonly DispatcherTimer timer;
        private int remainingSeconds;

        public QuestionTimerController(TimeSpan tickInterval)
        {
            timer = new DispatcherTimer
            {
                Interval = tickInterval
            };

            timer.Tick += OnTickInternal;
        }

        public event Action<int> Tick;
        public event Action Expired;

        public int RemainingSeconds => remainingSeconds;

        public bool IsRunning => timer.IsEnabled;

        public void Start(int seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            remainingSeconds = seconds;

            if (!timer.IsEnabled)
            {
                timer.Start();
            }

            Tick?.Invoke(remainingSeconds);
        }

        public void Stop()
        {
            if (timer.IsEnabled)
            {
                timer.Stop();
            }
        }

        private void OnTickInternal(object sender, EventArgs e)
        {
            if (remainingSeconds > 0)
            {
                remainingSeconds--;
                Tick?.Invoke(remainingSeconds);
            }

            if (remainingSeconds <= 0)
            {
                Stop();
                Expired?.Invoke();
            }
        }
    }
}
