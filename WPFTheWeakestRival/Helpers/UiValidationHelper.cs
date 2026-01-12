using System.Windows.Controls;

namespace WPFTheWeakestRival.Helpers
{
    internal static class UiValidationHelper
    {
        public const int EMAIL_MAX_LENGTH = 254;
        public const int DISPLAY_NAME_MAX_LENGTH = 50;
        public const int PASSWORD_MAX_LENGTH = 128;
        public const int CODE_MAX_LENGTH = 16;
        public const int GENERIC_TEXT_MAX_LENGTH = 200;
        public const int COMMENT_MAX_LENGTH = 500;
        public const int NUMBER_TEXT_MAX_LENGTH = 6;

        public static void ApplyMaxLength(TextBox tb, int max)
        {
            if (tb == null) return;
            tb.MaxLength = max;
        }

        public static void ApplyMaxLength(PasswordBox pb, int max)
        {
            if (pb == null) return;
            try
            {
                pb.MaxLength = max;
            }
            catch
            {
                // older PasswordBox implementations may not support setting MaxLength in some edge cases
            }
        }

        public static string TrimOrEmpty(string s)
        {
            return (s ?? string.Empty).Trim();
        }
    }
}
