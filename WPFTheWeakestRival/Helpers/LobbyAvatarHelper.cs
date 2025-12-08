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
                    ? $"Jugador {account.AccountId}"
                    : account.DisplayName,
                IsMe = false
            };

            var profileImageSource = UiImageHelper.TryCreateFromUrlOrPath(
                account.AvatarUrl,
                DEFAULT_AVATAR_SIZE);

            playerItem.Avatar = profileImageSource
                                ?? UiImageHelper.DefaultAvatar(DEFAULT_AVATAR_SIZE);

            playerItem.AvatarAppearance = MapAvatarAppearance(
                account.Avatar,
                playerItem.Avatar);

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

        private static AvatarAppearance MapAvatarAppearance(
            LobbyAvatarDto dto,
            ImageSource profileImage)
        {
            if (dto == null)
            {
                return null;
            }

            var appearance = new AvatarAppearance
            {
                BodyColor = (int)dto.BodyColor,
                PantsColor = (int)dto.PantsColor,
                HatType = (int)dto.HatType,
                HatColor = (int)dto.HatColor,
                FaceType = (int)dto.FaceType,
                ProfileImage = profileImage,
                UseProfilePhotoAsFace = profileImage != null
            };

            return appearance;
        }
    }
}
