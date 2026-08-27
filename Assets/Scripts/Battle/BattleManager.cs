using Camp;

namespace AutoChess
{
    public class BattleManager
    {
        public CampManager CampManager => _campManager;
        public ChessBoard ChessBoard => _chessBoard;
        
        private CampManager _campManager;
        private ChessBoard _chessBoard;
    }
}
