using System.Collections.Generic;
using System.Threading;
using AutoChess.Utils;
using UnityEngine;

namespace AutoChess.CircularDamage
{
    /// <summary>
    /// 圆形范围伤害的池化运行时数据，保存注册时锁定的圆心、半径、阵营和攻击力。
    /// </summary>
    public class CircularDamageRuntimeData : IPool
    {
        /// <summary>圆形伤害实例 ID 生成器。</summary>
        private static int _nextCircularDamageId;

        /// <summary>该实例是否由对象池创建。</summary>
        public bool IsFromPool { get; set; }

        /// <summary>本次注册分配的圆形伤害唯一 ID；回收后为 0。</summary>
        public int CircularDamageId;

        /// <summary>注册时锁定的棋盘 XZ 圆心，触发前不再移动。</summary>
        public Vector2 Center;

        /// <summary>注册时锁定的圆半径，判定使用棋子中心点距离。</summary>
        public float Radius;

        /// <summary>距离触发还剩的延迟秒数。</summary>
        public float RemainingDelay;

        /// <summary>注册时锁定的伤害，触发时不再回查任何棋子攻击力。</summary>
        public int Damage;

        /// <summary>注册时拷贝的受影响阵营集合。</summary>
        public HashSet<int> AffectedCamps;

        /// <summary>
        /// 使用一次注册请求初始化池化数据，并接管阵营集合快照。
        /// </summary>
        public void Initialize(
            Vector2 center,
            float radius,
            float delay,
            HashSet<int> affectedCamps,
            int damage)
        {
            CircularDamageId = Interlocked.Increment(ref _nextCircularDamageId);
            Center = center;
            Radius = radius;
            RemainingDelay = delay;
            Damage = damage;
            ReplaceAffectedCamps(affectedCamps);
        }

        /// <summary>
        /// 清空运行时数值和阵营快照，并将池对象归还到 ObjectPool。
        /// </summary>
        public void Dispose()
        {
            CircularDamageId = 0;
            Center = Vector2.zero;
            Radius = 0f;
            RemainingDelay = 0f;
            Damage = 0;
            AffectedCamps?.Clear();
            ObjectPool.Recycle(this);
        }

        /// <summary>
        /// 用注册时拷贝的阵营集合替换当前快照，优先复用已有 HashSet。
        /// </summary>
        private void ReplaceAffectedCamps(HashSet<int> affectedCamps)
        {
            if (AffectedCamps == null)
            {
                AffectedCamps = affectedCamps ?? new HashSet<int>();
                return;
            }

            AffectedCamps.Clear();
            if (affectedCamps == null)
            {
                return;
            }

            foreach (int campId in affectedCamps)
            {
                AffectedCamps.Add(campId);
            }
        }
    }
}
