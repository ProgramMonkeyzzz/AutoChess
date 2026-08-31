using System;
using System.Collections.Generic;
using Chess;

namespace Chess.FSM
{
    public class FSM
    {
        private readonly ChessController _chessController;
        private readonly Dictionary<Type, StateBase> _stateDict;
        private readonly Dictionary<Type, HashSet<StateTransition>> _transitionDict;
        private StateBase _currentState;
        private bool _isStarted;

        public ChessController ChessController => _chessController;

        public StateBase CurrentState => _currentState;

        public bool IsStarted => _isStarted;

        public FSM(ChessController chessController)
        {
            _chessController = chessController ?? throw new ArgumentNullException(nameof(chessController));
            _stateDict = new Dictionary<Type, StateBase>();
            _transitionDict = new Dictionary<Type, HashSet<StateTransition>>();
        }

        /// <summary>
        /// 注册状态实例。状态必须由当前 FSM 构造。
        /// </summary>
        public void AddState(StateBase state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (state.Fsm != this)
            {
                throw new InvalidOperationException("状态必须绑定到当前 FSM。");
            }

            Type stateType = state.GetType();
            if (_stateDict.ContainsKey(stateType))
            {
                throw new ArgumentException("当前 FSM 已注册该状态类型。", nameof(state));
            }

            _stateDict.Add(stateType, state);
        }

        /// <summary>
        /// 注册状态转换。源状态和目标状态必须已经注册。
        /// </summary>
        public void AddTransition(StateTransition transition)
        {
            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition));
            }
            if (transition.Fsm != this)
            {
                throw new InvalidOperationException("状态转换必须绑定到当前 FSM。");
            }
            if (!_stateDict.ContainsKey(transition.State1Type))
            {
                throw new InvalidOperationException("状态转换的源状态尚未注册。\n" + transition.State1Type);
            }
            if (!_stateDict.ContainsKey(transition.State2Type))
            {
                throw new InvalidOperationException("状态转换的目标状态尚未注册。\n" + transition.State2Type);
            }

            if (!_transitionDict.TryGetValue(transition.State1Type, out HashSet<StateTransition> transitions))
            {
                transitions = new HashSet<StateTransition>();
                _transitionDict.Add(transition.State1Type, transitions);
            }
            if (transitions.Contains(transition))
            {
                throw new ArgumentException("当前 FSM 已注册该状态转换实例。", nameof(transition));
            }

            transitions.Add(transition);
        }

        /// <summary>
        /// 进入初始状态并触发 OnStateEnter。
        /// </summary>
        public void Start<TState>() where TState : StateBase
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("FSM 已经启动。 ");
            }

            Type stateType = typeof(TState);
            StateBase state = GetRegisteredState(stateType);
            _currentState = state;
            _isStarted = true;
            state.OnStateEnter();
        }

        /// <summary>
        /// 显式切换到指定状态。未启动时可作为初始状态进入。
        /// </summary>
        public bool ChangeState<TState>() where TState : StateBase
        {
            Type stateType = typeof(TState);
            StateBase nextState = GetRegisteredState(stateType);
            if (_currentState != null && _currentState.GetType() == stateType)
            {
                return false;
            }

            if (_currentState != null)
            {
                _currentState.OnStateExit();
            }

            _currentState = nextState;
            _isStarted = true;
            _currentState.OnStateEnter();
            return true;
        }

        /// <summary>
        /// 使用零时间步检查当前状态的转换，并执行当前状态的停留逻辑。
        /// </summary>
        public void Tick()
        {
            Tick(0f);
        }

        /// <summary>
        /// 使用指定时间步检查当前状态的转换，并执行当前状态的停留逻辑。
        /// </summary>
        /// <param name="deltaTime">传递给当前状态及其停留行为树的时间步长。</param>
        public void Tick(float deltaTime)
        {
            if (!_isStarted || _currentState == null)
            {
                return;
            }

            if (_transitionDict.TryGetValue(_currentState.GetType(), out HashSet<StateTransition> transitions))
            {
                foreach (StateTransition transition in transitions)
                {
                    if (transition.CanTransition())
                    {
                        ChangeStateByType(transition.State2Type);
                        break;
                    }
                }
            }

            _currentState.OnStateStay(deltaTime);
        }

        private StateBase GetRegisteredState(Type stateType)
        {
            if (!_stateDict.TryGetValue(stateType, out StateBase state))
            {
                throw new InvalidOperationException("状态尚未注册。\n" + stateType);
            }

            return state;
        }

        private void ChangeStateByType(Type stateType)
        {
            StateBase nextState = GetRegisteredState(stateType);
            if (_currentState.GetType() == stateType)
            {
                return;
            }

            _currentState.OnStateExit();
            _currentState = nextState;
            _currentState.OnStateEnter();
        }
    }
}
