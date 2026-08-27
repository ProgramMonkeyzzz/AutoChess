using System;

namespace Chess.FSM
{
    public abstract class StateBase
    {
        protected readonly FSM _fsm;

        /// <summary>
        /// 获取该状态所属的状态机。
        /// </summary>
        public FSM Fsm => _fsm;

        protected StateBase(FSM fsm)
        {
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
        }

        public virtual void OnStateEnter()
        {
        }

        public virtual void OnStateExit()
        {
        }

        public virtual void OnStateStay()
        {
        }
    }
}
