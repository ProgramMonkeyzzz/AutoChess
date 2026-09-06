using System;
using AutoChess;
using AutoChess.Bullet;
using AutoChess.Utils;
using Chess.FSM.State;
using Config;
using UnityEngine;

namespace Chess
{
    /// <summary>
    /// 根据当前索敌目标驱动攻击冷却、起手前摇和命中伤害；自动起手仅在未移动时发生。
    /// </summary>
    public sealed class AttackController
    {
        private readonly ChessController _chessController;
        private ChessController _windUpTarget;
        private float _windUpRemaining;
        private float _cooldownRemaining;
        /// <summary>是否已经报告过缺少子弹管理器监听器。</summary>
        private bool _reportedMissingBulletManager;

        /// <summary>当前是否处于攻击前摇阶段。</summary>
        public bool IsWindingUp => _windUpTarget != null;

        /// <summary>当前攻击冷却剩余时间。</summary>
        public float CooldownRemaining => _cooldownRemaining;

        /// <summary>当前是否已进入攻击流程，包括前摇和命中后的攻击冷却。</summary>
        public bool IsAttackInProgress => IsWindingUp || _cooldownRemaining > 0f;

        /// <summary>
        /// 创建绑定到指定棋子控制器的攻击控制器。
        /// </summary>
        public AttackController(ChessController chessController)
        {
            _chessController = chessController ?? throw new ArgumentNullException(nameof(chessController));
        }

        /// <summary>
        /// 兼容旧入口；正式战斗必须传入固定时间步。
        /// </summary>
        [Obsolete("战斗逻辑必须由 BattleManager 传入固定时间步。")]
        public void Tick()
        {
            Tick(1f / 60f);
        }

        /// <summary>
        /// 使用指定时间步长推进攻击冷却、前摇和命中流程。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!CanAttackConfiguration() || IsDead(_chessController) || !CanRunInCurrentState())
            {
                StopAttack();
                return;
            }

