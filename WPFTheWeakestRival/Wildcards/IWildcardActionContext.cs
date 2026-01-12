using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFTheWeakestRival.Wildcards
{
    public interface IWildcardActionContext
    {
        int CurrentRound { get; }
        int CurrentPlayerUserId { get; }

        void ChangeCurrentQuestion();          
        void PassQuestionToOtherPlayer();      
        void ForceBankChainBeforeTurn();       
        void EnableScoreMultiplier(decimal factor); 
        void BlockOtherPlayerWildcardsOneRound();   
        void SwapTurnOrderWithTargetPlayer(int targetUserId); 
    }
}