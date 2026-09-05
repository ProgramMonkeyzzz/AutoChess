using System;
using AutoChess.Navigation;
using Chess.FSM.State;
using Config;
using UnityEngine;

namespace Chess
{
    /// <summary>
    /// 保存棋子移动、追踪和远离命令，并在线程间生成和消费纯数据导航快照。
    /// </summary>
    public sealed class ChessMovementController
    {
        /// <summary>棋子的固定碰撞半径。</summary>
        public const float ChessRadius = ChessBaseData.Radius;

        /// <summary>两个棋子保持不重叠时的最小中心距。</summary>
        public const float NoOverlapDistance = ChessRadius * 2f;

        private const float PositionEpsilon = 0.001f;

        private enum MovementMode
        {
            None,
            FixedPosition,
            ChessTarget,
            /// <summary>持续向当前目标的反方向移动。</summary>
            FleeTarget
        }

        private readonly ChessController _chessController;
        private readonly AutoChess.ChessBoard _chessBoard;
        private readonly NavigationAgentHandle _agent;
        private MovementMode _movementMode;
        private Vector2 _destination;
        private bool _hasDestination;
        private bool _lastReachedDestination;
        private bool _lastAvoidingAgents;
        private int _commandVersion;

        /// <summary>当前追踪或远离的目标棋子；固定点移动时为空。</summary>
        public ChessController TargetChess { get; private set; }

        /// <summary>当前移动命令的目标位置。</summary>
        public Vector2 Destination => _destination;

        /// <summary>当前是否存在有效移动命令。</summary>
        public bool HasDestination => _hasDestination;

        /// <summary>当前导航代理句柄。</summary>
        public NavigationAgentHandle Agent => _agent;

        /// <summary>当前是否仍需向命令目标移动。</summary>
        public bool IsMoving => _hasDestination && !HasReachedDestination;

        /// <summary>当前命令是否已到达固定点、目标安全距离或远离终点。</summary>
        public bool HasReachedDestination
        {
            get
            {
                if (!_hasDestination)
                {
                    return false;
                }

                if (_movementMode == MovementMode.ChessTarget)
                {
                    return TargetChess != null && TargetChess.RuntimeData != null
                           && Distance(_chessController.RuntimeData.CurrentPosition,
                               TargetChess.RuntimeData.CurrentPosition) <= GetSafeTargetDistance();
                }

                if (_movementMode == MovementMode.FleeTarget)
                {
                    return _chessController.RuntimeData != null
                           && (Distance(_chessController.RuntimeData.CurrentPosition, _destination)
                               <= PositionEpsilon || _lastReachedDestination);
                }

                return _lastReachedDestination;
            }
        }

        /// <summary>最近一次导航结果是否显示代理正在避让其他棋子。</summary>
        public bool IsAvoidingAgents => _lastAvoidingAgents;

        /// <summary>创建绑定棋子、棋盘和导航代理句柄的移动控制器。</summary>
        public ChessMovementController(
            ChessController chessController,
            AutoChess.ChessBoard chessBoard,
            NavigationAgentHandle agent)
        {
            _chessController = chessController ?? throw new ArgumentNullException(nameof(chessController));
            _chessBoard = chessBoard ?? throw new ArgumentNullException(nameof(chessBoard));
            if (!agent.IsValid)
            {
                throw new ArgumentException("导航代理句柄无效。", nameof(agent));
            }

            _agent = agent;
            if (_chessController.RuntimeData != null
                && !_chessBoard.ContainsPosition(_chessController.RuntimeData.CurrentPosition))
            {
                _chessController.RuntimeData.CurrentPosition = ClampPosition(_chessController.RuntimeData.CurrentPosition);
            }
        }

        /// <summary>发起或替换一次固定位置移动。</summary>
        public bool MoveToPosition(Vector2 position)
        {
            if (!CanMove() || !IsValidDestination(position))
            {
                return false;
            }

            _movementMode = MovementMode.FixedPosition;
            _destination = ClampPosition(position);
            _hasDestination = true;
            _lastReachedDestination = false;
            _lastAvoidingAgents = false;
            TargetChess = null;
            _commandVersion++;
            _chessController.EnterMovementState();
            return true;
        }

