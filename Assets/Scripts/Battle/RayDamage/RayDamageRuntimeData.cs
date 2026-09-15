using System.Collections.Generic;
using System.Threading;
using AutoChess.Utils;
using UnityEngine;

namespace AutoChess.RayDamage
{
    /// <summary>
    /// 圆形射线飞行物的池化运行时数据，保存注册快照、当前位置和已命中棋子。
    /// </summary>
    public class RayDamageRuntimeData : IPool
    {
        /// <summary>射线伤害实例 ID 生成器。</summary>
        private static int _nextRayDamageId;

        /// <summary>该实例是否由对象池创建。</summary>
        public bool IsFromPool { get; set; }

        /// <summary>本次注册分配的射线伤害唯一 ID；回收后为 0。</summary>
        public int RayDamageId;

        /// <summary>注册时锁定的棋盘 XZ 发射起点。</summary>
        public Vector2 Origin;

        /// <summary>注册时归一化后的飞行方向。</summary>
        public Vector2 Direction;

        /// <summary>飞行物圆半径，判定使用棋子中心点到扫掠线段的距离。</summary>
        public float Radius;

        /// <summary>沿射线的飞行速度。</summary>
        public float Speed;

        /// <summary>从起点算起允许飞行的最远距离。</summary>
        public float MaxDistance;

        /// <summary>注册时锁定的伤害，命中时不再回查任何棋子攻击力。</summary>
        public int Damage;

        /// <summary>最多可命中的存活单位数，达到后清除飞行物。</summary>
        public int MaxAffectedCount;

        /// <summary>飞行物当前棋盘 XZ 坐标。</summary>
        public Vector2 CurrentPosition;

        /// <summary>从起点沿射线已经走过的距离。</summary>
        public float TraveledDistance;

        /// <summary>注册时拷贝的受影响阵营集合。</summary>
        public HashSet<int> AffectedCamps;

        /// <summary>本实例已经造成过伤害的棋子 UniqueId。</summary>
        public HashSet<int> HitUniqueIds;

        /// <summary>
        /// 使用一次注册请求初始化池化数据，并将位置置于起点。
        /// </summary>
        public void Initialize(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float speed,
            float maxDistance,
            HashSet<int> affectedCamps,
            int damage,
            int maxAffectedCount)
        {
            RayDamageId = Interlocked.Increment(ref _nextRayDamageId);
            Origin = origin;
            Direction = direction;
            Radius = radius;
            Speed = speed;
            MaxDistance = maxDistance;
            Damage = damage;
            MaxAffectedCount = maxAffectedCount;
            CurrentPosition = origin;
            TraveledDistance = 0f;
            ReplaceSet(ref AffectedCamps, affectedCamps);
            if (HitUniqueIds == null)
            {
                HitUniqueIds = new HashSet<int>();
            }
            else
            {
                HitUniqueIds.Clear();
            }
        }

        /// <summary>
        /// 清空运行时数值和集合，并将池对象归还到 ObjectPool。
        /// </summary>
        public void Dispose()
        {
            RayDamageId = 0;
            Origin = Vector2.zero;
            Direction = Vector2.zero;
            Radius = 0f;
            Speed = 0f;
            MaxDistance = 0f;
            Damage = 0;
            MaxAffectedCount = 0;
            CurrentPosition = Vector2.zero;
            TraveledDistance = 0f;
            AffectedCamps?.Clear();
            HitUniqueIds?.Clear();
            ObjectPool.Recycle(this);
        }

        /// <summary>
        /// 用注册时拷贝的集合替换当前快照，优先复用已有 HashSet。
        /// </summary>
        private static void ReplaceSet(ref HashSet<int> target, HashSet<int> source)
        {
            if (target == null)
            {
                target = source ?? new HashSet<int>();
                return;
            }

            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (int value in source)
            {
                target.Add(value);
            }
        }
    }
}
