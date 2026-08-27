using System;
using AutoChess;
using Config;
using Pathfinding;
using Pathfinding.RVO;
using UnityEngine;

namespace Chess
{
    /// <summary>
    /// 控制单个棋子的 A* 移动，并将 Unity 世界位置同步到棋子运行时数据。
    /// </summary>
    public sealed class ChessMovementController
    {
        /// <summary>棋子的固定半径。</summary>
        public const float ChessRadius = ChessBaseData.Radius;

        /// <summary>两个棋子不重叠时所需的最小中心距。</summary>
        public const float NoOverlapDistance = ChessRadius * 2f;

        private const float PositionEpsilon = 0.001f;

        private enum MovementMode
        {
            None,
            FixedPosition,
            ChessTarget
        }

        private readonly ChessController _chessController;
        private readonly ChessBoard _chessBoard;
        private readonly IAstarAI _astarAI;
        private readonly RVOController _rvoController;

        private MovementMode _movementMode;
        private Vector2 _destination;
        private bool _hasDestination;
        private bool _movementAvailable = true;
        private bool _reportedMissingRvo;

        /// <summary>当前追踪的棋子；固定坐标移动时为空。</summary>
        public ChessController TargetChess { get; private set; }

        /// <summary>当前命令对应的棋盘 XZ 坐标。</summary>
        public Vector2 Destination => _destination;

        /// <summary>当前是否存在有效移动命令。</summary>
        public bool HasDestination => _hasDestination;

        /// <summary>当前是否仍在向命令目标移动。</summary>
        public bool IsMoving => _hasDestination && !HasReachedDestination;

        /// <summary>当前命令是否已经到达目标位置或目标棋子外侧。</summary>
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
                    return TargetChess != null && IsTargetWithinSafeDistance();
                }