        /// <summary>发起或替换一次追踪棋子移动，并保持双方半径之和的中心距。</summary>
        public bool MoveToTarget(ChessController target)
        {
            if (target == null || ReferenceEquals(target, _chessController))
            {
                Stop();
                return false;
            }

            if (!CanMove() || target.RuntimeData == null
                || !IsValidDestination(target.RuntimeData.CurrentPosition))
            {
                Stop();
                return false;
            }

            _movementMode = MovementMode.ChessTarget;
            _hasDestination = true;
            _lastReachedDestination = false;
            _lastAvoidingAgents = false;
            TargetChess = target;
            _commandVersion++;
            if (!RefreshTargetDestination())
            {
                Stop();
                return false;
            }

            _chessController.EnterMovementState();
            return true;
        }

        /// <summary>发起或替换一次远离目标的移动，并持续把终点刷新到反方向棋盘边缘。</summary>
        public bool MoveAwayFromTarget(ChessController target)
        {
            if (target == null || ReferenceEquals(target, _chessController))
            {
                Stop();
                return false;
            }

            if (!CanMove() || target.RuntimeData == null
                || !IsValidDestination(target.RuntimeData.CurrentPosition))
            {
                Stop();
                return false;
            }

            _movementMode = MovementMode.FleeTarget;
            _hasDestination = true;
            _lastReachedDestination = false;
            _lastAvoidingAgents = false;
            TargetChess = target;
            _commandVersion++;
            if (!RefreshFleeDestination())
            {
                Stop();
                return false;
            }

            _chessController.EnterMovementState();
            return true;
        }

        /// <summary>停止并清除当前移动命令。</summary>
        public void Stop()
        {
            ResetCommand();
        }

        /// <summary>根据战斗线程当前状态生成一个不包含 Unity 对象的导航输入快照。</summary>
        public NavigationInputSnapshot BuildNavigationInput(long stepId)
        {
            if (!RefreshTrackedDestination())
            {
                ResetCommand();
            }

            Vector2 currentPosition = _chessController.RuntimeData == null
                ? Vector2.zero
                : ClampPosition(_chessController.RuntimeData.CurrentPosition);
            Vector2 targetPosition = _hasDestination ? ClampPosition(_destination) : currentPosition;
            float maxSpeed = GetMoveSpeed();
            float distance = Distance(currentPosition, targetPosition);
            float desiredSpeed = _hasDestination && !_lastReachedDestination && distance > PositionEpsilon
                ? maxSpeed
                : 0f;
            bool enabled = _hasDestination && !IsDead() && desiredSpeed > 0f;
            return new NavigationInputSnapshot(
                stepId,
                _agent,
                currentPosition,
                targetPosition,
                desiredSpeed,
                maxSpeed,
                enabled,
                _commandVersion);
        }

        /// <summary>消费主线程返回的导航结果并更新棋子运行时坐标。</summary>
        public bool ApplyNavigationResult(NavigationResultSnapshot result)
        {
            if (result.Agent != _agent || result.CommandVersion != _commandVersion
                || _chessController.RuntimeData == null)
            {
                return false;
            }

            _chessController.RuntimeData.CurrentPosition = ClampPosition(result.Position);
            _lastReachedDestination = result.ReachedDestination;
            _lastAvoidingAgents = result.AvoidingAgents;
            return true;
        }

        /// <summary>兼容旧的单棋子 Tick 入口；导航结果由 BattleManager 统一消费。</summary>
        public void Tick()
        {
            if (!RefreshTrackedDestination())
            {
                ResetCommand();
            }
        }

        /// <summary>按当前命令模式刷新追踪或远离终点；固定点命令保持原终点。</summary>
        private bool RefreshTrackedDestination()
        {
            if (_movementMode == MovementMode.ChessTarget)
            {
                return RefreshTargetDestination();
            }

            if (_movementMode == MovementMode.FleeTarget)
            {
                return RefreshFleeDestination();
            }

            return true;
        }

        /// <summary>刷新追踪目标对应的安全终点。</summary>
        private bool RefreshTargetDestination()
        {
            if (TargetChess == null || TargetChess.RuntimeData == null
                || !IsValidDestination(TargetChess.RuntimeData.CurrentPosition))
            {
                return false;
            }

            Vector2 currentPosition = _chessController.RuntimeData.CurrentPosition;
            Vector2 targetPosition = TargetChess.RuntimeData.CurrentPosition;
            Vector2 toOwner = currentPosition - targetPosition;
            float distance = Length(toOwner);
            float safeDistance = GetSafeTargetDistance();
            if (distance <= safeDistance)
            {
                _destination = currentPosition;
                _lastReachedDestination = true;
                return true;
            }

            Vector2 direction = toOwner / distance;
            Vector2 nextDestination = targetPosition + direction * safeDistance;
            if (!IsValidDestination(nextDestination))
            {
                return false;
            }

            if (Distance(nextDestination, _destination) > PositionEpsilon)
            {
                _lastReachedDestination = false;
            }

            _destination = ClampPosition(nextDestination);
            return true;
        }

