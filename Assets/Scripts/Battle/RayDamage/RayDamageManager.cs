using System;
using System.Collections.Generic;
using AutoChess.Utils;
using Camp;
using Chess;
using UnityEngine;

namespace AutoChess.RayDamage
{
    /// <summary>
    /// 管理圆形射线飞行物的注册、沿射线移动、扫掠命中和对象池回收。
    /// </summary>
    public class RayDamageManager : IDisposable
    {
        /// <summary>棋子中心到扫掠线段判定时视为静止的平方距离阈值。</summary>
        private const float StationarySqrEpsilon = 0.000001f;

        /// <summary>查询受影响阵营棋子的阵营管理器。</summary>
        private readonly CampManager _campManager;

        /// <summary>当前仍在飞行的射线伤害。</summary>
        private readonly HashSet<RayDamageRuntimeData> _instances = new HashSet<RayDamageRuntimeData>();

        /// <summary>当前 Tick 中等待回收的射线伤害。</summary>
        private readonly List<RayDamageRuntimeData> _completedInstances = new List<RayDamageRuntimeData>();

        /// <summary>本步扫掠命中的候选，按沿射线距离排序后结算。</summary>
        private readonly List<RayHitCandidate> _hitCandidates = new List<RayHitCandidate>();

        /// <summary>管理器是否已经释放。</summary>
        private bool _isDisposed;

        /// <summary>创建射线伤害管理器并绑定阵营查询。</summary>
        public RayDamageManager(CampManager campManager)
        {
            _campManager = campManager ?? throw new ArgumentNullException(nameof(campManager));
        }

        /// <summary>获取当前所有仍在飞行的射线伤害。</summary>
        public IReadOnlyCollection<RayDamageRuntimeData> Instances => _instances;

        /// <summary>获取当前仍在飞行的射线伤害数量。</summary>
        public int ActiveCount => _instances.Count;

