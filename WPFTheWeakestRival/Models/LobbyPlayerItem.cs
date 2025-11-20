using System.Windows.Media;
using WPFTheWeakestRival.Controls;

namespace WPFTheWeakestRival.Models
{
    public sealed class LobbyPlayerItem
    {
        public int AccountId { get; set; }
        public string DisplayName { get; set; } = "Jugador";
        public ImageSource Avatar { get; set; }
        public bool IsMe { get; set; }
        public AvatarAppearance AvatarAppearance { get; set; }
    }
}
