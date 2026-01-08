using System;

namespace WPFTheWeakestRival.Infrastructure.Faults
{
    internal static class FaultKeyMessageResolver
    {
        private const string KEY_UI_GENERIC_ERROR = "UiGenericError";

        public static string Resolve(string messageKey, Func<string, string> localize)
        {
            if (localize == null)
            {
                throw new ArgumentNullException(nameof(localize));
            }

            string safeKey = (messageKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(safeKey))
            {
                return localize(KEY_UI_GENERIC_ERROR);
            }

            string localized = localize(safeKey);

            if (string.IsNullOrWhiteSpace(localized) ||
                string.Equals(localized.Trim(), safeKey, StringComparison.Ordinal))
            {
                return localize(KEY_UI_GENERIC_ERROR);
            }

            return localized;
        }
    }
}
