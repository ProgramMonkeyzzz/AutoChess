using System;

namespace Chess
{
    /// <summary>
    /// 接收对棋子的伤害，并在伤害结算后检查死亡状态。
    /// </summary>
    public class ChessDamageReceiver
    {
        private readonly ChessController _chessController;

        /// <summary>
        /// 创建绑定到指定棋子控制器的伤害接收器。
        /// </summary>
        public ChessDamageReceiver(ChessController chessController)
        {
            _chessController = chessController ?? throw new ArgumentNullException(nameof(chessController));
        }

        /// <summary>
        /// 扣除伤害并在本次调用结束时检查是否需要进入死亡状态。
        /// </summary>
        public void ApplyDamage(int damage)
        {
            _chessController.RuntimeData.Hp -= damage;
            if (_chessController.RuntimeData.Hp <= 0)
            {
                _chessController.RuntimeData.Hp = 0;
            }

            _chessController.CheckDeathStateTransition();
        }
    }
}
