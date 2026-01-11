using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Controls
{
    public enum HatType
    {
        None,
        Beanie,
        Baseball,
        TopHat
    }

    public enum FaceType
    {
        Neutral,
        Happy,
        Angry
    }

    public partial class AvatarControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private const double BODY_SHADE_INTENSITY = 0.14;
        private const double PANTS_SHADE_INTENSITY = 0.18;
        private const double HAT_SHADE_INTENSITY = 0.12;
        private const double HAT_RIM_SHADE_INTENSITY = 0.06;

        private const double SHADE_INTENSITY_MIN = 0d;
        private const double SHADE_INTENSITY_MAX = 1d;
        private const double SHADE_FACTOR_BASE = 1d;

        private const int BODY_COLOR_INDEX_RED = 0;
        private const int BODY_COLOR_INDEX_BLUE = 1;
        private const int BODY_COLOR_INDEX_GREEN = 2;
        private const int BODY_COLOR_INDEX_ORANGE = 3;
        private const int BODY_COLOR_INDEX_PURPLE = 4;
        private const int BODY_COLOR_INDEX_GRAY = 5;

        private const int PANTS_COLOR_INDEX_BLACK = 0;
        private const int PANTS_COLOR_INDEX_DARK_GRAY = 1;
        private const int PANTS_COLOR_INDEX_BLUE_JEANS = 2;

        private const int HAT_COLOR_INDEX_DEFAULT = 0;
        private const int HAT_COLOR_INDEX_RED = 1;
        private const int HAT_COLOR_INDEX_BLUE = 2;
        private const int HAT_COLOR_INDEX_BLACK = 3;

        private const int HAT_TYPE_INDEX_NONE = 0;
        private const int HAT_TYPE_INDEX_BASEBALL = 1;
        private const int HAT_TYPE_INDEX_TOP_HAT = 2;
        private const int HAT_TYPE_INDEX_BEANIE = 3;

        private const int FACE_TYPE_INDEX_NEUTRAL = 0;
        private const int FACE_TYPE_INDEX_ANGRY = 1;
        private const int FACE_TYPE_INDEX_HAPPY = 2;
        private const int FACE_TYPE_INDEX_NEUTRAL_ALT = 3;

        private const string COLOR_HEX_BLUE_2F6AA3 = "#FF2F6AA3";

        private static readonly Color DEFAULT_BODY_COLOR = ColorFromHex("#FFED4C00");
        private static readonly Color DEFAULT_PANTS_COLOR = ColorFromHex("#FF111111");
        private static readonly Color DEFAULT_SKIN_COLOR = ColorFromHex("#FFEED8C8");
        private static readonly Color DEFAULT_HAT_COLOR = ColorFromHex(COLOR_HEX_BLUE_2F6AA3);

        private static readonly Color BODY_COLOR_RED = ColorFromHex("#FFD32F2F");
        private static readonly Color BODY_COLOR_BLUE = ColorFromHex(COLOR_HEX_BLUE_2F6AA3);
        private static readonly Color BODY_COLOR_GREEN = ColorFromHex("#FF4CAF50");
        private static readonly Color BODY_COLOR_ORANGE = ColorFromHex("#FFED4C00");
        private static readonly Color BODY_COLOR_PURPLE = ColorFromHex("#FF9C27B0");
        private static readonly Color BODY_COLOR_GRAY = ColorFromHex("#FF9E9E9E");

        private static readonly Color PANTS_COLOR_BLACK = ColorFromHex("#FF111111");
        private static readonly Color PANTS_COLOR_DARK_GRAY = ColorFromHex("#FF444444");
        private static readonly Color PANTS_COLOR_BLUE_JEANS = ColorFromHex("#FF3F51B5");

        private static readonly Color HAT_COLOR_DEFAULT = ColorFromHex(COLOR_HEX_BLUE_2F6AA3);
        private static readonly Color HAT_COLOR_RED = ColorFromHex("#FFD32F2F");
        private static readonly Color HAT_COLOR_BLUE = ColorFromHex(COLOR_HEX_BLUE_2F6AA3);
        private static readonly Color HAT_COLOR_BLACK = ColorFromHex("#FF111111");

        public static readonly DependencyProperty BodyColorProperty =
            DependencyProperty.Register(
                nameof(BodyColor),
                typeof(Color),
                typeof(AvatarControl),
                new PropertyMetadata(DEFAULT_BODY_COLOR, OnDependencyPropertyChanged));

        public static readonly DependencyProperty PantsColorProperty =
            DependencyProperty.Register(
                nameof(PantsColor),
                typeof(Color),
                typeof(AvatarControl),
                new PropertyMetadata(DEFAULT_PANTS_COLOR, OnDependencyPropertyChanged));

        public static readonly DependencyProperty SkinColorProperty =
            DependencyProperty.Register(
                nameof(SkinColor),
                typeof(Color),
                typeof(AvatarControl),
                new PropertyMetadata(DEFAULT_SKIN_COLOR, OnDependencyPropertyChanged));

        public static readonly DependencyProperty HatColorProperty =
            DependencyProperty.Register(
                nameof(HatColor),
                typeof(Color),
                typeof(AvatarControl),
                new PropertyMetadata(DEFAULT_HAT_COLOR, OnDependencyPropertyChanged));

        public static readonly DependencyProperty HatTypeProperty =
            DependencyProperty.Register(
                nameof(HatType),
                typeof(HatType),
                typeof(AvatarControl),
                new PropertyMetadata(HatType.None, OnDependencyPropertyChanged));

        public static readonly DependencyProperty FaceTypeProperty =
            DependencyProperty.Register(
                nameof(FaceType),
                typeof(FaceType),
                typeof(AvatarControl),
                new PropertyMetadata(FaceType.Neutral, OnDependencyPropertyChanged));

        public static readonly DependencyProperty UseProfilePhotoAsFaceProperty =
            DependencyProperty.Register(
                nameof(UseProfilePhotoAsFace),
                typeof(bool),
                typeof(AvatarControl),
                new PropertyMetadata(false, OnDependencyPropertyChanged));

        public static readonly DependencyProperty FacePhotoProperty =
            DependencyProperty.Register(
                nameof(FacePhoto),
                typeof(ImageSource),
                typeof(AvatarControl),
                new PropertyMetadata(null, OnDependencyPropertyChanged));

        public static readonly DependencyProperty AppearanceProperty =
            DependencyProperty.Register(
                nameof(Appearance),
                typeof(AvatarAppearance),
                typeof(AvatarControl),
                new PropertyMetadata(null, OnAppearanceChanged));

        public static readonly DependencyProperty ProfileImageProperty =
            DependencyProperty.Register(
                nameof(ProfileImage),
                typeof(ImageSource),
                typeof(AvatarControl),
                new PropertyMetadata(null, OnProfileImageChanged));

        private Brush skinBrush;
        private Brush torsoBrush;
        private Brush pantsBrush;
        private Brush hatBrush;
        private Brush hatRimBrush;
        private Brush facePhotoBrush = Brushes.Transparent;
        private bool isHatVisible;
        private bool isFaceUsingPhoto;

        public event PropertyChangedEventHandler PropertyChanged;

        public AvatarControl()
        {
            InitializeComponent();
            RebuildAll();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        public Color BodyColor
        {
            get => (Color)GetValue(BodyColorProperty);
            set => SetValue(BodyColorProperty, value);
        }

        public Color PantsColor
        {
            get => (Color)GetValue(PantsColorProperty);
            set => SetValue(PantsColorProperty, value);
        }

        public Color SkinColor
        {
            get => (Color)GetValue(SkinColorProperty);
            set => SetValue(SkinColorProperty, value);
        }

        public Color HatColor
        {
            get => (Color)GetValue(HatColorProperty);
            set => SetValue(HatColorProperty, value);
        }

        public HatType HatType
        {
            get => (HatType)GetValue(HatTypeProperty);
            set => SetValue(HatTypeProperty, value);
        }

        public FaceType FaceType
        {
            get => (FaceType)GetValue(FaceTypeProperty);
            set => SetValue(FaceTypeProperty, value);
        }

        public bool UseProfilePhotoAsFace
        {
            get => (bool)GetValue(UseProfilePhotoAsFaceProperty);
            set => SetValue(UseProfilePhotoAsFaceProperty, value);
        }

        public ImageSource FacePhoto
        {
            get => (ImageSource)GetValue(FacePhotoProperty);
            set => SetValue(FacePhotoProperty, value);
        }

        public ImageSource ProfileImage
        {
            get => (ImageSource)GetValue(ProfileImageProperty);
            set => SetValue(ProfileImageProperty, value);
        }

        public AvatarAppearance Appearance
        {
            get => (AvatarAppearance)GetValue(AppearanceProperty);
            set => SetValue(AppearanceProperty, value);
        }

        public Brush SkinBrush
        {
            get => skinBrush;
            private set
            {
                skinBrush = value;
                OnPropertyChanged();
            }
        }

        public Brush TorsoBrush
        {
            get => torsoBrush;
            private set
            {
                torsoBrush = value;
                OnPropertyChanged();
            }
        }

        public Brush PantsBrush
        {
            get => pantsBrush;
            private set
            {
                pantsBrush = value;
                OnPropertyChanged();
            }
        }

        public Brush HatBrush
        {
            get => hatBrush;
            private set
            {
                hatBrush = value;
                OnPropertyChanged();
            }
        }

        public Brush HatRimBrush
        {
            get => hatRimBrush;
            private set
            {
                hatRimBrush = value;
                OnPropertyChanged();
            }
        }

        public Brush FacePhotoBrush
        {
            get => facePhotoBrush;
            private set
            {
                facePhotoBrush = value;
                OnPropertyChanged();
            }
        }

        public bool IsHatVisible
        {
            get => isHatVisible;
            private set
            {
                isHatVisible = value;
                OnPropertyChanged();
            }
        }

        public bool IsFaceUsingPhoto
        {
            get => isFaceUsingPhoto;
            private set
            {
                isFaceUsingPhoto = value;
                OnPropertyChanged();
            }
        }

        private static void OnDependencyPropertyChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArgs)
        {
            if (dependencyObject is AvatarControl control)
            {
                control.RebuildAll();
            }
        }

        private static void OnAppearanceChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArgs)
        {
            if (!(dependencyObject is AvatarControl control))
            {
                return;
            }

            var appearance = eventArgs.NewValue as AvatarAppearance;
            if (appearance == null)
            {
                control.UseProfilePhotoAsFace = false;
                control.FacePhoto = null;
                control.RebuildAll();
                return;
            }

            control.ApplyAppearance(appearance);
        }

        private static void OnProfileImageChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArgs)
        {
            if (!(dependencyObject is AvatarControl control))
            {
                return;
            }

            var imageSource = eventArgs.NewValue as ImageSource;

            control.FacePhoto = imageSource;
            control.RebuildAll();
        }

        private void RebuildAll()
        {
            UpdateBrushes();
            UpdateHatVisibility();
            UpdateFaceBrush();
            UpdateHatVariants();
            UpdateFaceVariants();
        }

        private void UpdateBrushes()
        {
            SkinBrush = new SolidColorBrush(SkinColor);
            TorsoBrush = BuildBodyShaded(BodyColor);
            PantsBrush = BuildPantsShaded(PantsColor);
            HatBrush = BuildHatShaded(HatColor);
            HatRimBrush = BuildHatRimShaded(HatColor);
        }

        private static LinearGradientBrush BuildBodyShaded(Color baseColor)
        {
            return BuildShadedInternal(baseColor, BODY_SHADE_INTENSITY);
        }

        private static LinearGradientBrush BuildPantsShaded(Color baseColor)
        {
            return BuildShadedInternal(baseColor, PANTS_SHADE_INTENSITY);
        }

        private static LinearGradientBrush BuildHatShaded(Color baseColor)
        {
            return BuildShadedInternal(baseColor, HAT_SHADE_INTENSITY);
        }

        private static LinearGradientBrush BuildHatRimShaded(Color baseColor)
        {
            return BuildShadedInternal(baseColor, HAT_RIM_SHADE_INTENSITY);
        }

        private void UpdateHatVisibility()
        {
            IsHatVisible = HatType != HatType.None;
        }

        private void UpdateFaceBrush()
        {
            IsFaceUsingPhoto = UseProfilePhotoAsFace && FacePhoto != null;

            Brush faceBrush = Brushes.Transparent;
            if (IsFaceUsingPhoto && FacePhoto != null)
            {
                faceBrush = new ImageBrush(FacePhoto)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
            }

            FacePhotoBrush = faceBrush;
        }

        private void UpdateHatVariants()
        {
            var beanie = (System.Windows.Controls.Grid)FindName("HatBeanie");
            var cap = (System.Windows.Controls.Grid)FindName("HatCap");
            var top = (System.Windows.Controls.Grid)FindName("HatTop");

            SetElementOpacity(beanie, HatType == HatType.Beanie);
            SetElementOpacity(cap, HatType == HatType.Baseball);
            SetElementOpacity(top, HatType == HatType.TopHat);
        }

        private void UpdateFaceVariants()
        {
            var mouthNeutral = (System.Windows.Shapes.Rectangle)FindName("MouthNeutral");
            var mouthHappy = (System.Windows.Shapes.Path)FindName("MouthHappy");
            var mouthAngry = (System.Windows.Shapes.Path)FindName("MouthAngry");
            var eyeLeftNeutral = (System.Windows.Shapes.Ellipse)FindName("EyeLeftNeutral");
            var eyeRightNeutral = (System.Windows.Shapes.Ellipse)FindName("EyeRightNeutral");
            var eyeLeftHappy = (System.Windows.Shapes.Ellipse)FindName("EyeLeftHappy");
            var eyeRightHappy = (System.Windows.Shapes.Ellipse)FindName("EyeRightHappy");

            var isNeutral = FaceType == FaceType.Neutral;
            var isHappy = FaceType == FaceType.Happy;
            var isAngry = FaceType == FaceType.Angry;

            SetElementOpacity(mouthNeutral, isNeutral);
            SetElementOpacity(mouthHappy, isHappy);
            SetElementOpacity(mouthAngry, isAngry);

            SetElementOpacity(eyeLeftNeutral, isNeutral);
            SetElementOpacity(eyeRightNeutral, isNeutral);

            SetElementOpacity(eyeLeftHappy, isHappy);
            SetElementOpacity(eyeRightHappy, isHappy);
        }

        private static void SetElementOpacity(UIElement element, bool isVisible)
        {
            if (element == null)
            {
                return;
            }

            element.Opacity = isVisible ? 1 : 0;
        }

        private static LinearGradientBrush BuildShadedInternal(Color baseColor, double shadeIntensity)
        {
            double clampedShade = ClampShadeIntensity(shadeIntensity);

            var darker = Color.FromArgb(
                baseColor.A,
                DarkenComponent(baseColor.R, clampedShade),
                DarkenComponent(baseColor.G, clampedShade),
                DarkenComponent(baseColor.B, clampedShade));

            return new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(darker, 0),
                    new GradientStop(baseColor, 1)
                },
                new Point(0, 0),
                new Point(0, 1));
        }

        private static double ClampShadeIntensity(double shadeIntensity)
        {
            if (shadeIntensity < SHADE_INTENSITY_MIN)
            {
                return SHADE_INTENSITY_MIN;
            }

            if (shadeIntensity > SHADE_INTENSITY_MAX)
            {
                return SHADE_INTENSITY_MAX;
            }

            return shadeIntensity;
        }

        private static byte DarkenComponent(byte component, double shadeIntensity)
        {
            double shadedValue = component * (SHADE_FACTOR_BASE - shadeIntensity);

            if (shadedValue < byte.MinValue)
            {
                return byte.MinValue;
            }

            if (shadedValue > byte.MaxValue)
            {
                return byte.MaxValue;
            }

            return (byte)shadedValue;
        }

        private static Color ColorFromHex(string hexValue)
        {
            return (Color)ColorConverter.ConvertFromString(hexValue);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ApplyAppearance(AvatarAppearance avatar)
        {
            if (avatar == null)
            {
                return;
            }

            ImageSource avatarProfileImage = avatar.ProfileImage ?? ProfileImage ?? FacePhoto;

            bool shouldUsePhoto = avatar.UseProfilePhotoAsFace && avatarProfileImage != null;
            ImageSource faceImage = shouldUsePhoto ? avatarProfileImage : null;

            SetAppearance(
                SkinColor,
                MapBodyColor(avatar.BodyColor),
                MapPantsColor(avatar.PantsColor),
                MapHatType(avatar.HatType),
                MapHatColor(avatar.HatColor),
                MapFaceType(avatar.FaceType),
                faceImage);
        }

        private Color MapBodyColor(int index)
        {
            switch (index)
            {
                case BODY_COLOR_INDEX_RED:
                    return BODY_COLOR_RED;
                case BODY_COLOR_INDEX_BLUE:
                    return BODY_COLOR_BLUE;
                case BODY_COLOR_INDEX_GREEN:
                    return BODY_COLOR_GREEN;
                case BODY_COLOR_INDEX_ORANGE:
                    return BODY_COLOR_ORANGE;
                case BODY_COLOR_INDEX_PURPLE:
                    return BODY_COLOR_PURPLE;
                case BODY_COLOR_INDEX_GRAY:
                    return BODY_COLOR_GRAY;
                default:
                    return BodyColor;
            }
        }

        private Color MapPantsColor(int index)
        {
            switch (index)
            {
                case PANTS_COLOR_INDEX_BLACK:
                    return PANTS_COLOR_BLACK;
                case PANTS_COLOR_INDEX_DARK_GRAY:
                    return PANTS_COLOR_DARK_GRAY;
                case PANTS_COLOR_INDEX_BLUE_JEANS:
                    return PANTS_COLOR_BLUE_JEANS;
                default:
                    return PantsColor;
            }
        }

        private Color MapHatColor(int index)
        {
            switch (index)
            {
                case HAT_COLOR_INDEX_DEFAULT:
                    return HAT_COLOR_DEFAULT;
                case HAT_COLOR_INDEX_RED:
                    return HAT_COLOR_RED;
                case HAT_COLOR_INDEX_BLUE:
                    return HAT_COLOR_BLUE;
                case HAT_COLOR_INDEX_BLACK:
                    return HAT_COLOR_BLACK;
                default:
                    return HatColor;
            }
        }

        private static HatType MapHatType(int index)
        {
            switch (index)
            {
                case HAT_TYPE_INDEX_NONE:
                    return HatType.None;
                case HAT_TYPE_INDEX_BASEBALL:
                    return HatType.Baseball;
                case HAT_TYPE_INDEX_TOP_HAT:
                    return HatType.TopHat;
                case HAT_TYPE_INDEX_BEANIE:
                    return HatType.Beanie;
                default:
                    return HatType.None;
            }
        }

        private static FaceType MapFaceType(int index)
        {
            switch (index)
            {
                case FACE_TYPE_INDEX_NEUTRAL:
                    return FaceType.Neutral;
                case FACE_TYPE_INDEX_ANGRY:
                    return FaceType.Angry;
                case FACE_TYPE_INDEX_HAPPY:
                    return FaceType.Happy;
                case FACE_TYPE_INDEX_NEUTRAL_ALT:
                    return FaceType.Neutral;
                default:
                    return FaceType.Neutral;
            }
        }

        public void SetAppearance(
            Color skinColor,
            Color bodyColor,
            Color pantsColor,
            HatType hatType,
            Color hatColor,
            FaceType faceType,
            ImageSource facePhoto = null)
        {
            SkinColor = skinColor;
            BodyColor = bodyColor;
            PantsColor = pantsColor;
            HatType = hatType;
            HatColor = hatColor;
            FaceType = faceType;

            FacePhoto = facePhoto;
            UseProfilePhotoAsFace = facePhoto != null;
        }
    }
}
