using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WPFTheWeakestRival.Controls;
using WPFTheWeakestRival.Properties;

namespace WPFTheWeakestRival.Windows
{
    public partial class AvatarCustomizationWindow : Window
    {
        private static readonly Color SKIN_LIGHT = (Color)ColorConverter.ConvertFromString("#FFEED8C8");
        private static readonly Color SKIN_FAIR = (Color)ColorConverter.ConvertFromString("#FFE1C3A3");
        private static readonly Color SKIN_TAN = (Color)ColorConverter.ConvertFromString("#FFD4A078");
        private static readonly Color SKIN_BROWN = (Color)ColorConverter.ConvertFromString("#FFC27A4C");
        private static readonly Color SKIN_DARK = (Color)ColorConverter.ConvertFromString("#FF915335");

        private static readonly Color BODY_RED = (Color)ColorConverter.ConvertFromString("#FFD32F2F");
        private static readonly Color BODY_BLUE = (Color)ColorConverter.ConvertFromString("#FF2F6AA3");
        private static readonly Color BODY_GREEN = (Color)ColorConverter.ConvertFromString("#FF4CAF50");
        private static readonly Color BODY_ORANGE = (Color)ColorConverter.ConvertFromString("#FFED4C00");
        private static readonly Color BODY_PURPLE = (Color)ColorConverter.ConvertFromString("#FF9C27B0");
        private static readonly Color BODY_GRAY = (Color)ColorConverter.ConvertFromString("#FF9E9E9E");

        private static readonly Color PANTS_BLACK = (Color)ColorConverter.ConvertFromString("#FF111111");
        private static readonly Color PANTS_DARK_GRAY = (Color)ColorConverter.ConvertFromString("#FF444444");
        private static readonly Color PANTS_JEANS_BLUE = (Color)ColorConverter.ConvertFromString("#FF3F51B5");

        private static readonly Color HAT_BLUE = (Color)ColorConverter.ConvertFromString("#FF2F6AA3");
        private static readonly Color HAT_RED = (Color)ColorConverter.ConvertFromString("#FFD32F2F");
        private static readonly Color HAT_BLACK = (Color)ColorConverter.ConvertFromString("#FF111111");
        private static readonly Color HAT_GREEN = (Color)ColorConverter.ConvertFromString("#FF4CAF50");

        public ObservableCollection<ColorOption> SkinColorOptions { get; } = new ObservableCollection<ColorOption>();
        public ObservableCollection<ColorOption> BodyColorOptions { get; } = new ObservableCollection<ColorOption>();
        public ObservableCollection<ColorOption> PantsColorOptions { get; } = new ObservableCollection<ColorOption>();
        public ObservableCollection<ColorOption> HatColorOptions { get; } = new ObservableCollection<ColorOption>();

        public ObservableCollection<HatTypeOption> HatTypeOptions { get; } = new ObservableCollection<HatTypeOption>();
        public ObservableCollection<FaceTypeOption> FaceTypeOptions { get; } = new ObservableCollection<FaceTypeOption>();

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

            DataContext = this;

            BuildOptions();
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

        private void BuildOptions()
        {
            Func<string, string> R = key => global::WPFTheWeakestRival.Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

            SkinColorOptions.Clear();
            SkinColorOptions.Add(new ColorOption(R("Skin_Light"), SKIN_LIGHT));
            SkinColorOptions.Add(new ColorOption(R("Skin_Fair"), SKIN_FAIR));
            SkinColorOptions.Add(new ColorOption(R("Skin_Tan"), SKIN_TAN));
            SkinColorOptions.Add(new ColorOption(R("Skin_Brown"), SKIN_BROWN));
            SkinColorOptions.Add(new ColorOption(R("Skin_Dark"), SKIN_DARK));

            BodyColorOptions.Clear();
            BodyColorOptions.Add(new ColorOption(R("Color_Red"), BODY_RED));
            BodyColorOptions.Add(new ColorOption(R("Color_Blue"), BODY_BLUE));
            BodyColorOptions.Add(new ColorOption(R("Color_Green"), BODY_GREEN));
            BodyColorOptions.Add(new ColorOption(R("Color_Orange"), BODY_ORANGE));
            BodyColorOptions.Add(new ColorOption(R("Color_Purple"), BODY_PURPLE));
            BodyColorOptions.Add(new ColorOption(R("Color_Gray"), BODY_GRAY));

            PantsColorOptions.Clear();
            PantsColorOptions.Add(new ColorOption(R("Color_Black"), PANTS_BLACK));
            PantsColorOptions.Add(new ColorOption(R("Color_DarkGray"), PANTS_DARK_GRAY));
            PantsColorOptions.Add(new ColorOption(R("Color_Jeans"), PANTS_JEANS_BLUE));

            HatColorOptions.Clear();
            HatColorOptions.Add(new ColorOption(R("HatColor_Blue"), HAT_BLUE));
            HatColorOptions.Add(new ColorOption(R("HatColor_Red"), HAT_RED));
            HatColorOptions.Add(new ColorOption(R("HatColor_Black"), HAT_BLACK));
            HatColorOptions.Add(new ColorOption(R("HatColor_Green"), HAT_GREEN));

            HatTypeOptions.Clear();
            HatTypeOptions.Add(new HatTypeOption(R("Hat_None"), HatType.None));
            HatTypeOptions.Add(new HatTypeOption(R("Hat_Baseball"), HatType.Baseball));
            HatTypeOptions.Add(new HatTypeOption(R("Hat_TopHat"), HatType.TopHat));
            HatTypeOptions.Add(new HatTypeOption(R("Hat_Beanie"), HatType.Beanie));

            FaceTypeOptions.Clear();
            FaceTypeOptions.Add(new FaceTypeOption(R("Face_Neutral"), FaceType.Neutral));
            FaceTypeOptions.Add(new FaceTypeOption(R("Face_Happy"), FaceType.Happy));
            FaceTypeOptions.Add(new FaceTypeOption(R("Face_Angry"), FaceType.Angry));
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

        public sealed class ColorOption
        {
            public string Name { get; }
            public Color Color { get; }
            public SolidColorBrush Brush { get; }

            public ColorOption(string name, Color color)
            {
                Name = string.IsNullOrWhiteSpace(name) ? color.ToString() : name;
                Color = color;

                Brush = new SolidColorBrush(color);
                Brush.Freeze();
            }
        }

        public sealed class HatTypeOption
        {
            public string Name { get; }
            public HatType Value { get; }

            public HatTypeOption(string name, HatType value)
            {
                Name = string.IsNullOrWhiteSpace(name) ? value.ToString() : name;
                Value = value;
            }
        }

        public sealed class FaceTypeOption
        {
            public string Name { get; }
            public FaceType Value { get; }

            public FaceTypeOption(string name, FaceType value)
            {
                Name = string.IsNullOrWhiteSpace(name) ? value.ToString() : name;
                Value = value;
            }
        }
    }
}
