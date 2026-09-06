using BehaviorTree;

namespace Chess.Behavior
{
    public class IsAttackCDReady : ConditionalNode
    {
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            if (context.Owner is ChessController chessController)
            {
                if (chessController.AttackController.CooldownRemaining <=0)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}