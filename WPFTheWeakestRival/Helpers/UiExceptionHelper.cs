using log4net;
using System;
using System.Windows;
using WPFTheWeakestRival.Properties.Langs;
using System.Diagnostics;

namespace WPFTheWeakestRival.Helpers
{
    public static class UiExceptionHelper
    {
        private const string DEFAULT_CONTEXT = "UiExceptionHelper.ShowError";

        public static void ShowError(Exception ex, string context, ILog logger)
        {
            ShowError(ex, context, logger, owner: null);
        }

        public static void ShowError(Exception ex, string context, ILog logger, Window owner)
        {
            string safeContext = string.IsNullOrWhiteSpace(context)
                ? DEFAULT_CONTEXT
                : context;

            try
            {
                if (logger != null)
                {
                    logger.Error(safeContext, ex);
                }
            }
            catch (Exception logEx)
            {
                try
                {
                    if (logger != null)
                    {
                        logger.Warn("UiExceptionHelper.ShowError failed while logging.", logEx);
                    }
                    else
                    {
                        Trace.TraceWarning("UiExceptionHelper.ShowError failed while logging: " + logEx);
                    }
                }
                catch (Exception traceEx)
                {
                    Trace.TraceWarning("UiExceptionHelper.ShowError nested logging failure: " + traceEx);
                }
            }

            string message = Lang.UiGenericError;
            string title = Lang.lobbyTitle;

            if (owner != null)
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
