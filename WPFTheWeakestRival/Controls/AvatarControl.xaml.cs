using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using WPFTheWeakestRival.Models;

namespace WPFTheWeakestRival.Controls
{
    public enum HatType { None, Beanie, Baseball, TopHat }
    public enum FaceType { Neutral, Happy, Angry }

    public partial class AvatarControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        // ===================== Dependency Properties =====================
        public static readonly DependencyProperty BodyColorProperty =
            DependencyProperty.Register(nameof(BodyColor), typeof(Color), typeof(AvatarControl),
                new PropertyMetadata(ColorFromHex("#FFED4C00"), OnDpChanged));

        public static readonly DependencyProperty PantsColorProperty =
            DependencyProperty.Register(nameof(PantsColor), typeof(Color), typeof(AvatarControl),
                new PropertyMetadata(ColorFromHex("#FF111111"), OnDpChanged));

        public static readonly DependencyProperty SkinColorProperty =
            DependencyProperty.Register(nameof(SkinColor), typeof(Color), typeof(AvatarControl),
                new PropertyMetadata(ColorFromHex("#FFEED8C8"), OnDpChanged));

        public static readonly DependencyProperty HatColorProperty =
            DependencyProperty.Register(nameof(HatColor), typeof(Color), typeof(AvatarControl),
                new PropertyMetadata(ColorFromHex("#FF2F6AA3"), OnDpChanged));

        public static readonly DependencyProperty HatTypeProperty =
            DependencyProperty.Register(nameof(HatType), typeof(HatType), typeof(AvatarControl),
                new PropertyMetadata(HatType.None, OnDpChanged));

        public static readonly DependencyProperty FaceTypeProperty =
            DependencyProperty.Register(nameof(FaceType), typeof(FaceType), typeof(AvatarControl),
                new PropertyMetadata(FaceType.Neutral, OnDpChanged));

        public static readonly DependencyProperty UseProfilePhotoAsFaceProperty =
            DependencyProperty.Register(nameof(UseProfilePhotoAsFace), typeof(bool), typeof(AvatarControl),
                new PropertyMetadata(false, OnDpChanged));

        public static readonly DependencyProperty FacePhotoProperty =
            DependencyProperty.Register(nameof(FacePhoto), typeof(ImageSource), typeof(AvatarControl),
                new PropertyMetadata(null, OnDpChanged));

        // ===================== .NET Properties =====================
        public Color BodyColor { get => (Color)GetValue(BodyColorProperty); set => SetValue(BodyColorProperty, value); }
        public Color PantsColor { get => (Color)GetValue(PantsColorProperty); set => SetValue(PantsColorProperty, value); }
        public Color SkinColor { get => (Color)GetValue(SkinColorProperty); set => SetValue(SkinColorProperty, value); }
        public Color HatColor { get => (Color)GetValue(HatColorProperty); set => SetValue(HatColorProperty, value); }
        public HatType HatType { get => (HatType)GetValue(HatTypeProperty); set => SetValue(HatTypeProperty, value); }
        public FaceType FaceType { get => (FaceType)GetValue(FaceTypeProperty); set => SetValue(FaceTypeProperty, value); }
        public bool UseProfilePhotoAsFace { get => (bool)GetValue(UseProfilePhotoAsFaceProperty); set => SetValue(UseProfilePhotoAsFaceProperty, value); }
        public ImageSource FacePhoto { get => (ImageSource)GetValue(FacePhotoProperty); set => SetValue(FacePhotoProperty, value); }

        // New: expose a friendly ProfileImage property used by other windows
        public ImageSource ProfileImage
        {
            get => FacePhoto;
            set
            {
                FacePhoto = value;
                // if an Appearance is already set, re-apply so UseProfilePhotoAsFace is respected
                if (Appearance != null)
                {
                    ApplyAppearance(Appearance);
                }
            }
        }

        private AvatarAppearance appearance;
        public AvatarAppearance Appearance
        {
            get => appearance;
            set
            {
                appearance = value;
                if (appearance == null)
                {
                    // Reset to defaults
                    // keep current FacePhoto but disable photo use
                    UseProfilePhotoAsFace = false;
                    RebuildAll();
                }
                else
                {
                    ApplyAppearance(appearance);
                }
            }
        }