                return _movementMode == MovementMode.FixedPosition
                       && !_astarAI.pathPending
                       && _astarAI.reachedDestination;
            }
        }

        /// <summary>
        /// 创建移动控制器并绑定棋子、棋盘和 A* 移动组件。
        /// </summary>
        public ChessMovementController(ChessController chessController, ChessBoard chessBoard, IAstarAI astarAI)
        {
            _chessController = chessController ?? throw new ArgumentNullException(nameof(chessController));
            _chessBoard = chessBoard ?? throw new ArgumentNullException(nameof(chessBoard));
            _astarAI = astarAI ?? throw new ArgumentNullException(nameof(astarAI));

            // A* 的半径只描述代理尺寸，连续的棋子避让由 RVOController 完成。
            _astarAI.radius = ChessRadius;
            if (_chessController.BaseData != null && _chessController.BaseData.MoveSpeed > 0)
            {
                _astarAI.maxSpeed = _chessController.BaseData.MoveSpeed;
            }

            // IAstarAI 实现通常是挂在棋子 GameObject 上的 MonoBehaviour。
            if (_astarAI is Component aiComponent)
            {
                _rvoController = aiComponent.GetComponent<RVOController>();
                if (_rvoController == null)
                {
                    ReportMissingRvo();
                }
                else
                {
                    _rvoController.radius = ChessRadius;
                }
            }

            SyncRuntimeData();
        }

        /// <summary>
        /// 发起或替换一次固定坐标移动。
        /// </summary>
        public bool MoveToPosition(Vector2 position)
        {
            if (!CanMove() || !_chessBoard.ContainsPosition(position))
            {
                return false;
            }

            _movementMode = MovementMode.FixedPosition;
            _destination = position;
            _hasDestination = true;
            TargetChess = null;
            _astarAI.isStopped = false;

            if (!ApplyDestination(position))
            {
                ResetCommand();
                return false;
            }

            // 只有成功建立路径后才打断其他状态并进入移动状态。
            _chessController.EnterMovementState();
            return true;
        }

        /// <summary>
        /// 发起或替换一次追踪棋子移动，并保持至少一个棋子直径的中心距。
        /// </summary>
        public bool MoveToTarget(ChessController target)
        {
            if (target == null || ReferenceEquals(target, _chessController))
            {
                Stop();
                return false;
            }

            if (!CanMove() || target.RuntimeData == null
                || !_chessBoard.ContainsPosition(target.RuntimeData.CurrentPosition))
            {
                Stop();
                return false;
            }

            _movementMode = MovementMode.ChessTarget;
            _hasDestination = true;
            TargetChess = target;
            if (!RefreshTargetDestination(true))
            {
                // 首次寻路失败时不保留半完成的追踪命令。
                Stop();
                return false;
            }

            // 目标已进入安全距离也属于 MovementState 下的静止待机。
            _chessController.EnterMovementState();
            return true;
        }

        /// <summary>
        /// 停止移动并清除当前固定坐标或追踪目标命令。
        /// </summary>
        public void Stop()
        {
            if (_astarAI != null)
            {
                _astarAI.isStopped = true;
            }

            ResetCommand();
        }

        /// <summary>
        /// 每帧调用，刷新追踪目标并同步运行时坐标。
        /// </summary>
        public void Tick()
        {
            if (!SyncRuntimeData())
            {
                Stop();
                return;
            }

            if (_movementMode == MovementMode.ChessTarget && !RefreshTargetDestination(false))
            {
                Stop();
            }
        }

        /// <summary>
        /// 将 A* 组件的三维世界位置投影到棋盘 XZ 坐标。
        /// </summary>
        private bool SyncRuntimeData()
        {
            if (_chessController.RuntimeData == null)
            {
                return false;
            }

            Vector3 position = _astarAI.position;
            _chessController.RuntimeData.CurrentPosition = new Vector2(position.x, position.z);
            return _chessBoard.ContainsPosition(_chessController.RuntimeData.CurrentPosition);
        }

        /// <summary>
        /// 根据目标棋子当前位置刷新安全终点。
        /// </summary>
        private bool RefreshTargetDestination(bool forceSearch)
        {
            if (TargetChess == null || TargetChess.RuntimeData == null)
            {
                return false;
            }

            Vector2 targetPosition = TargetChess.RuntimeData.CurrentPosition;
            if (!_chessBoard.ContainsPosition(targetPosition))
            {
                return false;
            }

            Vector2 currentPosition = _chessController.RuntimeData.CurrentPosition;
            Vector2 toOwner = currentPosition - targetPosition;
            float distance = toOwner.magnitude;
            float safeDistance = GetSafeTargetDistance();

            // 已经处于安全距离内时锁定当前位置，避免继续靠近目标。
            if (distance <= safeDistance)
            {
                _destination = currentPosition;
                _astarAI.isStopped = true;
                return true;
            }

            Vector2 direction = toOwner / distance;
            Vector2 nextDestination = targetPosition + direction * safeDistance;

            if (!_chessBoard.ContainsPosition(nextDestination))
            {
                return false;
            }

            bool destinationChanged = !_hasDestination
                                      || (nextDestination - _destination).sqrMagnitude
                                      > PositionEpsilon * PositionEpsilon;
            _destination = nextDestination;
            _hasDestination = true;
            _astarAI.isStopped = false;

            // 目标未移动时不重复发起异步寻路请求。
            if (!destinationChanged && !forceSearch)
            {
                return true;
            }

            return ApplyDestination(nextDestination);
        }

        /// <summary>
        /// 将棋盘坐标转换为世界坐标，并立即请求 A* 重算路径。
        /// </summary>
        private bool ApplyDestination(Vector2 destination)
        {
            try
            {
                Vector3 currentPosition = _astarAI.position;
                _astarAI.destination = new Vector3(destination.x, currentPosition.y, destination.y);
                _astarAI.SearchPath();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"棋子 A* 寻路请求失败：{exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取考虑 A* 到达容差后的安全追踪距离。
        /// </summary>
        private float GetSafeTargetDistance()
        {
            float arrivalTolerance = 0f;
            if (_astarAI is AIBase aiBase)
            {
                arrivalTolerance = Mathf.Max(0f, aiBase.endReachedDistance);
            }

            // 终点两侧各留出到达容差，确保 AI 提前停下时仍不会进入对方半径。
            return NoOverlapDistance + arrivalTolerance * 2f;
        }

        /// <summary>
        /// 判断自身与追踪目标的实际中心距是否已进入安全停止范围。
        /// </summary>
        private bool IsTargetWithinSafeDistance()
        {
            if (TargetChess == null || TargetChess.RuntimeData == null)
            {
                return false;
            }

            return Vector2.Distance(_chessController.RuntimeData.CurrentPosition,
                       TargetChess.RuntimeData.CurrentPosition) <= GetSafeTargetDistance();
        }

        /// <summary>
        /// 检查 A* 组件、运行时数据和 RVO 配置是否可用于移动。
        /// </summary>
        private bool CanMove()
        {
            if (_chessController.RuntimeData == null || !_movementAvailable
                || !_chessBoard.ContainsPosition(_chessController.RuntimeData.CurrentPosition))
            {
                return false;
            }

            if (_astarAI is Component aiComponent && aiComponent == null)
            {
                return false;
            }

            if (_astarAI is Component && (_rvoController == null || !_rvoController.enabled))
            {
                ReportMissingRvo();
                return false;
            }

            return true;
        }

        /// <summary>
        /// 清除当前命令，但不改变 A* 的停止状态。
        /// </summary>
        private void ResetCommand()
        {
            _movementMode = MovementMode.None;
            _destination = Vector2.zero;
            _hasDestination = false;
            TargetChess = null;
        }

        /// <summary>
        /// 报告真实 Unity 棋子缺少 RVO 组件，并阻止其移动。
        /// </summary>
        private void ReportMissingRvo()
        {
            _movementAvailable = false;
            if (_reportedMissingRvo)
            {
                return;
            }

            _reportedMissingRvo = true;
            Debug.LogError("ChessMovementController 要求棋子 GameObject 配置 RVOController 和场景 RVOSimulator。");
        }
    }
}
