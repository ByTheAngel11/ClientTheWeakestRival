using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WPFTheWeakestRival.Controls;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchWindowUiRefs
    {
        public MatchWindowUiRefs(
            Window window,
            TextBlock txtMatchCodeSmall,
            ListBox lstPlayers,
            TextBlock txtPlayersSummary,
            TextBlock txtChain,
            TextBlock txtBanked,
            TextBlock txtTurnPlayerName,
            TextBlock txtTurnLabel,
            TextBlock txtTimer,
            TextBlock txtQuestion,
            TextBlock txtAnswerFeedback,
            TextBlock txtPhase,
            TextBlock txtWildcardName,
            TextBlock txtWildcardDescription,
            Image imgWildcardIcon,
            Button btnWildcardPrev,
            Button btnWildcardNext,
            Button btnAnswer1,
            Button btnAnswer2,
            Button btnAnswer3,
            Button btnAnswer4,
            Button btnBank,
            Border turnBannerBackground,
            AvatarControl turnAvatar,
            UIElement introOverlay,
            MediaElement introVideo,
            UIElement coinFlipOverlay,
            TextBlock coinFlipResultText,
            UIElement specialEventOverlay,
            TextBlock specialEventTitleText,
            TextBlock specialEventDescriptionText)
        {
            Window = window;
            TxtMatchCodeSmall = txtMatchCodeSmall;
            LstPlayers = lstPlayers;
            TxtPlayersSummary = txtPlayersSummary;
            TxtChain = txtChain;
            TxtBanked = txtBanked;
            TxtTurnPlayerName = txtTurnPlayerName;
            TxtTurnLabel = txtTurnLabel;
            TxtTimer = txtTimer;
            TxtQuestion = txtQuestion;
            TxtAnswerFeedback = txtAnswerFeedback;
            TxtPhase = txtPhase;

            TxtWildcardName = txtWildcardName;
            TxtWildcardDescription = txtWildcardDescription;
            ImgWildcardIcon = imgWildcardIcon;

            BtnWildcardPrev = btnWildcardPrev;
            BtnWildcardNext = btnWildcardNext;

            BtnAnswer1 = btnAnswer1;
            BtnAnswer2 = btnAnswer2;
            BtnAnswer3 = btnAnswer3;
            BtnAnswer4 = btnAnswer4;

            BtnBank = btnBank;

            TurnBannerBackground = turnBannerBackground;
            TurnAvatar = turnAvatar;

            IntroOverlay = introOverlay;
            IntroVideo = introVideo;

            CoinFlipOverlay = coinFlipOverlay;
            CoinFlipResultText = coinFlipResultText;

            SpecialEventOverlay = specialEventOverlay;
            SpecialEventTitleText = specialEventTitleText;
            SpecialEventDescriptionText = specialEventDescriptionText;
        }

        public Window Window { get; }

        public TextBlock TxtMatchCodeSmall { get; }
        public ListBox LstPlayers { get; }
        public TextBlock TxtPlayersSummary { get; }

        public TextBlock TxtChain { get; }
        public TextBlock TxtBanked { get; }

        public TextBlock TxtTurnPlayerName { get; }
        public TextBlock TxtTurnLabel { get; }
        public TextBlock TxtTimer { get; }

        public TextBlock TxtQuestion { get; }
        public TextBlock TxtAnswerFeedback { get; }

        public TextBlock TxtPhase { get; }

        public TextBlock TxtWildcardName { get; }
        public TextBlock TxtWildcardDescription { get; }
        public Image ImgWildcardIcon { get; }

        public Button BtnWildcardPrev { get; }
        public Button BtnWildcardNext { get; }

        public Button BtnAnswer1 { get; }
        public Button BtnAnswer2 { get; }
        public Button BtnAnswer3 { get; }
        public Button BtnAnswer4 { get; }

        public Button BtnBank { get; }

        public Border TurnBannerBackground { get; }
        public AvatarControl TurnAvatar { get; }

        public UIElement IntroOverlay { get; }
        public MediaElement IntroVideo { get; }

        public UIElement CoinFlipOverlay { get; }
        public TextBlock CoinFlipResultText { get; }

        public UIElement SpecialEventOverlay { get; }
        public TextBlock SpecialEventTitleText { get; }
        public TextBlock SpecialEventDescriptionText { get; }
    }
}
