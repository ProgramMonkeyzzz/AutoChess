using System;
using System.Collections.Generic;
using AutoChess.Navigation;
using Chess;
using Pathfinding.RVO;
using Unity.Collections;
using UnityEngine;

namespace AutoChess
{
    /// <summary>
    /// 在 Unity 主线程上管理 SimulatorBurst，并为固定无障碍棋盘执行局部避让。
    /// </summary>
    public sealed class NavigationManager : IDisposable
    {
        private sealed class AgentState
        {
            public NavigationAgentHandle Handle;
            public IAgent Agent;
            public float Radius;
            public bool Registered;
        }

        private readonly ChessBoard _board;
        private readonly SimulatorBurst _simulator;
        private readonly Dictionary<int, AgentState> _agents = new Dictionary<int, AgentState>();
        private int _nextAgentId = 1;
        private bool _disposed;
        private int _mainThreadId;
        private readonly Dictionary<int, Vector2> _positions = new Dictionary<int, Vector2>();

        /// <summary>创建主线程导航管理器并初始化 XZ 平面 RVO 模拟器。</summary>
        public NavigationManager(ChessBoard board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            _simulator = new SimulatorBurst(MovementPlane.XZ)
            {
                UseNavmeshAsObstacle = false,
                HardCollisions = true,
                SymmetryBreakingBias = 0.1f
            };
        }

        /// <summary>当前是否已经释放。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>在主线程注册一个新的 RVO 代理。</summary>
        public NavigationAgentHandle RegisterAgent(Vector2 position, float radius, float maxSpeed)
        {
            return RegisterAgent(position, new NavigationAgentConfig(radius, maxSpeed, 2f, 0.5f, 10, 0.5f));
        }

        /// <summary>使用完整配置在主线程注册一个新的 RVO 代理。</summary>
        public NavigationAgentHandle RegisterAgent(Vector2 position, NavigationAgentConfig config)
        {
            EnsureMainThread();
            EnsureNotDisposed();
            if (!IsFinite(position) || config.Radius <= 0f || !IsFinite(config.MaxSpeed))
            {
                return NavigationAgentHandle.Invalid;
            }

            NavigationAgentHandle handle = new NavigationAgentHandle(_nextAgentId++);
            Vector2 clampedPosition = ClampPosition(position, config.Radius);
            IAgent agent = _simulator.AddAgent(ToWorld(clampedPosition));
            agent.Radius = config.Radius;
            agent.Height = 2f;
            agent.MaxNeighbours = config.MaxNeighbours;
            agent.AgentTimeHorizon = config.AgentTimeHorizon;
            agent.ObstacleTimeHorizon = config.ObstacleTimeHorizon;
            agent.Priority = config.Priority;
            agent.CollidesWith = (RVOLayer)(-1);
            agent.Position = ToWorld(clampedPosition);
            agent.SetTarget(ToWorld(clampedPosition), 0f, Math.Max(0f, config.MaxSpeed), ToWorld(clampedPosition));
            _agents.Add(handle.AgentId, new AgentState
            {
                Handle = handle,
                Agent = agent,
                Radius = config.Radius,
                Registered = true
            });
            _positions.Add(handle.AgentId, clampedPosition);
            return handle;
        }

        /// <summary>在主线程注销一个 RVO 代理。</summary>
        public bool UnregisterAgent(NavigationAgentHandle handle)
        {
            EnsureMainThread();
            EnsureNotDisposed();
            if (!_agents.TryGetValue(handle.AgentId, out AgentState state))
            {
                return false;
            }

            _simulator.RemoveAgent(state.Agent);
            state.Registered = false;
            _agents.Remove(handle.AgentId);
            _positions.Remove(handle.AgentId);
            return true;
        }

        /// <summary>
        /// 在主线程推进一个固定导航步，并返回所有有效代理的导航结果。
        /// </summary>
        public IReadOnlyList<NavigationResultSnapshot> Tick(
            long stepId,
            IReadOnlyList<NavigationInputSnapshot> inputs,
            float fixedStep)
        {
            EnsureMainThread();
            EnsureNotDisposed();
            if (fixedStep <= 0f || !IsFinite(fixedStep))
            {
                throw new ArgumentOutOfRangeException(nameof(fixedStep));
            }

            Dictionary<int, NavigationInputSnapshot> inputByAgent = new Dictionary<int, NavigationInputSnapshot>();
            if (inputs != null)
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    NavigationInputSnapshot input = inputs[i];
                    if (input.StepId == stepId && input.Agent.IsValid)
                    {
                        inputByAgent[input.Agent.AgentId] = input;
                    }
                }
            }

