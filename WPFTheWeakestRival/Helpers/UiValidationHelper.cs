using System;
using System.Windows.Controls;
using log4net;

namespace WPFTheWeakestRival.Helpers
{
    internal static class UiValidationHelper
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(UiValidationHelper));

        private const string LOG_PASSWORD_MAX_LENGTH_SET_FAILED =
            "UiValidationHelper.ApplyMaxLength(PasswordBox) failed to set PasswordBox.MaxLength. The exception will be ignored.";

        public const int EMAIL_MAX_LENGTH = 254;
        public const int DISPLAY_NAME_MAX_LENGTH = 50;
        public const int PASSWORD_MAX_LENGTH = 128;
        public const int CODE_MAX_LENGTH = 16;
        public const int GENERIC_TEXT_MAX_LENGTH = 200;
        public const int COMMENT_MAX_LENGTH = 500;
        public const int NUMBER_TEXT_MAX_LENGTH = 6;

        public static void ApplyMaxLength(TextBox textBox, int max)
        {
            if (textBox == null) return;
            textBox.MaxLength = max;
        }

        public static void ApplyMaxLength(PasswordBox passwordBox, int max)
        {
            if (passwordBox == null) return;

            try
            {
                passwordBox.MaxLength = max;
            }
            catch (NotSupportedException ex)
            {
                Logger.Warn(LOG_PASSWORD_MAX_LENGTH_SET_FAILED, ex);
            }
            catch (Exception ex)
            {
                Logger.Error(LOG_PASSWORD_MAX_LENGTH_SET_FAILED, ex);
            }
        }

        public static string TrimOrEmpty(string text)
        {
            return (text ?? string.Empty).Trim();
        }
    }
}
