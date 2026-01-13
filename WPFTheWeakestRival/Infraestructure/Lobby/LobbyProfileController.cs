using log4net;
using System;
using System.ServiceModel;
using System.Windows.Controls;
using WPFTheWeakestRival.Helpers;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyProfileController
    {
        private const int DefaultAvatarSize = 80;
        private const string DefaultDisplayName = "Yo";

        private readonly LobbyUiDispatcher ui;
        private readonly LobbyRuntimeState state;
        private readonly Image avatarImage;
        private readonly ILog logger;

        internal LobbyProfileController(
            LobbyUiDispatcher ui,
            LobbyRuntimeState state,
            Image avatarImage,
            ILog logger)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.avatarImage = avatarImage;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal void RefreshAvatar()
        {
            var token = LoginWindow.AppSession.CurrentToken?.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                SetDefaultAvatar();
                state.MyDisplayName = DefaultDisplayName;
                return;
            }

            try
            {
                var myProfile = AppServices.Lobby.GetMyProfile(token);

                state.MyDisplayName = string.IsNullOrWhiteSpace(myProfile?.DisplayName)
                    ? DefaultDisplayName
                    : myProfile.DisplayName;

                byte[] avatarBytes = TryGetProfileBytes(myProfile);

                var avatarImageSource =
                    UiImageHelper.TryCreateFromBytes(avatarBytes, DefaultAvatarSize) ??
                    UiImageHelper.DefaultAvatar(DefaultAvatarSize);

                ui.Ui(() =>
                {
                    if (avatarImage != null)
                    {
                        avatarImage.Source = avatarImageSource;
                    }
                });
            }
            catch (FaultException<LobbyService.ServiceFault> ex)
            {
                logger.Warn("Lobby fault while refreshing avatar.", ex);
                SetDefaultAvatar();
            }
            catch (CommunicationException ex)
            {
                logger.Warn("Communication error while refreshing avatar.", ex);
                SetDefaultAvatar();
            }
            catch (Exception ex)
            {
                logger.Warn("Unexpected error while refreshing avatar.", ex);
                SetDefaultAvatar();
            }
        }

        private void SetDefaultAvatar()
        {
            var defaultAvatar = UiImageHelper.DefaultAvatar(DefaultAvatarSize);

            ui.Ui(() =>
            {
                if (avatarImage != null)
                {
                    avatarImage.Source = defaultAvatar;
                }
            });
        }

        private static byte[] TryGetProfileBytes(object profile)
        {
            if (profile == null)
            {
                return Array.Empty<byte>();
            }

            try
            {
                var type = profile.GetType();

                var prop =
                    type.GetProperty("AvatarBytes") ??
                    type.GetProperty("ProfileImageBytes") ??
                    type.GetProperty("ProfilePhotoBytes") ??
                    type.GetProperty("PhotoBytes");

                if (prop == null || prop.PropertyType != typeof(byte[]))
                {
                    return Array.Empty<byte>();
                }

                var value = prop.GetValue(profile, null) as byte[];
                return value ?? Array.Empty<byte>();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }
    }
}
