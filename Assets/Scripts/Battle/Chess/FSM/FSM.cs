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
        /// 检查当前状态的全部转换，执行第一个可切换的目标并返回该状态。
        /// 未启动或无当前状态时返回 null；没有可切换转换时返回当前状态。
        /// </summary>
        public StateBase CheckCurrentStateTransition()
        {
            if (!_isStarted || _currentState == null)
            {
                return null;
            }

            if (!_transitionDict.TryGetValue(_currentState.GetType(), out HashSet<StateTransition> transitions))
            {
                return _currentState;
            }

            foreach (StateTransition transition in transitions)
            {
                if (!transition.CanTransition())
                {
                    continue;
                }

                StateBase nextState = GetRegisteredState(transition.State2Type);
                if (!ReferenceEquals(_currentState, nextState))
                {
                    ChangeState(nextState);
                }

                return nextState;
            }

            return _currentState;
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

            CheckCurrentStateTransition();
            _currentState.OnStateStay(deltaTime);
        }

        /// <summary>按类型读取已注册状态。</summary>
        public TState GetState<TState>() where TState : StateBase
        {
            return (TState)GetRegisteredState(typeof(TState));
        }

        /// <summary>
        /// 按类型读取已注册状态，未注册时抛出异常。
        /// </summary>
        private StateBase GetRegisteredState(Type stateType)
        {
            if (!_stateDict.TryGetValue(stateType, out StateBase state))
            {
                throw new InvalidOperationException("状态尚未注册。\n" + stateType);
            }

            return state;
        }

        /// <summary>
        /// 切换到指定状态实例。切到当前状态时返回 false 且不重入生命周期。
        /// </summary>
        private bool ChangeState(StateBase nextState)
        {
            if (nextState == null)
            {
                throw new ArgumentNullException(nameof(nextState));
            }
            if (ReferenceEquals(_currentState, nextState))
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
    }
}
