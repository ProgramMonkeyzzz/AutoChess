using System;
using Chess.FSM.State;

namespace Chess.FSM
{
    /// <summary>
    /// 状态转换的非泛型基类，供 FSM 统一存储。
    /// </summary>
    public abstract class StateTransition
    {
        protected readonly FSM _fsm;

        /// <summary>
        /// 获取该转换所属的状态机。
        /// </summary>
        public FSM Fsm => _fsm;

        public abstract Type State1Type { get; }

        public abstract Type State2Type { get; }

        protected StateTransition(FSM fsm)
        {
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
        }

        /// <summary>
        /// 返回当前是否可以执行该状态转换。
        /// </summary>
        public abstract bool CanTransition();
    }

    /// <summary>
    /// 状态从 State1 切换到 State2。
    /// </summary>
    /// <typeparam name="State1">源状态类型。</typeparam>
    /// <typeparam name="State2">目标状态类型。</typeparam>
    public abstract class StateTransition<State1, State2> : StateTransition
        where State1 : StateBase
        where State2 : StateBase
    {
        protected StateTransition(FSM fsm) : base(fsm)
        {
        }

        public override Type State1Type => typeof(State1);

        public override Type State2Type => typeof(State2);
    }

    /// <summary>
    /// 棋子 HP 归零时从移动状态切换到死亡状态。
    /// </summary>
    public sealed class MovementToDeadTransition : StateTransition<MovementState, DeadState>
    {
        /// <summary>
        /// 创建绑定到指定状态机的死亡转换。
        /// </summary>
        public MovementToDeadTransition(FSM fsm) : base(fsm)
        {
        }

        /// <summary>
        /// 当棋子运行时 HP 小于等于零时允许进入死亡状态。
        /// </summary>
        public override bool CanTransition()
        {
            return _fsm.ChessController.RuntimeData != null
                   && _fsm.ChessController.RuntimeData.Hp <= 0;
        }
    }

    /// <summary>
    /// 攻击流程开始时从移动状态切换到攻击状态。
    /// </summary>
    public sealed class MovementToAttackTransition : StateTransition<MovementState, AttackState>
    {
        /// <summary>
        /// 创建绑定到指定状态机的攻击进入转换。
        /// </summary>
        public MovementToAttackTransition(FSM fsm) : base(fsm)
        {
        }

        /// <summary>
        /// 当攻击控制器已进入攻击前摇阶段时允许切换。
        /// </summary>
        public override bool CanTransition()
        {
            return _fsm.ChessController.AttackController != null
                   && _fsm.ChessController.RuntimeData != null
                   && _fsm.ChessController.RuntimeData.Hp > 0
                   && _fsm.ChessController.AttackController.IsWindingUp;
        }
    }

    /// <summary>
    /// 攻击状态下存在移动命令，或不在前摇期间时，切换回移动状态。
    /// </summary>
    public sealed class AttackToMovementTransition : StateTransition<AttackState, MovementState>
    {
        /// <summary>
        /// 创建绑定到指定状态机的移动退出转换。
        /// </summary>
        public AttackToMovementTransition(FSM fsm) : base(fsm)
        {
        }

        /// <summary>
        /// 当存在有效移动命令，或攻击控制器当前不在前摇期间时允许切换。
        /// </summary>
        public override bool CanTransition()
        {
            if (_fsm.ChessController.RuntimeData == null
                || _fsm.ChessController.RuntimeData.Hp <= 0)
            {
                return false;
            }

            if (_fsm.ChessController.MovementController != null
                && _fsm.ChessController.MovementController.HasDestination)
            {
                return true;
            }

            return _fsm.ChessController.AttackController == null
                   || !_fsm.ChessController.AttackController.IsWindingUp;
        }
    }

    /// <summary>
    /// 攻击状态下棋子 HP 归零时切换到死亡状态。
    /// </summary>
    public sealed class AttackToDeadTransition : StateTransition<AttackState, DeadState>
    {
        /// <summary>
        /// 创建绑定到指定状态机的攻击死亡转换。
        /// </summary>
        public AttackToDeadTransition(FSM fsm) : base(fsm)
        {
        }

        /// <summary>
        /// 当棋子运行时 HP 小于等于零时允许进入死亡状态。
        /// </summary>
        public override bool CanTransition()
        {
            return _fsm.ChessController.RuntimeData != null
                   && _fsm.ChessController.RuntimeData.Hp <= 0;
        }
    }

}
