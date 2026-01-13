using System.Windows;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal static class SessionTokenProvider
    {
        internal static string GetTokenOrShowMessage()
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;

            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(Lang.noValidSessionCode);
                return string.Empty;
            }

            return token;
        }
    }
}
