using System.Windows.Media;
using WPFTheWeakestRival.Controls;
using WPFTheWeakestRival.Models;
using LobbyAvatarDto = WPFTheWeakestRival.LobbyService.AvatarAppearanceDto;
using GameplayAvatarDto = WPFTheWeakestRival.GameplayService.AvatarAppearanceDto;


namespace WPFTheWeakestRival.Helpers
{
    public static class AvatarMapper
    {
        public static AvatarAppearance FromLobbyDto(LobbyAvatarDto dto)
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
                UseProfilePhotoAsFace = dto.UseProfilePhotoAsFace
            };
        }

        public static AvatarAppearance FromGameplayDto(GameplayAvatarDto dto)
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
                UseProfilePhotoAsFace = dto.UseProfilePhotoAsFace
            };
        }


        private static readonly Color BODY_RED = (Color)ColorConverter.ConvertFromString("#FFD32F2F");
        private static readonly Color BODY_BLUE = (Color)ColorConverter.ConvertFromString("#FF2F6AA3");
        private static readonly Color BODY_GREEN = (Color)ColorConverter.ConvertFromString("#FF4CAF50");
        private static readonly Color BODY_ORANGE = (Color)ColorConverter.ConvertFromString("#FFED4C00");
        private static readonly Color BODY_PURPLE = (Color)ColorConverter.ConvertFromString("#FF9C27B0");
        private static readonly Color BODY_GRAY = (Color)ColorConverter.ConvertFromString("#FF9E9E9E");

        private static readonly Color PANTS_BLACK = (Color)ColorConverter.ConvertFromString("#FF111111");
        private static readonly Color PANTS_DARK_GRAY = (Color)ColorConverter.ConvertFromString("#FF444444");
        private static readonly Color PANTS_BLUE = (Color)ColorConverter.ConvertFromString("#FF3F51B5");

        private static readonly Color HAT_BLUE = (Color)ColorConverter.ConvertFromString("#FF2F6AA3");
        private static readonly Color HAT_RED = (Color)ColorConverter.ConvertFromString("#FFD32F2F");
        private static readonly Color HAT_BLACK = (Color)ColorConverter.ConvertFromString("#FF111111");

        public static int GetBodyColorIndex(Color color)
        {
            if (color == BODY_RED) return 0;
            if (color == BODY_BLUE) return 1;
            if (color == BODY_GREEN) return 2;
            if (color == BODY_ORANGE) return 3;
            if (color == BODY_PURPLE) return 4;
            if (color == BODY_GRAY) return 5;
            return 0;
        }

        public static int GetPantsColorIndex(Color color)
        {
            if (color == PANTS_BLACK) return 0;
            if (color == PANTS_DARK_GRAY) return 1;
            if (color == PANTS_BLUE) return 2;
            return 0;
        }

        public static int GetHatColorIndex(Color color)
        {
            if (color == HAT_BLUE) return 0;
            if (color == HAT_RED) return 1;
            if (color == HAT_BLACK) return 3;
            return 0;
        }

        public static int GetHatTypeIndex(HatType hatType)
        {
            switch (hatType)
            {
                case HatType.None:
                    return 0;
                case HatType.Baseball:
                    return 1;
                case HatType.TopHat:
                    return 2;
                case HatType.Beanie:
                    return 3;
                default:
                    return 0;
            }
        }

        public static int GetFaceTypeIndex(FaceType faceType)
        {
            switch (faceType)
            {
                case FaceType.Neutral:
                    return 0;
                case FaceType.Angry:
                    return 1;
                case FaceType.Happy:
                    return 2;
                default:
                    return 0;
            }
        }
    }
}
