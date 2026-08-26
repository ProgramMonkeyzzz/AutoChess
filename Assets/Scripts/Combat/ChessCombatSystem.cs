using Config;
using Pathfinding;
using Pathfinding.ECS;
using Pathfinding.ECS.RVO;
using Pathfinding.PID;
using Pathfinding.RVO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace AutoChess.Combat
{
    internal struct ChessDamageEvent
    {
        public Entity Target;
        public int Amount;
    }

    /// <summary>
    /// Creates the two logic-only combat agents from the Test0 and Test1 data assets.
    /// The agents are A* FollowerEntity ECS agents with RVO enabled; no render entity is attached.
    /// A scene can opt out by adding its own ChessUnit entities before this system runs.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ChessBattleBootstrapSystem : SystemBase
    {
        private bool initialized;

        protected override void OnCreate()
        {
            initialized = false;
        }

        protected override void OnUpdate()
        {
            if (initialized || !Application.isPlaying)
            {
                return;
            }

            var test0 = Resources.Load<ChessBaseData>("Configs/ChessBaseData/Test0");
            var test1 = Resources.Load<ChessBaseData>("Configs/ChessBaseData/Test1");
            if (test0 == null || test1 == null)
            {
                return;
            }

            if (SystemAPI.QueryBuilder().WithAll<ChessUnit>().Build().CalculateEntityCount() > 0)
            {
                initialized = true;
                Enabled = false;
                return;
            }

            EnsurePathfindingServices();
            var test0Entity = CreateUnit(test0, 0, new float3(-4f, 0f, 0f));
            var test1Entity = CreateUnit(test1, 1, new float3(4f, 0f, 0f));

            EntityManager.SetComponentData(test0Entity, new ChessUnit
            {
                Target = test1Entity,
                Id = test0.Id,
                Team = 0,
                Hp = math.max(0, test0.Hp),
                MaxHp = math.max(0, test0.Hp),
                Attack = math.max(0, test0.Atk),
                AttackRange = math.max(0f, test0.Range),
                MoveSpeed = math.max(0f, test0.MoveSpeed),
                AttackInterval = math.max(0.0001f, test0.AtkSpeed),
                Alive = (byte)(test0.Hp > 0 ? 1 : 0),
            });
            EntityManager.SetComponentData(test1Entity, new ChessUnit
            {
                Target = test0Entity,
                Id = test1.Id,
                Team = 1,
                Hp = math.max(0, test1.Hp),
                MaxHp = math.max(0, test1.Hp),
                Attack = math.max(0, test1.Atk),
                AttackRange = math.max(0f, test1.Range),
                MoveSpeed = math.max(0f, test1.MoveSpeed),
                AttackInterval = math.max(0.0001f, test1.AtkSpeed),
                Alive = (byte)(test1.Hp > 0 ? 1 : 0),
            });

            initialized = true;
            Enabled = false;
        }

        private static void EnsurePathfindingServices()
        {
            if (AstarPath.active == null)
            {
                var pathfindingObject = new GameObject("A* Pathfinding (Logic)");
                var astar = pathfindingObject.AddComponent<AstarPath>();
                astar.scanOnStartup = false;
            }

            var activeAstar = AstarPath.active;
            if (activeAstar.data.graphs == null || activeAstar.data.graphs.Length == 0)
            {
                var graph = activeAstar.data.AddGraph<GridGraph>();
                graph.center = Vector3.zero;
                graph.SetDimensions(64, 64, 0.5f);
                graph.collision.collisionCheck = false;
                graph.collision.heightCheck = false;
                activeAstar.Scan(graph);
            }

            if (RVOSimulator.active == null)
            {
                var rvoObject = new GameObject("RVO Simulator (Logic)");
                var simulator = rvoObject.AddComponent<RVOSimulator>();
                simulator.movementPlane = MovementPlane.XZ;
            }
        }

        private Entity CreateUnit(ChessBaseData data, int team, float3 position)
        {
            var shape = new AgentCylinderShape
            {
                radius = ChessBaseData.Radius,
                height = 2f,
            };
            var movement = new MovementSettings
            {
                follower = new PIDMovement
                {
                    speed = math.max(0f, data.MoveSpeed),
                    rotationSpeed = 600f,
                    maxRotationSpeed = 720f,
                    maxOnSpotRotationSpeed = 720f,
                    slowdownTime = 0.2f,
                    slowdownTimeWhenTurningOnSpot = 0.05f,
                    desiredWallDistance = 0f,
                    leadInRadiusWhenApproachingDestination = 0f,
                    allowRotatingOnSpot = true,
                },
                stopDistance = math.max(0f, data.Range),
                rotationSmoothing = 0f,
                groundMask = -1,
                isStopped = false,
                debugFlags = PIDMovement.DebugFlags.Nothing,
            };
            var autoRepath = Pathfinding.ECS.AutoRepathPolicy.Default;
            autoRepath.mode = Pathfinding.AutoRepathPolicy.Mode.Dynamic;
            autoRepath.period = 0.25f;

            var pathSettings = PathRequestSettings.Default;
            var managedState = new ManagedState();
            var managedSettings = new ManagedSettings
            {
                pathfindingSettings = pathSettings,
            };
            var rvoSettings = RVOAgent.Default;

            var entity = FollowerEntity.CreateEntity(
                position,
                quaternion.identity,
                1f,
                ref shape,
                ref movement,
                ref autoRepath,
                managedState,
                OrientationMode.ZAxisForward,
                MovementPlaneSource.Graph,
                false,
                false,
                false,
                true,
                rvoSettings,
                managedSettings,
                Physics.defaultPhysicsScene);

            EntityManager.AddComponentData(entity, new ChessUnit
            {
                Id = data.Id,
                Team = team,
                Hp = math.max(0, data.Hp),
                MaxHp = math.max(0, data.Hp),
                Attack = math.max(0, data.Atk),
                AttackRange = math.max(0f, data.Range),
                MoveSpeed = math.max(0f, data.MoveSpeed),
                AttackInterval = math.max(0.0001f, data.AtkSpeed),
                Target = Entity.Null,
                Alive = (byte)(data.Hp > 0 ? 1 : 0),
            });
            EntityManager.AddComponentData(entity, new ChessCombatState
            {
                AttackTimer = 0f,
                Phase = ChessCombatPhase.SeekingTarget,
            });
            return entity;
        }
    }

    /// <summary>
    /// Runs after A* movement. It supplies the next destination before the following
    /// movement tick and resolves attacks when the RVO agent reaches attack range.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Pathfinding.ECS.AIMovementSystemGroup))]
    public partial class ChessCombatSystem : SystemBase
    {
        private EntityQuery unitQuery;

        protected override void OnCreate()
        {
            unitQuery = GetEntityQuery(ComponentType.ReadWrite<ChessUnit>(), ComponentType.ReadWrite<ChessCombatState>());
            RequireForUpdate(unitQuery);
        }

        protected override void OnUpdate()
        {
            var entities = unitQuery.ToEntityArray(Allocator.Temp);
            var damageEvents = new NativeList<ChessDamageEvent>(entities.Length, Allocator.Temp);
            var deltaTime = SystemAPI.Time.DeltaTime;

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var unit = EntityManager.GetComponentData<ChessUnit>(entity);
                var combat = EntityManager.GetComponentData<ChessCombatState>(entity);

                if (unit.Alive == 0)
                {
                    combat.Phase = ChessCombatPhase.Dead;
                    StopUnit(entity);
                    EntityManager.SetComponentData(entity, combat);
                    continue;
                }

                if (!IsValidEnemy(unit.Target, unit.Team))
                {
                    unit.Target = FindEnemy(entity, unit.Team, entities);
                }

                if (unit.Target == Entity.Null || !EntityManager.HasComponent<LocalTransform>(unit.Target))
                {
                    combat.Phase = ChessCombatPhase.SeekingTarget;
                    combat.AttackTimer = 0f;
                    StopUnit(entity);
                    EntityManager.SetComponentData(entity, unit);
                    EntityManager.SetComponentData(entity, combat);
                    continue;
                }

                var position = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var targetPosition = EntityManager.GetComponentData<LocalTransform>(unit.Target).Position;
                var inRange = ChessCombatMath.IsInAttackRange(position, targetPosition, unit.AttackRange);
                var enteringRange = inRange && combat.Phase != ChessCombatPhase.Attacking;
                SetMovementGoal(entity, targetPosition, unit.AttackRange, unit.MoveSpeed, inRange);

                combat.Phase = inRange ? ChessCombatPhase.Attacking : ChessCombatPhase.MovingToAttackRange;

                if (inRange)
                {
                    var interval = math.max(0.0001f, unit.AttackInterval);
                    combat.AttackTimer = enteringRange
                        ? interval
                        : ChessCombatMath.AdvanceAttackTimer(combat.AttackTimer, deltaTime);
                    while (combat.AttackTimer >= interval)
                    {
                        combat.AttackTimer -= interval;
                        damageEvents.Add(new ChessDamageEvent
                        {
                            Target = unit.Target,
                            Amount = unit.Attack,
                        });
                    }
                }
                else
                {
                    combat.AttackTimer = 0f;
                }

                EntityManager.SetComponentData(entity, unit);
                EntityManager.SetComponentData(entity, combat);
            }

            for (var i = 0; i < damageEvents.Length; i++)
            {
                ApplyDamage(damageEvents[i]);
            }

            damageEvents.Dispose();
            entities.Dispose();
        }

        private bool IsValidEnemy(Entity target, int team)
        {
            return target != Entity.Null &&
                   EntityManager.Exists(target) &&
                   EntityManager.HasComponent<ChessUnit>(target) &&
                   EntityManager.GetComponentData<ChessUnit>(target).Alive != 0 &&
                   EntityManager.GetComponentData<ChessUnit>(target).Team != team;
        }

        private Entity FindEnemy(Entity self, int team, NativeArray<Entity> entities)
        {
            var selfPosition = EntityManager.GetComponentData<LocalTransform>(self).Position;
            var closest = Entity.Null;
            var closestDistance = float.PositiveInfinity;

            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate == self || !EntityManager.HasComponent<LocalTransform>(candidate))
                {
                    continue;
                }

                var candidateData = EntityManager.GetComponentData<ChessUnit>(candidate);
                if (candidateData.Alive == 0 || candidateData.Team == team)
                {
                    continue;
                }

                var distance = math.lengthsq(EntityManager.GetComponentData<LocalTransform>(candidate).Position - selfPosition);
                if (distance < closestDistance || (math.abs(distance - closestDistance) < 0.0001f && candidate.Index < closest.Index))
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void SetMovementGoal(Entity entity, float3 targetPosition, float attackRange, float moveSpeed, bool inRange)
        {
            if (EntityManager.HasComponent<DestinationPoint>(entity))
            {
                EntityManager.SetComponentData(entity, new DestinationPoint
                {
                    destination = targetPosition,
                    facingDirection = targetPosition - EntityManager.GetComponentData<LocalTransform>(entity).Position,
                });
            }

            if (EntityManager.HasComponent<MovementSettings>(entity))
            {
                var movement = EntityManager.GetComponentData<MovementSettings>(entity);
                movement.stopDistance = math.max(0f, attackRange);
                movement.follower.speed = math.max(0f, moveSpeed);
                movement.isStopped = inRange;
                EntityManager.SetComponentData(entity, movement);
            }
        }

        private void StopUnit(Entity entity)
        {
            if (!EntityManager.HasComponent<MovementSettings>(entity))
            {
                return;
            }

            var movement = EntityManager.GetComponentData<MovementSettings>(entity);
            movement.isStopped = true;
            EntityManager.SetComponentData(entity, movement);
        }

        private void ApplyDamage(ChessDamageEvent damageEvent)
        {
            if (!EntityManager.Exists(damageEvent.Target) || !EntityManager.HasComponent<ChessUnit>(damageEvent.Target))
            {
                return;
            }

            var target = EntityManager.GetComponentData<ChessUnit>(damageEvent.Target);
            if (target.Alive == 0)
            {
                return;
            }

            target.Hp -= math.max(0, damageEvent.Amount);
            if (target.Hp <= 0)
            {
                target.Hp = 0;
                target.Alive = 0;
                target.Target = Entity.Null;
                if (EntityManager.HasComponent<ChessCombatState>(damageEvent.Target))
                {
                    var state = EntityManager.GetComponentData<ChessCombatState>(damageEvent.Target);
                    state.Phase = ChessCombatPhase.Dead;
                    state.AttackTimer = 0f;
                    EntityManager.SetComponentData(damageEvent.Target, state);
                }

                StopUnit(damageEvent.Target);
            }

            EntityManager.SetComponentData(damageEvent.Target, target);
        }
    }
}
