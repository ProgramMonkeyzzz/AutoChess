using System;

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
}
