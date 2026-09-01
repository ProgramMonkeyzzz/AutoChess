using System;
using UnityEngine;

namespace AutoChess.Navigation
{
    /// <summary>
    /// 导航代理的稳定句柄，只包含进程内单调递增的代理 ID。
    /// </summary>
    public readonly struct NavigationAgentHandle : IEquatable<NavigationAgentHandle>
    {
        /// <summary>无效代理句柄。</summary>
        public static readonly NavigationAgentHandle Invalid = new NavigationAgentHandle(0);

        /// <summary>代理 ID。</summary>
        public int AgentId { get; }

        /// <summary>创建指定 ID 的代理句柄。</summary>
        public NavigationAgentHandle(int agentId)
        {
            AgentId = agentId;
        }

        /// <summary>判断句柄是否有效。</summary>
        public bool IsValid => AgentId > 0;

        /// <summary>判断两个代理句柄是否相等。</summary>
        public bool Equals(NavigationAgentHandle other) => AgentId == other.AgentId;

        /// <summary>判断对象是否为相同代理句柄。</summary>
        public override bool Equals(object obj) => obj is NavigationAgentHandle other && Equals(other);

        /// <summary>返回代理句柄哈希值。</summary>
        public override int GetHashCode() => AgentId;

        /// <summary>比较两个代理句柄。</summary>
        public static bool operator ==(NavigationAgentHandle left, NavigationAgentHandle right) => left.Equals(right);

        /// <summary>比较两个代理句柄是否不同。</summary>
        public static bool operator !=(NavigationAgentHandle left, NavigationAgentHandle right) => !left.Equals(right);
    }

    /// <summary>
    /// RVO 代理的静态配置，半径和时间视野使用项目约定默认值。
    /// </summary>
    public readonly struct NavigationAgentConfig
    {
        /// <summary>代理半径，默认 0.5。</summary>
        public readonly float Radius;
        /// <summary>代理最大速度。</summary>
        public readonly float MaxSpeed;
        /// <summary>代理时间视野，默认 2 秒。</summary>
        public readonly float AgentTimeHorizon;
        /// <summary>障碍时间视野，默认 0.5 秒。</summary>
        public readonly float ObstacleTimeHorizon;
        /// <summary>最大邻居数，默认 10。</summary>
        public readonly int MaxNeighbours;
        /// <summary>避让优先级，默认 0.5。</summary>
        public readonly float Priority;

        /// <summary>创建代理配置。</summary>
        public NavigationAgentConfig(
            float radius,
            float maxSpeed,
            float agentTimeHorizon,
            float obstacleTimeHorizon,
            int maxNeighbours,
            float priority)
        {
            Radius = radius;
            MaxSpeed = maxSpeed;
            AgentTimeHorizon = agentTimeHorizon;
            ObstacleTimeHorizon = obstacleTimeHorizon;
            MaxNeighbours = maxNeighbours;
            Priority = priority;
        }

        /// <summary>使用项目默认半径和时间视野创建配置。</summary>
        public static NavigationAgentConfig Create(float maxSpeed)
        {
            return new NavigationAgentConfig(0.5f, maxSpeed, 2f, 0.5f, 10, 0.5f);
        }
    }

    /// <summary>
    /// BattleManager 在线程内生成的单代理导航输入。
    /// </summary>
    public readonly struct NavigationInputSnapshot
    {
        /// <summary>固定步编号。</summary>
        public readonly long StepId;

        /// <summary>代理句柄。</summary>
        public readonly NavigationAgentHandle Agent;

        /// <summary>当前 XZ 位置。</summary>
        public readonly Vector2 Position;

        /// <summary>期望移动目标点。</summary>
        public readonly Vector2 TargetPoint;

        /// <summary>期望移动速度。</summary>
        public readonly float DesiredSpeed;

        /// <summary>代理最大速度。</summary>
        public readonly float MaxSpeed;

        /// <summary>当前是否允许代理移动。</summary>
        public readonly bool Enabled;

        /// <summary>移动命令版本。</summary>
        public readonly int CommandVersion;

        /// <summary>是否为当前固定步输入批次的最后一条记录。</summary>
        public readonly bool IsStepComplete;

        /// <summary>创建导航输入快照。</summary>
        public NavigationInputSnapshot(
            long stepId,
            NavigationAgentHandle agent,
            Vector2 position,
            Vector2 targetPoint,
            float desiredSpeed,
            float maxSpeed,
            bool enabled,
            int commandVersion,
            bool isStepComplete = false)
        {
            StepId = stepId;
            Agent = agent;
            Position = position;
            TargetPoint = targetPoint;
            DesiredSpeed = desiredSpeed;
            MaxSpeed = maxSpeed;
            Enabled = enabled;
            CommandVersion = commandVersion;
            IsStepComplete = isStepComplete;
        }
    }

    /// <summary>
    /// NavigationManager 发布给 BattleManager 的单代理导航结果。
    /// </summary>
    public readonly struct NavigationResultSnapshot
    {
        /// <summary>固定步编号。</summary>
        public readonly long StepId;

        /// <summary>代理句柄。</summary>
        public readonly NavigationAgentHandle Agent;

        /// <summary>RVO 计算后的新 XZ 位置。</summary>
        public readonly Vector2 Position;

        /// <summary>RVO 计算出的速度。</summary>
        public readonly float CalculatedSpeed;

        /// <summary>是否已经到达期望终点。</summary>
        public readonly bool ReachedDestination;

        /// <summary>是否被其他代理阻挡或产生避让。</summary>
        public readonly bool AvoidingAgents;

        /// <summary>移动命令版本。</summary>
        public readonly int CommandVersion;

        /// <summary>创建导航结果快照。</summary>
        public NavigationResultSnapshot(
            long stepId,
            NavigationAgentHandle agent,
            Vector2 position,
            float calculatedSpeed,
            bool reachedDestination,
            bool avoidingAgents,
            int commandVersion)
        {
            StepId = stepId;
            Agent = agent;
            Position = position;
            CalculatedSpeed = calculatedSpeed;
            ReachedDestination = reachedDestination;
            AvoidingAgents = avoidingAgents;
            CommandVersion = commandVersion;
        }
    }
}
