using System;
using System.Windows;
using System.Windows.Media;
using WPFTheWeakestRival.Controls;

namespace WPFTheWeakestRival.Windows
{
    public partial class AvatarCustomizationWindow : Window
    {
        public Color ResultBodyColor => AvatarPreview.BodyColor;
        public Color ResultPantsColor => AvatarPreview.PantsColor;
        public Color ResultSkinColor => AvatarPreview.SkinColor;
        public Color ResultHatColor => AvatarPreview.HatColor;
        public HatType ResultHatType => AvatarPreview.HatType;
        public FaceType ResultFaceType => AvatarPreview.FaceType;
        public bool ResultUseProfilePhotoAsFace => AvatarPreview.UseProfilePhotoAsFace;

        public AvatarCustomizationWindow()
        {
            InitializeComponent();
            InitializeFaceTypeCombo();
        }

        public AvatarCustomizationWindow(AvatarControl sourceAvatar)
            : this()
        {
            if (sourceAvatar == null)
            {
                throw new ArgumentNullException(nameof(sourceAvatar));
            }

            AvatarPreview.BodyColor = sourceAvatar.BodyColor;
            AvatarPreview.PantsColor = sourceAvatar.PantsColor;
            AvatarPreview.SkinColor = sourceAvatar.SkinColor;
            AvatarPreview.HatColor = sourceAvatar.HatColor;
            AvatarPreview.HatType = sourceAvatar.HatType;
            AvatarPreview.FaceType = sourceAvatar.FaceType;
            AvatarPreview.UseProfilePhotoAsFace = sourceAvatar.UseProfilePhotoAsFace;
            AvatarPreview.FacePhoto = sourceAvatar.FacePhoto;
        }

        private void InitializeFaceTypeCombo()
        {
            FaceTypeCombo.Items.Clear();
            foreach (FaceType value in Enum.GetValues(typeof(FaceType)))
            {
                FaceTypeCombo.Items.Add(value);
            }

            if (FaceTypeCombo.SelectedItem == null)
            {
                FaceTypeCombo.SelectedItem = FaceType.Neutral;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
