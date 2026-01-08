using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WPFTheWeakestRival.Models
{
    public sealed class FriendItem : INotifyPropertyChanged
    {
        private string displayName = string.Empty;
        private string statusText = string.Empty;
        private string presence = "Offline";
        private ImageSource avatar;
        private bool isOnline;
        private int accountId;

        public int AccountId
        {
            get => accountId;
            set => Set(ref accountId, value);
        }

        public string DisplayName
        {
            get => displayName;
            set => Set(ref displayName, value);
        }

        public string StatusText
        {
            get => statusText;
            set => Set(ref statusText, value);
        }

        public string Presence
        {
            get => presence;
            set => Set(ref presence, value);
        }

        public ImageSource Avatar
        {
            get => avatar;
            set => Set(ref avatar, value);
        }

        public bool IsOnline
        {
            get => isOnline;
            set => Set(ref isOnline, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
