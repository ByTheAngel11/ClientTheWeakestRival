using WPFTheWeakestRival.Models;
using LobbyAvatarDto = WPFTheWeakestRival.LobbyService.AvatarAppearanceDto;

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
    }
}
