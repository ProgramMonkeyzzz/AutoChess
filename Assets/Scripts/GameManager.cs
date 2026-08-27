using AutoChess;
using AutoChess.Utils;

namespace AutoChess
{
    public class GameManager : Singleton<GameManager>
    {
        public BattleManager BattleManager => _battleManager;

        private BattleManager _battleManager;
    }
}
