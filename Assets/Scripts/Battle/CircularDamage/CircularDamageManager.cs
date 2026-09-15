using System;
using System.Collections.Generic;
using AutoChess.Utils;
using Camp;
using Chess;
using UnityEngine;

namespace AutoChess.CircularDamage
{
    /// <summary>
    /// 管理延迟圆形范围伤害的注册、倒计时、范围结算和对象池回收。
    /// </summary>
    public class CircularDamageManager : IDisposable
    {
        /// <summary>查询受影响阵营棋子的阵营管理器。</summary>
        private readonly CampManager _campManager;

        /// <summary>当前尚未触发的圆形伤害。</summary>
        private readonly HashSet<CircularDamageRuntimeData> _instances =
            new HashSet<CircularDamageRuntimeData>();

        /// <summary>当前 Tick 中已到期、等待回收的圆形伤害。</summary>
        private readonly List<CircularDamageRuntimeData> _completedInstances =
            new List<CircularDamageRuntimeData>();

        /// <summary>管理器是否已经释放。</summary>
        private bool _isDisposed;

        /// <summary>创建圆形范围伤害管理器并绑定阵营查询。</summary>
        public CircularDamageManager(CampManager campManager)
        {
            _campManager = campManager ?? throw new ArgumentNullException(nameof(campManager));
        }

        /// <summary>获取当前所有尚未触发的圆形伤害。</summary>
        public IReadOnlyCollection<CircularDamageRuntimeData> Instances => _instances;

        /// <summary>获取当前尚未触发的圆形伤害数量。</summary>
        public int ActiveCount => _instances.Count;

        /// <summary>
        /// 注册一次圆形范围伤害；参数非法时返回 null，不立刻结算。
        /// </summary>
        public CircularDamageRuntimeData Register(
            Vector2 center,
            float radius,
            float delay,
            IEnumerable<int> affectedCamps,
            int damage)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(CircularDamageManager));
            }

            if (!AreParametersValid(center, radius, delay, damage)
                || !TryCopyAffectedCamps(affectedCamps, out HashSet<int> camps))
            {
                return null;
            }

            CircularDamageRuntimeData instance = ObjectPool.Fetch<CircularDamageRuntimeData>();
            instance.Initialize(center, radius, delay, camps, damage);
            if (!_instances.Add(instance))
            {
                instance.Dispose();
                throw new InvalidOperationException("同一个圆形伤害实例不能被重复注册。");
            }

            return instance;
        }

        /// <summary>
        /// 使用 Unity 帧时间推进圆形伤害倒计时。
        /// </summary>
        [Obsolete("战斗逻辑必须由 BattleManager 传入固定时间步。")]
        public void Tick()
        {
            Tick(1f / 60f);
        }

        /// <summary>
        /// 使用指定时间步长推进倒计时，并对到期实例按当前位置结算伤害。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_isDisposed || _instances.Count == 0)
            {
                return;
            }

            float safeDeltaTime = Math.Max(0f, deltaTime);
            _completedInstances.Clear();

            foreach (CircularDamageRuntimeData instance in _instances)
            {
                instance.RemainingDelay -= safeDeltaTime;
                if (instance.RemainingDelay > 0f)
                {
                    continue;
                }

                ApplyDamage(instance);
                _completedInstances.Add(instance);
            }

            foreach (CircularDamageRuntimeData instance in _completedInstances)
            {
                Recycle(instance);
            }

            _completedInstances.Clear();
        }

        /// <summary>
        /// 回收全部尚未触发的圆形伤害。
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
            foreach (CircularDamageRuntimeData instance in _completedInstances)
            {
                Recycle(instance);
            }

            _completedInstances.Clear();
        }

        /// <summary>
        /// 校验圆心、半径、延迟和伤害是否可以注册。
        /// </summary>
        public static bool AreParametersValid(Vector2 center, float radius, float delay, int damage)
        {
            return !float.IsNaN(center.x)
                   && !float.IsInfinity(center.x)
                   && !float.IsNaN(center.y)
                   && !float.IsInfinity(center.y)
                   && !float.IsNaN(radius)
                   && !float.IsInfinity(radius)
                   && radius >= 0f
                   && !float.IsNaN(delay)
                   && !float.IsInfinity(delay)
                   && delay >= 0f
                   && damage >= 0;
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
        /// 对受影响阵营中仍存活且中心点落在圆内（含边界）的棋子结算注册时锁定的伤害。
        /// </summary>
        private void ApplyDamage(CircularDamageRuntimeData instance)
        {
            if (instance == null || instance.AffectedCamps == null || instance.AffectedCamps.Count == 0)
            {
                return;
            }

            foreach (ChessController chess in _campManager.GetAllChesses())
            {
                if (!CanBeDamaged(chess, instance))
                {
                    continue;
                }

                chess.DamageReceiver.ApplyDamage(instance.Damage);
            }
        }

        /// <summary>
        /// 判断棋子是否属于受影响阵营、仍然存活，且中心点位于圆内。
        /// </summary>
        private static bool CanBeDamaged(ChessController chess, CircularDamageRuntimeData instance)
        {
            if (chess == null || !chess.IsAlive || chess.RuntimeData == null)
            {
                return false;
            }

            if (!instance.AffectedCamps.Contains(chess.Camp))
            {
                return false;
            }

            return IsInsideCircle(instance.Center, instance.Radius, chess.RuntimeData.CurrentPosition);
        }

        /// <summary>
        /// 判断棋盘坐标是否落在圆内（含边界），避免战斗线程调用 Unity 数学辅助 API。
        /// </summary>
        private static bool IsInsideCircle(Vector2 center, float radius, Vector2 position)
        {
            float x = position.x - center.x;
            float y = position.y - center.y;
            return (x * x) + (y * y) <= radius * radius;
        }

        /// <summary>
        /// 从活动集合移除并回收指定圆形伤害。
        /// </summary>
        private void Recycle(CircularDamageRuntimeData instance)
        {
            if (!_instances.Remove(instance))
            {
                return;
            }

            instance.Dispose();
        }
    }
}
