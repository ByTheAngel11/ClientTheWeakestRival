using System;
using System.Windows;
using System.Windows.Controls;
using WPFTheWeakestRival.Controls;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class MatchWindowUiRefs
    {
        public MatchWindowUiRefs(MatchWindowUiRefsArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            Window = args.Window ?? throw new ArgumentNullException(nameof(args.Window));

            TxtMatchCodeSmall = args.TxtMatchCodeSmall ?? throw new ArgumentNullException(nameof(args.TxtMatchCodeSmall));
            LstPlayers = args.LstPlayers ?? throw new ArgumentNullException(nameof(args.LstPlayers));
            TxtPlayersSummary = args.TxtPlayersSummary ?? throw new ArgumentNullException(nameof(args.TxtPlayersSummary));

            TxtChain = args.TxtChain ?? throw new ArgumentNullException(nameof(args.TxtChain));
            TxtBanked = args.TxtBanked ?? throw new ArgumentNullException(nameof(args.TxtBanked));

            TxtTurnPlayerName = args.TxtTurnPlayerName ?? throw new ArgumentNullException(nameof(args.TxtTurnPlayerName));
            TxtTurnLabel = args.TxtTurnLabel ?? throw new ArgumentNullException(nameof(args.TxtTurnLabel));
            TxtTimer = args.TxtTimer ?? throw new ArgumentNullException(nameof(args.TxtTimer));

            TxtQuestion = args.TxtQuestion ?? throw new ArgumentNullException(nameof(args.TxtQuestion));
            TxtAnswerFeedback = args.TxtAnswerFeedback ?? throw new ArgumentNullException(nameof(args.TxtAnswerFeedback));

            TxtPhase = args.TxtPhase ?? throw new ArgumentNullException(nameof(args.TxtPhase));

            TxtWildcardName = args.TxtWildcardName ?? throw new ArgumentNullException(nameof(args.TxtWildcardName));
            TxtWildcardDescription = args.TxtWildcardDescription ?? throw new ArgumentNullException(nameof(args.TxtWildcardDescription));
            ImgWildcardIcon = args.ImgWildcardIcon ?? throw new ArgumentNullException(nameof(args.ImgWildcardIcon));

            BtnWildcardPrev = args.BtnWildcardPrev ?? throw new ArgumentNullException(nameof(args.BtnWildcardPrev));
            BtnWildcardNext = args.BtnWildcardNext ?? throw new ArgumentNullException(nameof(args.BtnWildcardNext));
            BtnUseWildcard = args.BtnUseWildcard ?? throw new ArgumentNullException(nameof(args.BtnUseWildcard));

            BtnAnswer1 = args.BtnAnswer1 ?? throw new ArgumentNullException(nameof(args.BtnAnswer1));
            BtnAnswer2 = args.BtnAnswer2 ?? throw new ArgumentNullException(nameof(args.BtnAnswer2));
            BtnAnswer3 = args.BtnAnswer3 ?? throw new ArgumentNullException(nameof(args.BtnAnswer3));
            BtnAnswer4 = args.BtnAnswer4 ?? throw new ArgumentNullException(nameof(args.BtnAnswer4));

            BtnBank = args.BtnBank ?? throw new ArgumentNullException(nameof(args.BtnBank));

            TurnBannerBackground = args.TurnBannerBackground ?? throw new ArgumentNullException(nameof(args.TurnBannerBackground));
            TurnAvatar = args.TurnAvatar ?? throw new ArgumentNullException(nameof(args.TurnAvatar));

            IntroOverlay = args.IntroOverlay ?? throw new ArgumentNullException(nameof(args.IntroOverlay));
            IntroVideo = args.IntroVideo ?? throw new ArgumentNullException(nameof(args.IntroVideo));

            CoinFlipOverlay = args.CoinFlipOverlay ?? throw new ArgumentNullException(nameof(args.CoinFlipOverlay));
            CoinFlipResultText = args.CoinFlipResultText ?? throw new ArgumentNullException(nameof(args.CoinFlipResultText));

            SpecialEventOverlay = args.SpecialEventOverlay ?? throw new ArgumentNullException(nameof(args.SpecialEventOverlay));
            SpecialEventTitleText = args.SpecialEventTitleText ?? throw new ArgumentNullException(nameof(args.SpecialEventTitleText));
            SpecialEventDescriptionText = args.SpecialEventDescriptionText ?? throw new ArgumentNullException(nameof(args.SpecialEventDescriptionText));

            GrdReconnectOverlay = args.GrdReconnectOverlay ?? throw new ArgumentNullException(nameof(args.GrdReconnectOverlay));
            TxtReconnectStatus = args.TxtReconnectStatus ?? throw new ArgumentNullException(nameof(args.TxtReconnectStatus));
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
        public Button BtnUseWildcard { get; }

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

        public Grid GrdReconnectOverlay { get; set; }
        public TextBlock TxtReconnectStatus { get; set; }
    }

    internal sealed class MatchWindowUiRefsArgs
    {
        public Window Window { get; set; }

        public TextBlock TxtMatchCodeSmall { get; set; }
        public ListBox LstPlayers { get; set; }
        public TextBlock TxtPlayersSummary { get; set; }

        public TextBlock TxtChain { get; set; }
        public TextBlock TxtBanked { get; set; }

        public TextBlock TxtTurnPlayerName { get; set; }
        public TextBlock TxtTurnLabel { get; set; }
        public TextBlock TxtTimer { get; set; }

        public TextBlock TxtQuestion { get; set; }
        public TextBlock TxtAnswerFeedback { get; set; }

        public TextBlock TxtPhase { get; set; }

        public TextBlock TxtWildcardName { get; set; }
        public TextBlock TxtWildcardDescription { get; set; }
        public Image ImgWildcardIcon { get; set; }

        public Button BtnWildcardPrev { get; set; }
        public Button BtnWildcardNext { get; set; }
        public Button BtnUseWildcard { get; set; }

        public Button BtnAnswer1 { get; set; }
        public Button BtnAnswer2 { get; set; }
        public Button BtnAnswer3 { get; set; }
        public Button BtnAnswer4 { get; set; }

        public Button BtnBank { get; set; }

        public Border TurnBannerBackground { get; set; }
        public AvatarControl TurnAvatar { get; set; }

        public UIElement IntroOverlay { get; set; }
        public MediaElement IntroVideo { get; set; }

        public UIElement CoinFlipOverlay { get; set; }
        public TextBlock CoinFlipResultText { get; set; }

        public UIElement SpecialEventOverlay { get; set; }
        public TextBlock SpecialEventTitleText { get; set; }
        public TextBlock SpecialEventDescriptionText { get; set; }
        public Grid GrdReconnectOverlay { get; set; }
        public TextBlock TxtReconnectStatus { get; set; }

    }
}