        /// <summary>
        /// 注册一枚圆形射线飞行物；参数非法时返回 null，不立刻结算。
        /// </summary>
        public RayDamageRuntimeData Register(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float speed,
            float maxDistance,
            IEnumerable<int> affectedCamps,
            int damage,
            int maxAffectedCount)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RayDamageManager));
            }

            if (!TryNormalizeDirection(direction, out Vector2 normalized)
                || !AreParametersValid(origin, radius, speed, maxDistance, damage, maxAffectedCount)
                || !TryCopyAffectedCamps(affectedCamps, out HashSet<int> camps))
            {
                return null;
            }

            RayDamageRuntimeData instance = ObjectPool.Fetch<RayDamageRuntimeData>();
            instance.Initialize(
                origin,
                normalized,
                radius,
                speed,
                maxDistance,
                camps,
                damage,
                maxAffectedCount);
            if (!_instances.Add(instance))
            {
                instance.Dispose();
                throw new InvalidOperationException("同一个射线伤害实例不能被重复注册。");
            }

            return instance;
        }

        /// <summary>
        /// 使用 Unity 帧时间推进射线飞行物。
        /// </summary>
        [Obsolete("战斗逻辑必须由 BattleManager 传入固定时间步。")]
        public void Tick()
        {
            Tick(1f / 60f);
        }

        /// <summary>
        /// 使用指定时间步长沿射线移动飞行物，并对本步扫掠到的棋子结算伤害。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_isDisposed || _instances.Count == 0)
            {
                return;
            }

            float safeDeltaTime = Math.Max(0f, deltaTime);
            _completedInstances.Clear();

            foreach (RayDamageRuntimeData instance in _instances)
            {
                AdvanceAndApplyHits(instance, safeDeltaTime);
                if (ShouldRecycle(instance))
                {
                    _completedInstances.Add(instance);
                }
            }

            foreach (RayDamageRuntimeData instance in _completedInstances)
            {
                Recycle(instance);
            }

            _completedInstances.Clear();
        }

        /// <summary>
        /// 回收全部仍在飞行的射线伤害。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _completedInstances.Clear();
            _completedInstances.AddRange(_instances);
            foreach (RayDamageRuntimeData instance in _completedInstances)
            {
                Recycle(instance);
            }

            _completedInstances.Clear();
        }

        /// <summary>
        /// 校验起点、半径、速度、最远距离、伤害和命中上限是否可以注册。
        /// </summary>
        public static bool AreParametersValid(
            Vector2 origin,
            float radius,
            float speed,
            float maxDistance,
            int damage,
            int maxAffectedCount)
        {
            return IsFinite(origin.x)
                   && IsFinite(origin.y)
                   && IsFinite(radius)
                   && radius >= 0f
                   && IsFinite(speed)
                   && speed > 0f
                   && IsFinite(maxDistance)
                   && maxDistance >= 0f
                   && damage >= 0
                   && maxAffectedCount >= 1;
        }

        /// <summary>
        /// 将发射方向归一化为单位向量；零向量或非有限分量视为非法。
        /// </summary>
        public static bool TryNormalizeDirection(Vector2 direction, out Vector2 normalized)
        {
            normalized = Vector2.zero;
            if (!IsFinite(direction.x) || !IsFinite(direction.y))
            {
                return false;
            }

            float length = (float)Math.Sqrt((direction.x * direction.x) + (direction.y * direction.y));
            if (length <= 0.000001f)
            {
                return false;
            }

            normalized = new Vector2(direction.x / length, direction.y / length);
            return true;
        }

        /// <summary>
        /// 将受影响阵营拷贝为独立集合；空集合或 null 视为非法。
        /// </summary>
        public static bool TryCopyAffectedCamps(IEnumerable<int> affectedCamps, out HashSet<int> camps)
        {
            camps = null;
            if (affectedCamps == null)
            {
                return false;
            }

            HashSet<int> copied = new HashSet<int>();
            foreach (int campId in affectedCamps)
            {
                copied.Add(campId);
            }

            if (copied.Count == 0)
            {
                return false;
            }

            camps = copied;
            return true;
        }

        /// <summary>
        /// 沿射线推进一个时间步，并按从近到远对本步扫掠到的棋子结算伤害。
        /// </summary>
        private void AdvanceAndApplyHits(RayDamageRuntimeData instance, float deltaTime)
        {
            Vector2 previous = instance.CurrentPosition;
            float remaining = instance.MaxDistance - instance.TraveledDistance;
            if (remaining < 0f)
            {
                remaining = 0f;
            }

            float step = Math.Min(instance.Speed * deltaTime, remaining);
            if (step < 0f)
            {
                step = 0f;
            }

            Vector2 next = new Vector2(
                previous.x + (instance.Direction.x * step),
                previous.y + (instance.Direction.y * step));
            instance.CurrentPosition = next;
            instance.TraveledDistance += step;
            ApplyHits(instance, previous, next);
        }

        /// <summary>
        /// 收集本步扫掠命中的存活棋子，按沿射线距离排序后依次扣血。
        /// </summary>
        private void ApplyHits(RayDamageRuntimeData instance, Vector2 previous, Vector2 next)
        {
            if (instance.AffectedCamps == null || instance.HitUniqueIds == null)
            {
                return;
            }

            _hitCandidates.Clear();
            foreach (ChessController chess in _campManager.GetAllChesses())
            {
                if (!CanBeHit(chess, instance))
                {
                    continue;
                }

                Vector2 position = chess.RuntimeData.CurrentPosition;
                if (!IsWithinSweptRadius(previous, next, position, instance.Radius))
                {
                    continue;
                }

                float alongRay = ((position.x - instance.Origin.x) * instance.Direction.x)
                                 + ((position.y - instance.Origin.y) * instance.Direction.y);
                _hitCandidates.Add(new RayHitCandidate(alongRay, chess.UniqueId, chess));
            }

            _hitCandidates.Sort(CompareHits);
            for (int i = 0; i < _hitCandidates.Count; i++)
            {
                if (instance.HitUniqueIds.Count >= instance.MaxAffectedCount)
                {
                    break;
                }

                ChessController chess = _hitCandidates[i].Chess;
                if (!CanBeHit(chess, instance))
                {
                    continue;
                }

                chess.DamageReceiver.ApplyDamage(instance.Damage);
                instance.HitUniqueIds.Add(chess.UniqueId);
            }

            _hitCandidates.Clear();
        }

        /// <summary>
        /// 判断飞行物是否已达命中上限或飞完全程。
        /// </summary>
        private static bool ShouldRecycle(RayDamageRuntimeData instance)
        {
            if (instance.HitUniqueIds != null
                && instance.HitUniqueIds.Count >= instance.MaxAffectedCount)
            {
                return true;
            }

            return instance.TraveledDistance + 0.000001f >= instance.MaxDistance;
        }

        /// <summary>
        /// 判断棋子是否属于受影响阵营、仍然存活，且尚未被本实例命中。
        /// </summary>
        private static bool CanBeHit(ChessController chess, RayDamageRuntimeData instance)
        {
            if (chess == null || !chess.IsAlive || chess.RuntimeData == null)
            {
                return false;
            }

            if (!instance.AffectedCamps.Contains(chess.Camp))
            {
                return false;
            }

            return !instance.HitUniqueIds.Contains(chess.UniqueId);
        }

        /// <summary>
        /// 判断点到扫掠线段的最短距离是否不超过半径，避免战斗线程调用 Unity 数学辅助 API。
        /// </summary>
        private static bool IsWithinSweptRadius(Vector2 start, Vector2 end, Vector2 point, float radius)
        {
            float abx = end.x - start.x;
            float aby = end.y - start.y;
            float apx = point.x - start.x;
            float apy = point.y - start.y;
            float abSqr = (abx * abx) + (aby * aby);
            float closestX;
            float closestY;
            if (abSqr <= StationarySqrEpsilon)
            {
                closestX = start.x;
                closestY = start.y;
            }
            else
            {
                float t = ((apx * abx) + (apy * aby)) / abSqr;
                if (t < 0f)
                {
                    t = 0f;
                }
                else if (t > 1f)
                {
                    t = 1f;
                }

                closestX = start.x + (abx * t);
                closestY = start.y + (aby * t);
            }

            float dx = point.x - closestX;
            float dy = point.y - closestY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        /// <summary>
        /// 按沿射线投影距离排序命中候选，相等时再比 UniqueId。
        /// </summary>
        private static int CompareHits(RayHitCandidate left, RayHitCandidate right)
        {
            int compare = left.AlongRay.CompareTo(right.AlongRay);
            if (compare != 0)
            {
                return compare;
            }

            return left.UniqueId.CompareTo(right.UniqueId);
        }

        /// <summary>
        /// 从活动集合移除并回收指定射线伤害。
        /// </summary>
        private void Recycle(RayDamageRuntimeData instance)
        {
            if (!_instances.Remove(instance))
            {
                return;
            }

            instance.Dispose();
        }

        /// <summary>判断浮点数是否为有限值。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>一次扫掠命中候选，用于按沿射线距离稳定排序。</summary>
        private readonly struct RayHitCandidate
        {
            /// <summary>棋子中心在射线上相对起点的投影距离。</summary>
            public readonly float AlongRay;

            /// <summary>候选棋子 UniqueId。</summary>
            public readonly int UniqueId;

            /// <summary>候选棋子。</summary>
            public readonly ChessController Chess;

            /// <summary>创建一次扫掠命中候选。</summary>
            public RayHitCandidate(float alongRay, int uniqueId, ChessController chess)
            {
                AlongRay = alongRay;
                UniqueId = uniqueId;
                Chess = chess;
            }
        }
    }
}
