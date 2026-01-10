using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using WPFTheWeakestRival.Models;
using LobbyAccountMini = WPFTheWeakestRival.LobbyService.AccountMini;
using LobbyAvatarDto = WPFTheWeakestRival.LobbyService.AvatarAppearanceDto;

namespace WPFTheWeakestRival.Helpers
{
    public static class LobbyAvatarHelper
    {
        private const int DEFAULT_AVATAR_SIZE = 40;

        public static LobbyPlayerItem BuildFromAccountMini(LobbyAccountMini account)
        {
            if (account == null)
            {
                return null;
            }

            var playerItem = new LobbyPlayerItem
            {
                AccountId = account.AccountId,
                DisplayName = string.IsNullOrWhiteSpace(account.DisplayName)
                    ? string.Format("Jugador {0}", account.AccountId)
                    : account.DisplayName,
                IsMe = false
            };

            bool hasProfilePhoto = HasProfilePhoto(account);

            ImageSource profileImageSource =
                TryReadProfileImage(account) ??
                UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

            playerItem.Avatar = profileImageSource;

            playerItem.AvatarAppearance = MapAvatarAppearance(
                account.Avatar,
                profileImageSource,
                hasProfilePhoto);

            return playerItem;
        }

        public static void RebuildLobbyPlayers(
            ObservableCollection<LobbyPlayerItem> collection,
            LobbyAccountMini[] players,
            Func<LobbyAccountMini, LobbyPlayerItem> mapper)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            collection.Clear();

            if (players == null || players.Length == 0)
            {
                return;
            }

            foreach (var player in players)
            {
                var item = mapper(player);
                if (item != null)
                {
                    collection.Add(item);
                }
            }
        }

        private static ImageSource TryReadProfileImage(LobbyAccountMini account)
        {
            if (account == null)
            {
                return null;
            }

            return UiImageHelper.TryCreateFromBytes(account.AvatarBytes, DEFAULT_AVATAR_SIZE);
        }

        private static bool HasProfilePhoto(LobbyAccountMini account)
        {
            if (account == null)
            {
                return false;
            }

            byte[] bytes = account.AvatarBytes;
            return bytes != null && bytes.Length > 0;
        }

        private static AvatarAppearance MapAvatarAppearance(
            LobbyAvatarDto dto,
            ImageSource profileImage,
            bool hasProfilePhoto)
        {
            if (dto == null)
            {
                return null;
            }

            return new AvatarAppearance
            {
                BodyColor = (int)dto.BodyColor,
                PantsColor = (int)dto.PantsColor,
                HatType = (int)dto.HatType,
                HatColor = (int)dto.HatColor,
                FaceType = (int)dto.FaceType,
                ProfileImage = profileImage,
                UseProfilePhotoAsFace = dto.UseProfilePhotoAsFace && hasProfilePhoto
            };
        }
    }
}
