using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using WPFTheWeakestRival.Helpers;
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

            if (players == null || players.Length == 0)
            {
                collection.Clear();
                return;
            }

            var existingById = new Dictionary<int, int>(collection.Count);
            for (var i = 0; i < collection.Count; i++)
            {
                existingById[collection[i].AccountId] = i;
            }

            var newItemsById = new Dictionary<int, LobbyPlayerItem>(players.Length);
            for (var i = 0; i < players.Length; i++)
            {
                var mappedItem = mapper(players[i]);
                if (mappedItem != null)
                {
                    newItemsById[mappedItem.AccountId] = mappedItem;
                }
            }

            for (var i = collection.Count - 1; i >= 0; i--)
            {
                var accountId = collection[i].AccountId;
                if (!newItemsById.ContainsKey(accountId))
                {
                    collection.RemoveAt(i);
                }
            }

            var insertIndex = 0;
            foreach (var pair in newItemsById)
            {
                var accountId = pair.Key;
                var playerItem = pair.Value;

                if (existingById.TryGetValue(accountId, out var existingIndex))
                {
                    var existingItem = collection[existingIndex];
                    existingItem.DisplayName = playerItem.DisplayName;
                    existingItem.Avatar = playerItem.Avatar;
                    existingItem.AvatarAppearance = playerItem.AvatarAppearance;
                }
                else
                {
                    collection.Insert(insertIndex, playerItem);
                }

                insertIndex++;
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
                ProfileImage = profileImage
            };

            appearance.UseProfilePhotoAsFace = profileImage != null;

            return appearance;
        }
    }
}
