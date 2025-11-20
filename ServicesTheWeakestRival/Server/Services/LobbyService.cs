            // 3) Avatar de cada user (UserAvatar + MapAvatar)
            var avatarSql = new UserAvatarSql(Connection);
            foreach (var id in userIds)
            {
                if (!result.TryGetValue(id, out var mini))
                {
                    mini = new AccountMini
                    {
                        AccountId = id,
                        DisplayName = "Jugador " + id
                    };
                    result[id] = mini;
                }

                var avatarEntity = avatarSql.GetByUserId(id);
                
                // Si no hay avatar guardado, crear uno por defecto
                if (avatarEntity == null)
                {
                    avatarEntity = new UserAvatarEntity
                    {
                        UserId = id,
                        BodyColor = 0,      // Red (AvatarBodyColor.Red)
                        PantsColor = 0,     // Black (AvatarPantsColor.Black)
                        HatType = 0,        // None (AvatarHatType.None)
                        HatColor = 0,       // Default (AvatarHatColor.Default)
                        FaceType = 0,       // Default (AvatarFaceType.Default)
                        UseProfilePhoto = false
                    };
                }
                
                mini.Avatar = MapAvatar(avatarEntity);
            }