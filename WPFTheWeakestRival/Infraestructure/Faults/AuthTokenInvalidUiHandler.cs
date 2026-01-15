using log4net;
using System;
using System.Globalization;
using System.Windows;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infrastructure.Faults
{
    public static class AuthTokenInvalidUiHandler
    {
        private const string CODE_TOKEN_INVALID = "TOKEN_INVALID";
        private const string CODE_AUTH_TOKEN_INVALID = "AUTH_TOKEN_INVALID";
        private const string CODE_INVALID_TOKEN = "INVALID_TOKEN";
        private const string CODE_TOKEN_EXPIRED = "TOKEN_EXPIRED";
        private const string CODE_SESSION_EXPIRED = "SESSION_EXPIRED";

        private const string MARKER_ES_TOKEN_INVALID = "token inválido";
        private const string MARKER_ES_TOKEN_INVALID2 = "token invalido";
        private const string MARKER_ES_EXPIRED = "expirado";
        private const string MARKER_EN_INVALID = "invalid token";
        private const string MARKER_EN_EXPIRED = "expired";

        private const string TOKEN_WORD_MARKER = "token";

        private const string CTX_FALLBACK = "AuthTokenInvalidUiHandler";

        private const string LOG_INVALID_TOKEN_TEMPLATE =
            "Invalid token detected. Ctx={0}, Code={1}, Message={2}";

        private const string LOG_CLEAR_TOKEN_FAILED =
            "AuthTokenInvalidUiHandler: failed to clear token.";

        private const string LOG_SESSION_CLEANUP_FAILED =
            "AuthTokenInvalidUiHandler: SessionCleanup failed.";

        private const string LOG_MESSAGEBOX_FAILED =
            "AuthTokenInvalidUiHandler: MessageBox failed.";

        private const string LOG_REDIRECT_FAILED =
            "AuthTokenInvalidUiHandler: redirect to Login failed.";

        public static bool TryHandleInvalidToken(
            string faultCode,
            string faultMessage,
            string context,
            ILog logger,
            object uiOwner)
        {
            if (!IsInvalidToken(faultCode, faultMessage))
            {
                return false;
            }

            if (logger != null)
            {
                logger.WarnFormat(
                    CultureInfo.InvariantCulture,
                    LOG_INVALID_TOKEN_TEMPLATE,
                    context ?? string.Empty,
                    faultCode ?? string.Empty,
                    faultMessage ?? string.Empty);
            }

            try
            {
                LoginWindow.AppSession.CurrentToken = null;
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Warn(LOG_CLEAR_TOKEN_FAILED, ex);
                }
            }

            try
            {
                SessionCleanup.Shutdown(string.IsNullOrWhiteSpace(context) ? CTX_FALLBACK : context);
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Warn(LOG_SESSION_CLEANUP_FAILED, ex);
                }
            }

            Window ownerWindow = ResolveOwnerWindow(uiOwner);

            try
            {
                if (ownerWindow != null)
                {
                    MessageBox.Show(
                        ownerWindow,
                        Lang.authTokenInvalidMessage,
                        Lang.loginTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        Lang.authTokenInvalidMessage,
                        Lang.loginTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Warn(LOG_MESSAGEBOX_FAILED, ex);
                }
            }

            try
            {
                var loginWindow = new LoginWindow();

                var app = Application.Current;
                if (app != null)
                {
                    app.MainWindow = loginWindow;
                    app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                }

                loginWindow.Show();

                if (ownerWindow != null && !ReferenceEquals(ownerWindow, loginWindow))
                {
                    ownerWindow.Close();
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(LOG_REDIRECT_FAILED, ex);
                }
            }

            return true;
        }

        private static Window ResolveOwnerWindow(object uiOwner)
        {
            if (uiOwner is Window w)
            {
                return w;
            }

            if (uiOwner is DependencyObject dep)
            {
                return Window.GetWindow(dep);
            }

            return Application.Current != null ? Application.Current.MainWindow : null;
        }

        private static bool IsInvalidToken(string faultCode, string faultMessage)
        {
            string code = (faultCode ?? string.Empty).Trim();
            string message = (faultMessage ?? string.Empty).Trim();

            if (IsInvalidTokenCode(code))
            {
                return true;
            }

            if (ContainsInvalidTokenMarkers(message))
            {
                return true;
            }

            return false;
        }

        private static bool IsInvalidTokenCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            return
                string.Equals(code, CODE_TOKEN_INVALID, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, CODE_AUTH_TOKEN_INVALID, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, CODE_INVALID_TOKEN, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, CODE_TOKEN_EXPIRED, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, CODE_SESSION_EXPIRED, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsInvalidTokenMarkers(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string lower = message.ToLowerInvariant();

            bool hasToken = lower.Contains(TOKEN_WORD_MARKER);

            bool hasInvalid =
                lower.Contains(MARKER_ES_TOKEN_INVALID) ||
                lower.Contains(MARKER_ES_TOKEN_INVALID2) ||
                lower.Contains(MARKER_EN_INVALID);

            bool hasExpired =
                lower.Contains(MARKER_ES_EXPIRED) ||
                lower.Contains(MARKER_EN_EXPIRED);

            return hasToken && (hasInvalid || hasExpired);
        }
    }
}
