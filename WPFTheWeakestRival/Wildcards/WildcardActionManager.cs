using System;
using log4net;
using WPFTheWeakestRival.WildcardService;

namespace WPFTheWeakestRival.Wildcards
{
    public static class WildcardActionManager
    {
        private const decimal DUPLICATE_SCORE_FACTOR = 2m;

        private const string CODE_CHANGE_QUESTION = "CHANGE_QUESTION";
        private const string CODE_PASS_QUESTION = "PASS_QUESTION";
        private const string CODE_FORCED_BANK = "FORCED_BANK";
        private const string CODE_DUPLICATE_SCORE = "DUPLICATE_SCORE";
        private const string CODE_BLOCK_WILDCARDS = "BLOCK_WILDCARDS";

        public static void ApplyWildcardAction(
            PlayerWildcardDto wildcard,
            IWildcardActionContext context,
            ILog logger)
        {
            if (wildcard == null)
            {
                throw new ArgumentNullException(nameof(wildcard));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            string code = wildcard.Code?.Trim().ToUpperInvariant() ?? string.Empty;

            logger.InfoFormat(
                "Applying wildcard action. Code={0}, UserId={1}, Round={2}",
                code,
                context.CurrentPlayerUserId,
                context.CurrentRound);

            switch (code)
            {
                case CODE_CHANGE_QUESTION:
                    context.ChangeCurrentQuestion();
                    break;

                case CODE_PASS_QUESTION:
                    context.PassQuestionToOtherPlayer();
                    break;

                case CODE_FORCED_BANK:
                    context.ForceBankChainBeforeTurn();
                    break;

                case CODE_DUPLICATE_SCORE:
                    context.EnableScoreMultiplier(DUPLICATE_SCORE_FACTOR);
                    break;

                case CODE_BLOCK_WILDCARDS:
                    context.BlockOtherPlayerWildcardsOneRound();
                    break;
                default:
                    logger.WarnFormat("Unknown wildcard code '{0}'. No action applied.", code);
                    break;
            }
        }
    }
}
