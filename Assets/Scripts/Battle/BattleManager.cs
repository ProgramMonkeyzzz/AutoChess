using System;
using AutoChess.Bullet;
using Camp;
using UnityEngine;

namespace AutoChess
{
    /// <summary>
    /// 协调阵营、棋盘和子弹等战斗子系统。
    /// </summary>
    public class BattleManager : IDisposable
    {
        /// <summary>获取阵营管理器。</summary>
        public CampManager CampManager => _campManager;

        /// <summary>获取当前战斗棋盘。</summary>
        public ChessBoard ChessBoard => _chessBoard;

        /// <summary>获取子弹管理器。</summary>
        public BulletManager BulletManager => _bulletManager;

        /// <summary>阵营管理器实例。</summary>
        private readonly CampManager _campManager;

        /// <summary>战斗棋盘实例。</summary>
        private readonly ChessBoard _chessBoard;

        /// <summary>由战斗管理器托管的子弹管理器。</summary>
        private readonly BulletManager _bulletManager;

        /// <summary>
        /// 创建战斗管理器及其阵营、棋盘和子弹子系统。
        /// </summary>
        public BattleManager()
        {
            _campManager = new CampManager();
            _chessBoard = new ChessBoard();
            _bulletManager = new BulletManager();
        }

        /// <summary>
        /// 使用 Unity 帧时间推进战斗中的子弹。
        /// </summary>
        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 使用指定时间步长推进战斗中的子弹。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _bulletManager.Tick(deltaTime);
        }

        /// <summary>
        /// 释放战斗管理器托管的子弹管理器。
        /// </summary>
        public void Dispose()
        {
            _bulletManager.Dispose();
        }
    }
}
