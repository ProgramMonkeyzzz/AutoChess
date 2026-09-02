using AutoChess.Utils;
using Chess;
using UnityEngine;

namespace AutoChess.Bullet
{
    /// <summary>
    /// 子弹发射事件，由攻击控制器发送并由子弹管理器监听。
    /// </summary>
    public readonly struct BulletSpawnEvent
    {
        /// <summary>发射子弹的棋子。</summary>
        public readonly ChessController OwnerChess;

        /// <summary>子弹锁定的目标棋子。</summary>
        public readonly ChessController TargetChess;

        /// <summary>子弹在棋盘 XZ 平面上的移动速度。</summary>
        public readonly float BulletSpeed;

        /// <summary>
        /// 创建一次子弹发射事件。
        /// </summary>
        public BulletSpawnEvent(ChessController ownerChess, ChessController targetChess, float bulletSpeed)
        {
            OwnerChess = ownerChess;
            TargetChess = targetChess;
            BulletSpeed = bulletSpeed;
        }
    }

    /// <summary>
    /// 子弹的池化运行时数据。
    /// </summary>
    public class BulletRuntimeData : IPool
    {
        /// <summary>该实例是否由对象池创建。</summary>
        public bool IsFromPool { get; set; }

        /// <summary>发射子弹的棋子。</summary>
        public ChessController OwnerChess;

        /// <summary>子弹当前追踪的目标棋子；目标失效后为空。</summary>
        public ChessController TargetChess;

        /// <summary>子弹当前棋盘 XZ 坐标。</summary>
        public Vector2 CurrentPosition;

        /// <summary>目标最后一次有效的棋盘 XZ 坐标。</summary>
        public Vector2 LastTargetPosition;

        /// <summary>子弹在棋盘 XZ 平面上的移动速度。</summary>
        public float Speed;

        /// <summary>创建时锁定的最终伤害，飞行期间不再随攻击者攻击力变化。</summary>
        public int Damage;

        /// <summary>抵达目标位置后是否仍允许造成伤害。</summary>
        public bool CanDamageTarget;

        /// <summary>
        /// 使用一次发射请求初始化池化子弹数据，并快照攻击者当前攻击力。
        /// </summary>
        public void Initialize(ChessController ownerChess, ChessController targetChess, float speed)
        {
            OwnerChess = ownerChess;
            TargetChess = targetChess;
            CurrentPosition = ownerChess.RuntimeData.CurrentPosition;
            LastTargetPosition = targetChess.RuntimeData.CurrentPosition;
            Speed = speed;
            Damage = ownerChess.RuntimeData.GetCurrentAtk(ownerChess.BaseData.Atk);
            CanDamageTarget = true;
        }

        /// <summary>
        /// 清空运行时引用和数值，并将池对象归还到 ObjectPool。
        /// </summary>
        public void Dispose()
        {
            OwnerChess = null;
            TargetChess = null;
            CurrentPosition = Vector2.zero;
            LastTargetPosition = Vector2.zero;
            Speed = 0f;
            Damage = 0;
            CanDamageTarget = false;
            ObjectPool.Recycle(this);
        }
    }
}