            foreach (AgentState state in _agents.Values)
            {
                if (!inputByAgent.TryGetValue(state.Handle.AgentId, out NavigationInputSnapshot input))
                {
                    input = new NavigationInputSnapshot(
                        stepId,
                        state.Handle,
                        FromWorld(state.Agent.Position),
                        FromWorld(state.Agent.Position),
                        0f,
                        0f,
                        false,
                        0);
                }

                Vector2 currentPosition = _positions.TryGetValue(state.Handle.AgentId, out Vector2 previousPosition)
                    ? previousPosition
                    : input.Position;
                Vector2 safePosition = ClampPosition(currentPosition, state.Radius);
                Vector2 safeTarget = ClampPosition(input.TargetPoint, state.Radius);
                float maxSpeed = Math.Max(0f, SanitizeFinite(input.MaxSpeed));
                float desiredSpeed = Math.Max(0f, Math.Min(maxSpeed, SanitizeFinite(input.DesiredSpeed)));
                state.Agent.Position = ToWorld(safePosition);
                state.Agent.Locked = !input.Enabled || desiredSpeed <= 0f;
                state.Agent.SetTarget(
                    ToWorld(safeTarget),
                    input.Enabled ? desiredSpeed : 0f,
                    maxSpeed,
                    ToWorld(safeTarget));
            }

            _simulator.Update(default, fixedStep, false, Allocator.TempJob).Complete();

            List<NavigationResultSnapshot> results = new List<NavigationResultSnapshot>(_agents.Count);
            foreach (AgentState state in _agents.Values)
            {
                if (!inputByAgent.TryGetValue(state.Handle.AgentId, out NavigationInputSnapshot input))
                {
                    continue;
                }

                Vector2 position = FromWorld(state.Agent.Position);
                Vector2 target = ClampPosition(input.TargetPoint, state.Radius);
                Vector2 steeringTarget = FromWorld(state.Agent.CalculatedTargetPoint);
                if (!IsFinite(steeringTarget))
                {
                    steeringTarget = target;
                }

                float speed = Math.Max(0f, SanitizeFinite(state.Agent.CalculatedSpeed));
                Vector2 delta = steeringTarget - position;
                float maxDistance = speed * fixedStep;
                if (delta.sqrMagnitude > maxDistance * maxDistance && maxDistance > 0f)
                {
                    position += delta.normalized * maxDistance;
                }

                position = ClampPosition(position, state.Radius);
                _positions[state.Handle.AgentId] = position;
                state.Agent.Position = ToWorld(position);
                results.Add(new NavigationResultSnapshot(
                    stepId,
                    state.Handle,
                    position,
                    speed,
                    state.Agent.CalculatedEffectivelyReachedDestination == ReachedEndOfPath.Reached
                        || (target - position).sqrMagnitude <= 0.0001f,
                    state.Agent.AvoidingAnyAgents,
                    input.CommandVersion));
            }

            return results;
        }

        /// <summary>释放所有 RVO Native 容器，必须在主线程调用。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            EnsureMainThread();
            _simulator.OnDestroy();
            _agents.Clear();
            _positions.Clear();
            _disposed = true;
        }

        /// <summary>确保当前调用来自创建管理器的 Unity 主线程。</summary>
        private void EnsureMainThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                throw new InvalidOperationException("NavigationManager 只能在创建它的 Unity 主线程调用。");
            }
        }

        /// <summary>确保导航管理器尚未释放。</summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NavigationManager));
            }
        }

        /// <summary>将 XZ 平面坐标转换为 RVO 使用的三维坐标。</summary>
        private static Vector3 ToWorld(Vector2 position) => new Vector3(position.x, 0f, position.y);

        /// <summary>将 RVO 三维坐标投影回棋盘 XZ 平面。</summary>
        private static Vector2 FromWorld(Vector3 position) => new Vector2(position.x, position.z);

        /// <summary>将代理中心限制在考虑自身半径后的棋盘范围内。</summary>
        private Vector2 ClampPosition(Vector2 position, float radius)
        {
            float min = Math.Max(0f, radius);
            float maxX = Math.Max(min, _board.Width - radius);
            float maxY = Math.Max(min, _board.Height - radius);
            return new Vector2(
                Math.Clamp(SanitizeFinite(position.x), min, maxX),
                Math.Clamp(SanitizeFinite(position.y), min, maxY));
        }

        /// <summary>判断二维坐标是否为有限值。</summary>
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        /// <summary>判断浮点数是否为有限值。</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>将无效浮点输入归一为零。</summary>
        private static float SanitizeFinite(float value) => IsFinite(value) ? value : 0f;
    }
}
