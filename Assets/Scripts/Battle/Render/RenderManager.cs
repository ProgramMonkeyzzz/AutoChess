using System.Collections.Generic;

namespace AutoChess.Render
{
    public class RenderManager
    {
        /// <summary>
        /// key是棋子唯一Id
        /// </summary>
        private Dictionary<int,ChessView> _chessViews = new Dictionary<int,ChessView>();
        
        /// <summary>
        /// key是子弹唯一Id
        /// </summary>
        private Dictionary<int,BulletView> _bulletViews = new Dictionary<int,BulletView>();

        private const string _chessPrefabName = "";

        private const string _bulletPrefabName = "";

        public void CreateChessView(int chessId)
        {
            
        }

        public void CreateBulletView(int bulletId)
        {
            
        }
    }
}