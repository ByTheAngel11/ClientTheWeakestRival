using System;
using System.Windows;
using log4net;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyUiDispatcher
    {
        private readonly Window window;
        private readonly ILog logger;

        internal LobbyUiDispatcher(Window window, ILog logger)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal void Ui(Action action)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                if (window.Dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                window.Dispatcher.BeginInvoke(action);
            }
            catch (Exception ex)
            {
                logger.Warn("LobbyUiDispatcher.Ui error.", ex);
            }
        }
    }
}
