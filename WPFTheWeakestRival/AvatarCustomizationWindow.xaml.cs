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

        public AvatarCustomizationWindow(
            Color bodyColor,
            Color pantsColor,
            Color skinColor,
            Color hatColor,
            HatType hatType,
            FaceType faceType,
            ImageSource facePhoto)
            : this()
        {
            AvatarPreview.BodyColor = bodyColor;
            AvatarPreview.PantsColor = pantsColor;
            AvatarPreview.SkinColor = skinColor;
            AvatarPreview.HatColor = hatColor;
            AvatarPreview.HatType = hatType;
            AvatarPreview.FaceType = faceType;

            AvatarPreview.FacePhoto = facePhoto;
            AvatarPreview.UseProfilePhotoAsFace = facePhoto != null;
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
