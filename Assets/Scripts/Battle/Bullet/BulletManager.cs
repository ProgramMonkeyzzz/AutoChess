using System;
using System.Collections.Generic;
using AutoChess.Utils;
using Chess;
using Chess.FSM.State;
using UnityEngine;

namespace AutoChess.Bullet
{
    /// <summary>
    /// 管理活动子弹的注册、移动、命中结算和对象池回收。
    /// </summary>
    public class BulletManager : IDisposable
    {
        /// <summary>当前正在飞行的活动子弹。</summary>
        private readonly HashSet<BulletRuntimeData> _bullets = new HashSet<BulletRuntimeData>();

        /// <summary>当前 Tick 中等待回收的子弹。</summary>
        private readonly List<BulletRuntimeData> _completedBullets = new List<BulletRuntimeData>();

        /// <summary>管理器是否已经释放。</summary>
        private bool _isDisposed;

        /// <summary>获取当前所有活动子弹。</summary>
        public IReadOnlyCollection<BulletRuntimeData> Bullets => _bullets;

        /// <summary>获取当前活动子弹数量。</summary>
        public int ActiveBulletCount => _bullets.Count;

        /// <summary>
        /// 创建子弹管理器并开始监听子弹发射事件。
        /// </summary>
        public BulletManager()
        {
            EventDispatcher.Instance.RegisterListener<BulletSpawnEvent>(OnBulletSpawn);
        }

        /// <summary>
        /// 从对象池注册一枚追踪指定目标的子弹。
        /// </summary>
        public BulletRuntimeData RegisterBullet(
            ChessController ownerChess,
            ChessController targetChess,
            float bulletSpeed)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(BulletManager));
            }

            if (!CanRegister(ownerChess, targetChess, bulletSpeed))
            {
                return null;
            }

            BulletRuntimeData bullet = ObjectPool.Fetch<BulletRuntimeData>();
            bullet.Initialize(ownerChess, targetChess, bulletSpeed);
            if (!_bullets.Add(bullet))
            {
                bullet.Dispose();
                throw new InvalidOperationException("同一个子弹实例不能被重复注册。");
            }

            return bullet;
        }

        /// <summary>
        /// 使用 Unity 帧时间更新所有活动子弹。
        /// </summary>
        [Obsolete("战斗逻辑必须由 BattleManager 传入固定时间步。")]
        public void Tick()
        {
            Tick(1f / 60f);
        }

        /// <summary>
        /// 使用指定时间步长更新子弹位置，并结算已抵达目标位置的子弹。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_isDisposed || _bullets.Count == 0)
            {
                return;
            }

            float safeDeltaTime = Math.Max(0f, deltaTime);
            _completedBullets.Clear();

            foreach (BulletRuntimeData bullet in _bullets)
            {
                RefreshTargetPosition(bullet);

                float maxDistanceDelta = bullet.Speed * safeDeltaTime;
                float remainingDistance = Distance(
                    bullet.CurrentPosition,
                    bullet.LastTargetPosition);
                bullet.CurrentPosition = MoveTowards(
                    bullet.CurrentPosition,
                    bullet.LastTargetPosition,
                    maxDistanceDelta);

                if (remainingDistance <= maxDistanceDelta || remainingDistance <= 0.000001f)
                {
                    ApplyHitIfValid(bullet);
                    _completedBullets.Add(bullet);
                }
            }

            foreach (BulletRuntimeData bullet in _completedBullets)
            {
                RecycleBullet(bullet);
            }

            _completedBullets.Clear();
        }

        /// <summary>
        /// 停止监听发射事件，并回收全部活动子弹。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            EventDispatcher.Instance.RemoveListener<BulletSpawnEvent>(OnBulletSpawn);

            _completedBullets.Clear();
            _completedBullets.AddRange(_bullets);
            foreach (BulletRuntimeData bullet in _completedBullets)
            {
                RecycleBullet(bullet);
            }

            _completedBullets.Clear();
        }

        /// <summary>
        /// 响应攻击控制器发送的子弹发射事件。
        /// </summary>
        private void OnBulletSpawn(BulletSpawnEvent spawnEvent)
        {
            RegisterBullet(spawnEvent.OwnerChess, spawnEvent.TargetChess, spawnEvent.BulletSpeed);
        }

        /// <summary>
        /// 验证一次子弹注册请求是否具有可用的攻击者、目标和速度。
        /// </summary>
        private static bool CanRegister(
            ChessController ownerChess,
            ChessController targetChess,
            float bulletSpeed)
        {
            return ownerChess != null
                   && ownerChess.RuntimeData != null
                   && ownerChess.BaseData != null
                   && targetChess != null
                   && targetChess.RuntimeData != null
                   && targetChess.RuntimeData.Hp > 0
                   && !(targetChess.Fsm.CurrentState is DeadState)
                   && bulletSpeed > 0f
                   && !float.IsNaN(bulletSpeed)
                   && !float.IsInfinity(bulletSpeed);
        }

        /// <summary>
        /// 追踪存活目标的最新位置；目标失效后保留最后位置并禁止伤害。
        /// </summary>
        private static void RefreshTargetPosition(BulletRuntimeData bullet)
        {
            if (IsTargetAlive(bullet.TargetChess))
            {
                bullet.LastTargetPosition = bullet.TargetChess.RuntimeData.CurrentPosition;
                return;
            }

            bullet.TargetChess = null;
            bullet.CanDamageTarget = false;
        }

        /// <summary>
        /// 在目标仍有效时使用创建时锁定的伤害快照结算伤害。
        /// </summary>
        private static void ApplyHitIfValid(BulletRuntimeData bullet)
        {
            if (!bullet.CanDamageTarget || !IsTargetAlive(bullet.TargetChess))
            {
                return;
            }

            bullet.TargetChess.DamageReceiver.ApplyDamage(bullet.Damage);
        }

        /// <summary>
        /// 判断目标棋子是否仍可被子弹命中。
        /// </summary>
        private static bool IsTargetAlive(ChessController targetChess)
        {
            return targetChess != null
                   && targetChess.RuntimeData != null
                   && targetChess.RuntimeData.Hp > 0
                   && !(targetChess.Fsm.CurrentState is DeadState);
        }

        /// <summary>
        /// 从活动集合移除并回收指定子弹。
        /// </summary>
        private void RecycleBullet(BulletRuntimeData bullet)
        {
            if (!_bullets.Remove(bullet))
            {
                return;
            }

            bullet.Dispose();
        }

        /// <summary>计算两个棋盘坐标之间的距离，避免战斗线程调用 Unity 数学辅助 API。</summary>
        private static float Distance(Vector2 left, Vector2 right)
        {
            float x = left.x - right.x;
            float y = left.y - right.y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        /// <summary>按最大步长向目标移动一个棋盘坐标。</summary>
        private static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
        {
            float x = target.x - current.x;
            float y = target.y - current.y;
            float distance = (float)Math.Sqrt(x * x + y * y);
            if (distance <= maxDistanceDelta || distance <= 0.000001f)
            {
                return target;
            }

            float scale = maxDistanceDelta / distance;
            return new Vector2(current.x + x * scale, current.y + y * scale);
        }
    }
}