        /// <summary>刷新远离目标时指向棋盘边缘的终点。</summary>
        private bool RefreshFleeDestination()
        {
            if (TargetChess == null || TargetChess.RuntimeData == null
                || !IsValidDestination(TargetChess.RuntimeData.CurrentPosition)
                || _chessController.RuntimeData == null)
            {
                return false;
            }

            Vector2 currentPosition = _chessController.RuntimeData.CurrentPosition;
            Vector2 targetPosition = TargetChess.RuntimeData.CurrentPosition;
            Vector2 away = currentPosition - targetPosition;
            float distance = Length(away);
            if (distance <= PositionEpsilon)
            {
                away = Vector2.up;
                distance = 1f;
            }

            float fleeDistance = Math.Max(_chessBoard.Width, _chessBoard.Height);
            Vector2 nextDestination = ClampPosition(currentPosition + away / distance * fleeDistance);
            if (Distance(nextDestination, currentPosition) <= PositionEpsilon)
            {
                _destination = currentPosition;
                _lastReachedDestination = true;
                return true;
            }

            if (Distance(nextDestination, _destination) > PositionEpsilon)
            {
                _lastReachedDestination = false;
            }

            _destination = nextDestination;
            return true;
        }

        /// <summary>获取考虑双方半径后的目标安全距离。</summary>
        private float GetSafeTargetDistance() => NoOverlapDistance;

        /// <summary>检查移动命令是否允许建立。</summary>
        private bool CanMove()
        {
            return _chessController.RuntimeData != null
                   && _chessBoard.ContainsPosition(_chessController.RuntimeData.CurrentPosition)
                   && !IsDead();
        }

        /// <summary>判断棋子是否已经死亡。</summary>
        private bool IsDead()
        {
            return _chessController.RuntimeData == null
                   || _chessController.RuntimeData.Hp <= 0
                   || _chessController.Fsm.CurrentState is DeadState;
        }

        /// <summary>获取合法的棋子移动速度。</summary>
        private float GetMoveSpeed()
        {
            float speed = _chessController.BaseData == null ? 0f : _chessController.BaseData.MoveSpeed;
            return IsFinite(speed) && speed > 0f ? speed : 0f;
        }

        /// <summary>清除命令状态但不改变 FSM 当前状态。</summary>
        private void ResetCommand()
        {
            _movementMode = MovementMode.None;
            _destination = Vector2.zero;
            _hasDestination = false;
            _lastReachedDestination = false;
            _lastAvoidingAgents = false;
            TargetChess = null;
            _commandVersion++;
        }

        /// <summary>判断目标点是否为有限且位于棋盘中心点范围内的坐标。</summary>
        private bool IsValidDestination(Vector2 position)
        {
            return IsFinite(position.x) && IsFinite(position.y)
                   && _chessBoard.ContainsPosition(position);
        }

        /// <summary>将中心点限制到考虑棋子半径后的棋盘范围。</summary>
        private Vector2 ClampPosition(Vector2 position)
        {
            float min = ChessRadius;
            float maxX = Math.Max(min, _chessBoard.Width - ChessRadius);
            float maxY = Math.Max(min, _chessBoard.Height - ChessRadius);
            return new Vector2(
                Clamp(Sanitize(position.x), min, maxX),
                Clamp(Sanitize(position.y), min, maxY));
        }

        /// <summary>计算二维向量长度。</summary>
        private static float Length(Vector2 value) => (float)Math.Sqrt(value.x * value.x + value.y * value.y);

        /// <summary>计算两个二维点之间的距离。</summary>
        private static float Distance(Vector2 left, Vector2 right) => Length(left - right);

        /// <summary>将浮点值限制在指定区间内。</summary>
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));

        /// <summary>判断浮点值是否有限。</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>将无效浮点值转换为零。</summary>
        private static float Sanitize(float value) => IsFinite(value) ? value : 0f;
    }
}
