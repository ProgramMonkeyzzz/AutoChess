using System;
using UnityEngine;

namespace AutoChess.Render
{
    /// <summary>
    /// 单个存活棋子在某一战斗步的纯数据渲染快照。
    /// </summary>
    public readonly struct ChessRenderSnapshot
    {
        /// <summary>棋子实例唯一 ID。</summary>
        public readonly int InstanceId;

        /// <summary>棋子所属阵营 ID。</summary>
        public readonly int CampId;

        /// <summary>棋盘 XZ 坐标。</summary>
        public readonly Vector2 Position;

        /// <summary>棋盘 XZ 朝向；零向量表示保持原朝向。</summary>
        public readonly Vector2 Forward;

        /// <summary>创建棋子渲染快照。</summary>
        public ChessRenderSnapshot(int instanceId, int campId, Vector2 position, Vector2 forward)
        {
            InstanceId = instanceId;
            CampId = campId;
            Position = position;
            Forward = forward;
        }
    }

    /// <summary>
    /// 单枚活动子弹在某一战斗步的纯数据渲染快照。
    /// </summary>
    public readonly struct BulletRenderSnapshot
    {
        /// <summary>子弹实例唯一 ID。</summary>
        public readonly int BulletId;

        /// <summary>棋盘 XZ 坐标。</summary>
        public readonly Vector2 Position;

        /// <summary>棋盘 XZ 朝向；零向量表示保持原朝向。</summary>
        public readonly Vector2 Forward;

        /// <summary>创建子弹渲染快照。</summary>
        public BulletRenderSnapshot(int bulletId, Vector2 position, Vector2 forward)
        {
            BulletId = bulletId;
            Position = position;
            Forward = forward;
        }
    }

    /// <summary>
    /// 一个战斗固定步结束后发布给主线程的完整渲染帧。
    /// </summary>
    public sealed class BattleRenderFrame
    {
        /// <summary>对应的战斗固定步编号。</summary>
        public readonly long StepId;

        /// <summary>本帧仍需显示的存活棋子。</summary>
        public readonly ChessRenderSnapshot[] Chess;

        /// <summary>本帧仍在飞行的活动子弹。</summary>
        public readonly BulletRenderSnapshot[] Bullets;

        /// <summary>创建一份不可变的渲染帧；空数组表示该类型视图应全部清除。</summary>
        public BattleRenderFrame(long stepId, ChessRenderSnapshot[] chess, BulletRenderSnapshot[] bullets)
        {
            StepId = stepId;
            Chess = chess ?? Array.Empty<ChessRenderSnapshot>();
            Bullets = bullets ?? Array.Empty<BulletRenderSnapshot>();
        }
    }
}