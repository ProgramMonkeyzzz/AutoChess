using System;
using Chess.FSM.State;
using Config;
using UnityEngine;

namespace Chess
{
    /// <summary>
    /// 根据当前索敌目标驱动攻击冷却、起手前摇和命中伤害。
    /// </summary>
    public sealed class AttackController
    {
        private readonly ChessController _chessController;
        private ChessController _windUpTarget;
        private float _windUpRemaining;
        private float _cooldownRemaining;

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
        /// 使用 Unity 帧时间推进攻击流程。
        /// </summary>
        public void Tick()
        {
            Tick(Time.deltaTime);
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

            float remainingTime = Mathf.Max(0f, deltaTime);
            while (true)
            {
                if (IsWindingUp)
                {
                    if (!IsValidTarget(_windUpTarget))
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

                    if (!IsValidTarget(target))
                    {
                        return;
                    }

                    target.DamageReceiver.ApplyDamage(_chessController.RuntimeData.GetCurrentAtk(_chessController.BaseData.Atk));
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

                if (!TryStartAttack())
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
                   && baseData.Range >= 0
                   && baseData.WindUpTime >= 0f;
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
            if (!IsValidTarget(target))
            {
                return false;
            }

            // 合法攻击开始时立即进入攻击状态，进入状态会停止当前移动命令。
            _chessController.EnterAttackState();

            if (_chessController.BaseData.WindUpTime <= 0f)
            {
                target.DamageReceiver.ApplyDamage(_chessController.RuntimeData.GetCurrentAtk(_chessController.BaseData.Atk));
                _cooldownRemaining = GetAttackInterval();
                return true;
            }

            _windUpTarget = target;
            _windUpRemaining = _chessController.BaseData.WindUpTime;
            return true;
        }

        /// <summary>
        /// 检查目标是否仍为当前索敌目标、存活且处于攻击射程内。
        /// </summary>
        private bool IsValidTarget(ChessController target)
        {
            if (target == null || ReferenceEquals(target, _chessController)
                || !ReferenceEquals(_chessController.TargetSelector?.SelectedChessController, target)
                || IsDead(target) || target.RuntimeData == null || _chessController.RuntimeData == null)
            {
                return false;
            }

            return Vector2.Distance(_chessController.RuntimeData.CurrentPosition,
                       target.RuntimeData.CurrentPosition) <= _chessController.BaseData.Range;
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
        /// 取消尚未完成的攻击前摇，不产生伤害也不启动冷却。
        /// </summary>
        private void CancelWindUp()
        {
            _windUpTarget = null;
            _windUpRemaining = 0f;
        }
    }
}