        // ===================== Brushes para XAML =====================
        private Brush skinBrush;
        public Brush SkinBrush { get => skinBrush; private set { skinBrush = value; OnPropertyChanged(); } }

        private Brush torsoBrush;
        public Brush TorsoBrush { get => torsoBrush; private set { torsoBrush = value; OnPropertyChanged(); } }

        private Brush pantsBrush;
        public Brush PantsBrush { get => pantsBrush; private set { pantsBrush = value; OnPropertyChanged(); } }

        private Brush hatBrush;
        public Brush HatBrush { get => hatBrush; private set { hatBrush = value; OnPropertyChanged(); } }

        private Brush hatRimBrush;
        public Brush HatRimBrush { get => hatRimBrush; private set { hatRimBrush = value; OnPropertyChanged(); } }

        private Brush facePhotoBrush = Brushes.Transparent;
        public Brush FacePhotoBrush { get => facePhotoBrush; private set { facePhotoBrush = value; OnPropertyChanged(); } }

        // Estados derivados que se usan en bindings
        private bool hatVisible;
        public bool HatVisible { get => hatVisible; private set { hatVisible = value; OnPropertyChanged(); } }

        private bool faceUsesPhoto;
        public bool FaceUsesPhoto { get => faceUsesPhoto; private set { faceUsesPhoto = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;

        public AvatarControl()
        {
            InitializeComponent();
            RebuildAll();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        private static void OnDpChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            if (d is AvatarControl c) c.RebuildAll();
        }

        private void RebuildAll()
        {
            // Brushes
            SkinBrush = new SolidColorBrush(SkinColor);
            TorsoBrush = BuildShaded(BodyColor, 0.14);
            PantsBrush = BuildShaded(PantsColor, 0.18);
            HatBrush = BuildShaded(HatColor, 0.12);
            HatRimBrush = BuildShaded(HatColor, 0.06);

            // Flags
            HatVisible = HatType != HatType.None;
            FaceUsesPhoto = UseProfilePhotoAsFace && FacePhoto != null;

            // Foto
            Brush faceBrush = FaceUsesPhoto && FacePhoto != null
            ? (Brush)new ImageBrush(FacePhoto) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center }
            : (Brush)Brushes.Transparent;
            FacePhotoBrush = faceBrush;


            // Partes visibles según tipo de cara/sombrero
            // Sombreros
            var beanie = (System.Windows.Controls.Grid)FindName("HatBeanie");
            var cap = (System.Windows.Controls.Grid)FindName("HatCap");
            var top = (System.Windows.Controls.Grid)FindName("HatTop");
            if (beanie != null) beanie.Opacity = HatType == HatType.Beanie ? 1 : 0;
            if (cap != null) cap.Opacity = HatType == HatType.Baseball ? 1 : 0;
            if (top != null) top.Opacity = HatType == HatType.TopHat ? 1 : 0;

            // Boca/ojos
            var mNeutral = (System.Windows.Shapes.Rectangle)FindName("MouthNeutral");
            var mHappy = (System.Windows.Shapes.Path)FindName("MouthHappy");
            var mAngry = (System.Windows.Shapes.Path)FindName("MouthAngry");
            var eLN = (System.Windows.Shapes.Ellipse)FindName("EyeLeftNeutral");
            var eRN = (System.Windows.Shapes.Ellipse)FindName("EyeRightNeutral");
            var eLH = (System.Windows.Shapes.Ellipse)FindName("EyeLeftHappy");
            var eRH = (System.Windows.Shapes.Ellipse)FindName("EyeRightHappy");

            if (mNeutral != null) mNeutral.Opacity = FaceType == FaceType.Neutral ? 1 : 0;
            if (mHappy != null) mHappy.Opacity = FaceType == FaceType.Happy ? 1 : 0;
            if (mAngry != null) mAngry.Opacity = FaceType == FaceType.Angry ? 1 : 0;

            if (eLN != null) eLN.Opacity = FaceType == FaceType.Neutral ? 1 : 0;
            if (eRN != null) eRN.Opacity = FaceType == FaceType.Neutral ? 1 : 0;
            if (eLH != null) eLH.Opacity = FaceType == FaceType.Happy ? 1 : 0;
            if (eRH != null) eRH.Opacity = FaceType == FaceType.Happy ? 1 : 0;
        }

        private static LinearGradientBrush BuildShaded(Color baseColor, double shade)
        {
            byte Dark(byte c) => (byte)Math.Max(0, c * (1 - shade));
            var darker = Color.FromArgb(baseColor.A, Dark(baseColor.R), Dark(baseColor.G), Dark(baseColor.B));
            return new LinearGradientBrush(
                new GradientStopCollection {
                    new GradientStop(darker, 0),
                    new GradientStop(baseColor, 1)
                },
                new Point(0, 0), new Point(0, 1));
        }

        private static Color ColorFromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private void ApplyAppearance(AvatarAppearance a)
        {
            if (a == null)
            {
                return;
            }

            // Map body color index to an actual Color
            Color MapBodyColor(int idx)
            {
                switch (idx)
                {
                    case 0: return (Color)ColorConverter.ConvertFromString("#FFD32F2F"); // Red
                    case 1: return (Color)ColorConverter.ConvertFromString("#FF2F6AA3"); // Blue
                    case 2: return (Color)ColorConverter.ConvertFromString("#FF4CAF50"); // Green
                    case 3: return (Color)ColorConverter.ConvertFromString("#FFED4C00"); // Yellow/Orange
                    case 4: return (Color)ColorConverter.ConvertFromString("#FF9C27B0"); // Purple
                    case 5: return (Color)ColorConverter.ConvertFromString("#FF9E9E9E"); // Gray
                    default: return BodyColor;
                }
            }

            Color MapPantsColor(int idx)
            {
                switch (idx)
                {
                    case 0: return (Color)ColorConverter.ConvertFromString("#FF111111"); // Black
                    case 1: return (Color)ColorConverter.ConvertFromString("#FF444444"); // DarkGray
                    case 2: return (Color)ColorConverter.ConvertFromString("#FF3F51B5"); // BlueJeans
                    default: return PantsColor;
                }
            }

            Color MapHatColor(int idx)
            {
                switch (idx)
                {
                    case 0: return (Color)ColorConverter.ConvertFromString("#FF2F6AA3"); // Default (blue)
                    case 1: return (Color)ColorConverter.ConvertFromString("#FFD32F2F"); // Red
                    case 2: return (Color)ColorConverter.ConvertFromString("#FF2F6AA3"); // Blue
                    case 3: return (Color)ColorConverter.ConvertFromString("#FF111111"); // Black
                    default: return HatColor;
                }
            }

            HatType MapHatType(int idx)
            {
                switch (idx)
                {
                    case 0: return HatType.None;
                    case 1: return HatType.Baseball; // Cap
                    case 2: return HatType.TopHat;
                    case 3: return HatType.Beanie;
                    default: return HatType.None;
                }
            }

            FaceType MapFaceType(int idx)
            {
                switch (idx)
                {
                    case 0: return FaceType.Neutral; // Default
                    case 1: return FaceType.Angry;
                    case 2: return FaceType.Happy;
                    case 3: return FaceType.Neutral; // Sleepy -> neutral fallback
                    default: return FaceType.Neutral;
                }
            }

            // Use current FacePhoto (ProfileImage) when applying
            var faceImage = FacePhoto;
            var usePhoto = a.UseProfilePhotoAsFace;

            // Call existing helper to set values
            SetAppearance(
                SkinColor, // skin not present in model; keep current skin
                MapBodyColor(a.BodyColor),
                MapPantsColor(a.PantsColor),
                MapHatType(a.HatType),
                MapHatColor(a.HatColor),
                MapFaceType(a.FaceType),
                faceImage,
                usePhoto);
        }

        // API conveniente
        public void SetAppearance(
            Color skin, Color body, Color pants,
            HatType hatType, Color hat,
            FaceType face,
            ImageSource facePhoto = null, bool usePhoto = false)
        {
            SkinColor = skin;
            BodyColor = body;
            PantsColor = pants;
            HatType = hatType;
            HatColor = hat;
            FaceType = face;
            FacePhoto = facePhoto;
            UseProfilePhotoAsFace = usePhoto && facePhoto != null;
        }
    }
}
