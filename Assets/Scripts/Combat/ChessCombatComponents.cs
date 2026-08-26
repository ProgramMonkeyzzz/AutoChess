using Unity.Entities;
using Unity.Mathematics;

namespace AutoChess.Combat
{
    /// <summary>
    /// Runtime combat data copied from ChessBaseData when a unit is created.
    /// </summary>
    public struct ChessUnit : IComponentData
    {
        public int Id;
        public int Team;
        public int Hp;
        public int MaxHp;
        public int Attack;
        public float AttackRange;
        public float MoveSpeed;
        public float AttackInterval;
        public Entity Target;
        public byte Alive;
    }

    public enum ChessCombatPhase : byte
    {
        SeekingTarget = 0,
        MovingToAttackRange = 1,
        Attacking = 2,
        Dead = 3,
    }

    public struct ChessCombatState : IComponentData
    {
        public float AttackTimer;
        public ChessCombatPhase Phase;
    }

    /// <summary>Small unmanaged helpers used by the combat loop.</summary>
    public static class ChessCombatMath
    {
        public static bool IsInAttackRange(float3 position, float3 targetPosition, float range)
        {
            var safeRange = math.max(0f, range);
            return math.lengthsq(targetPosition - position) <= safeRange * safeRange;
        }

        public static float AdvanceAttackTimer(float timer, float deltaTime)
        {
            return math.max(0f, timer) + math.max(0f, deltaTime);
        }
    }
}