            float remainingTime = Math.Max(0f, deltaTime);
            while (true)
            {
                if (IsWindingUp)
                {
                    if (!IsValidWindUpTarget(_windUpTarget))
                    {
                        StopAttack();
                        return;
                    }

                    if (_windUpRemaining > remainingTime)
                    {
                        _windUpRemaining -= remainingTime;
                        return;
                    }

                    remainingTime -= _windUpRemaining;
                    _windUpRemaining = 0f;
                    ChessController target = _windUpTarget;
                    _windUpTarget = null;

                    if (!IsValidWindUpTarget(target))
                    {
                        return;
                    }

                    ApplyHit(target);
                    _cooldownRemaining = GetAttackInterval();
                    continue;
                }

                if (_cooldownRemaining > 0f)
                {
                    if (_cooldownRemaining > remainingTime)
                    {
                        _cooldownRemaining -= remainingTime;
                        return;
                    }

                    remainingTime -= _cooldownRemaining;
                    _cooldownRemaining = 0f;
                }

                // 移动过程中不自动起手，避免打断远离/追击；AttackTarget() 仍可强制开打。
                if (IsOwnerMoving() || !TryStartAttack())
                {
                    return;
                }

                if (remainingTime <= 0f)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 尝试根据当前索敌目标启动一次攻击前摇；零前摇攻击会立即命中。
        /// </summary>
        public void AttackTarget()
        {
            if (!CanAttackConfiguration() || IsDead(_chessController)
                || !CanRunInCurrentState() || IsWindingUp || _cooldownRemaining > 0f)
            {
                return;
            }

            TryStartAttack();
        }

        /// <summary>
        /// 停止当前攻击流程，取消前摇但保留命中后尚未结束的攻击冷却。
        /// </summary>
        public void StopAttack()
        {
            CancelWindUp();
        }

        /// <summary>
        /// 检查攻击配置是否可用；任一负数战斗参数表示棋子处于缴械状态。
        /// </summary>
        private bool CanAttackConfiguration()
        {
            ChessBaseData baseData = _chessController.BaseData;
            return baseData != null
                   && baseData.AtkSpeed > 0
                   && baseData.Range >= 0f
                   && baseData.WindUpTime >= 0f;
        }

        /// <summary>
        /// 判断棋子当前是否仍在执行未到达的移动命令。
        /// </summary>
        private bool IsOwnerMoving()
        {
            ChessMovementController movement = _chessController.MovementController;
            return movement != null && movement.IsMoving;
        }

        /// <summary>
        /// 判断当前状态是否允许攻击控制器推进或建立攻击流程。
        /// </summary>
        private bool CanRunInCurrentState()
        {
            return _chessController.Fsm.CurrentState is MovementState
                   || _chessController.Fsm.CurrentState is AttackState;
        }

        /// <summary>
        /// 尝试锁定当前合法目标并进入前摇或立即造成伤害。
        /// </summary>
        private bool TryStartAttack()
        {
            ChessController target = _chessController.TargetSelector?.SelectedChessController;
            if (!IsValidAttackStartTarget(target))
            {
                return false;
            }

            // 先锁定前摇目标，使 MovementToAttack 在检查转换时成立。
            _windUpTarget = target;
            _windUpRemaining = _chessController.BaseData.WindUpTime;
            _chessController.Fsm.CheckCurrentStateTransition();

            if (_chessController.BaseData.WindUpTime <= 0f)
            {
                _windUpTarget = null;
                _windUpRemaining = 0f;
                ApplyHit(target);
                _cooldownRemaining = GetAttackInterval();
            }

            return true;
        }

        /// <summary>
        /// 检查目标是否满足攻击启动条件，包括当前索敌目标、存活状态和攻击射程。
        /// </summary>
        private bool IsValidAttackStartTarget(ChessController target)
        {
            if (!IsValidWindUpTarget(target) || _chessController.RuntimeData == null)
            {
                return false;
            }

            return Vector2.Distance(_chessController.RuntimeData.CurrentPosition,
                       target.RuntimeData.CurrentPosition) <= _chessController.BaseData.Range;
        }

        /// <summary>
        /// 检查前摇锁定目标是否仍存在、未替换且存活，不重新检查攻击射程。
        /// </summary>
        private bool IsValidWindUpTarget(ChessController target)
        {
            if (target == null || ReferenceEquals(target, _chessController)
                || !ReferenceEquals(_chessController.TargetSelector?.SelectedChessController, target)
                || IsDead(target))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判断指定棋子是否已进入死亡状态。
        /// </summary>
        private static bool IsDead(ChessController chessController)
        {
            return chessController == null
                   || chessController.RuntimeData == null
                   || chessController.RuntimeData.Hp <= 0
                   || chessController.Fsm.CurrentState is DeadState;
        }

        /// <summary>
        /// 获取按每秒攻击次数换算得到的攻击间隔。
        /// </summary>
        private float GetAttackInterval()
        {
            return 1f / _chessController.BaseData.AtkSpeed;
        }

        /// <summary>
        /// 根据子弹速度选择发送子弹事件或直接结算即时伤害。
        /// </summary>
        private void ApplyHit(ChessController target)
        {
            if (_chessController.BaseData.BulletSpeed > 0f)
            {
                if (EventDispatcher.Instance.GetListenerCount<BulletSpawnEvent>() <= 0)
                {
                    if (!_reportedMissingBulletManager)
                    {
                        _reportedMissingBulletManager = true;
                        ThreadLogQueue.Enqueue(ThreadLogLevel.Error,
                            "AttackController 未找到监听 BulletSpawnEvent 的 BulletManager，子弹伤害不会结算。");
                    }

                    return;
                }

                _reportedMissingBulletManager = false;
                EventDispatcher.Instance.Send(new BulletSpawnEvent(
                    _chessController,
                    target,
                    _chessController.BaseData.BulletSpeed));
                return;
            }

            int damage = _chessController.RuntimeData.GetCurrentAtk(_chessController.BaseData.Atk);
            target.DamageReceiver.ApplyDamage(damage);
        }

        /// <summary>
        /// 取消尚未完成的攻击前摇，不产生伤害也不启动冷却。
        /// </summary>
        private void CancelWindUp()
        {
            _windUpTarget = null;
            _windUpRemaining = 0f;
        }
    }
}
