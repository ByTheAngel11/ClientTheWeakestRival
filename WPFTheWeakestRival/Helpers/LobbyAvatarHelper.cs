using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using WPFTheWeakestRival.Models;
using WPFTheWeakestRival.Properties.Langs;
using LobbyAccountMini = WPFTheWeakestRival.LobbyService.AccountMini;
using LobbyAvatarDto = WPFTheWeakestRival.LobbyService.AvatarAppearanceDto;

namespace WPFTheWeakestRival.Helpers
{
    public static class LobbyAvatarHelper
    {
        private const int DEFAULT_AVATAR_SIZE = 40;

        private const string PLAYER_NAME_FORMAT = "{0} {1}";

        public static void RebuildLobbyPlayers(
            ObservableCollection<LobbyPlayerItem> targetPlayers,
            LobbyAccountMini[] sourcePlayers,
            Func<LobbyAccountMini, LobbyPlayerItem> mapper)
        {
            if (targetPlayers == null)
            {
                return;
            }

            targetPlayers.Clear();

            if (sourcePlayers == null || sourcePlayers.Length == 0)
            {
                return;
            }

            var seen = new HashSet<int>();

            foreach (LobbyAccountMini account in sourcePlayers)
            {
                if (account == null || account.AccountId <= 0)
                {
                    continue;
                }

                if (!seen.Add(account.AccountId))
                {
                    continue;
                }

                LobbyPlayerItem item = mapper != null
                    ? mapper(account)
                    : BuildFromAccountMini(account);

                if (item == null)
                {
                    continue;
                }

                targetPlayers.Add(item);
            }
        }

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
                    ? BuildDefaultPlayerName(account.AccountId)
                    : account.DisplayName.Trim(),
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

        private static string BuildDefaultPlayerName(int accountId)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                PLAYER_NAME_FORMAT,
                Lang.player,
                accountId);
        }

        private static ImageSource TryReadProfileImage(LobbyAccountMini account)
        {
            if (account == null)
            {
                return null;
            }

            if (!HasProfilePhoto(account))
            {
                return null;
            }

            return UiImageHelper.TryCreateFromProfileCode(account.ProfileImageCode, DEFAULT_AVATAR_SIZE);
        }

        private static bool HasProfilePhoto(LobbyAccountMini account)
        {
            if (account == null)
            {
                return false;
            }

            if (!account.HasProfileImage)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(account.ProfileImageCode);
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
