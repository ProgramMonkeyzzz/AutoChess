#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AutoChess;
using AutoChess.RayDamage;
using AutoChess.Utils;
using Camp;
using Chess;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证圆形射线飞行物的注册快照、扫掠命中、命中上限和 BattleManager 驱动。</summary>
public class RayDamageManagerTests
{
    private CampManager _camps;
    private ChessController _ally;
    private ChessController _enemy;

    [SetUp]
    public void SetUp()
    {
        _camps = CreateCampManager(1);
        _ally = _camps.RegisterChess(1, 1, Vector2.zero);
        _enemy = _camps.RegisterChess(2, 1, new Vector2(1f, 0f));
    }

    [TearDown]
    public void TearDown()
    {
        while (ThreadLogQueue.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// 验证注册会写入路径快照、归一化方向和伤害，且实例来自对象池。
    /// </summary>
    [Test]
    public void RegisterStoresSnapshotAndNormalizesDirection()
    {
        using (var manager = new RayDamageManager(_camps))
        {
            var camps = new HashSet<int> { 2 };
            RayDamageRuntimeData instance = manager.Register(
                new Vector2(3f, 4f),
                new Vector2(3f, 4f),
                0.5f,
                10f,
                8f,
                camps,
                12,
                3);

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.IsFromPool, Is.True);
            Assert.That(instance.RayDamageId, Is.GreaterThan(0));
            Assert.That(instance.Origin, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(instance.CurrentPosition, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(instance.Direction.x, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(instance.Direction.y, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(instance.Radius, Is.EqualTo(0.5f));
            Assert.That(instance.Speed, Is.EqualTo(10f));
            Assert.That(instance.MaxDistance, Is.EqualTo(8f));
            Assert.That(instance.Damage, Is.EqualTo(12));
            Assert.That(instance.MaxAffectedCount, Is.EqualTo(3));
            Assert.That(instance.TraveledDistance, Is.EqualTo(0f));
            Assert.That(instance.AffectedCamps, Does.Contain(2));
            Assert.That(instance.HitUniqueIds, Is.Empty);
            Assert.That(manager.ActiveCount, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// 验证飞行物按速度沿方向移动，且不会超过最远距离。
    /// </summary>
    [Test]
    public void TickMovesAlongDirectionWithoutExceedingMaxDistance()
    {
        _ally.RuntimeData.CurrentPosition = new Vector2(0f, 10f);
        _enemy.RuntimeData.CurrentPosition = new Vector2(0f, 11f);
        using (var manager = new RayDamageManager(_camps))
        {
            RayDamageRuntimeData instance = manager.Register(
                Vector2.zero,
                Vector2.right,
                0.25f,
                10f,
                5f,
                new[] { 2 },
                1,
                8);

            manager.Tick(0.2f);
            Assert.That(instance.CurrentPosition, Is.EqualTo(new Vector2(2f, 0f)));
            Assert.That(instance.TraveledDistance, Is.EqualTo(2f));
            Assert.That(manager.ActiveCount, Is.EqualTo(1));

            manager.Tick(1f);
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
            Assert.That(instance.CurrentPosition, Is.EqualTo(new Vector2(5f, 0f)));
            Assert.That(instance.TraveledDistance, Is.EqualTo(5f));
        }
    }

    /// <summary>
    /// 验证高速扫掠仍能命中路径上的棋子，且同一棋子只扣一次伤害。
    /// </summary>
    [Test]
    public void TickDamagesTouchedChessOnceEvenWhenSweepingPast()
    {
        using (var manager = new RayDamageManager(_camps))
        {
            manager.Register(
                Vector2.zero,
                Vector2.right,
                0.25f,
                100f,
                20f,
                new[] { 2 },
                7,
                8);

            manager.Tick(0.1f);
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(43));
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(manager.ActiveCount, Is.EqualTo(1));

            manager.Tick(0.1f);
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(43));
        }
    }

    /// <summary>
    /// 验证侧向错过、其他阵营和尸体不扣血，边界距离命中。
    /// </summary>
    [Test]
    public void TickSkipsOutOfRangeUnaffectedAndDeadChess()
    {
        ChessController outside = _camps.RegisterChess(2, 1, new Vector2(1f, 2f));
        ChessController onBoundary = _camps.RegisterChess(2, 1, new Vector2(1f, 0.5f));
        ChessController corpse = _camps.RegisterChess(2, 1, new Vector2(0.5f, 0f));
        corpse.DamageReceiver.ApplyDamage(50);
        corpse.RuntimeData.Hp = 50;

        using (var manager = new RayDamageManager(_camps))
        {
            manager.Register(
                Vector2.zero,
                Vector2.right,
                0.5f,
                10f,
                10f,
                new[] { 2 },
                9,
                8);
            manager.Tick(0.2f);

            Assert.That(corpse.Fsm.CurrentState, Is.TypeOf<DeadState>());
            Assert.That(corpse.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(outside.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(onBoundary.RuntimeData.Hp, Is.EqualTo(41));
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(41));
        }
    }

    /// <summary>
    /// 验证同一步内按沿射线远近命中，达到上限后更远的单位不扣血且飞行物回收。
    /// </summary>
    [Test]
    public void TickHitsCloserUnitsFirstUntilCap()
    {
        ChessController mid = _camps.RegisterChess(2, 1, new Vector2(2f, 0f));
        ChessController far = _camps.RegisterChess(2, 1, new Vector2(3f, 0f));

        using (var manager = new RayDamageManager(_camps))
        {
            manager.Register(
                Vector2.zero,
                Vector2.right,
                0.25f,
                100f,
                10f,
                new[] { 2 },
                5,
                2);
            manager.Tick(0.1f);

            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(45));
            Assert.That(mid.RuntimeData.Hp, Is.EqualTo(45));
            Assert.That(far.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证飞完全程后回收，即使尚未达到命中上限。
    /// </summary>
    [Test]
    public void TickRecyclesWhenMaxDistanceReachedWithoutCap()
    {
        _ally.RuntimeData.CurrentPosition = new Vector2(0f, 10f);
        _enemy.RuntimeData.CurrentPosition = new Vector2(0f, 11f);
        using (var manager = new RayDamageManager(_camps))
        {
            RayDamageRuntimeData instance = manager.Register(
                Vector2.zero,
                Vector2.right,
                0.25f,
                10f,
                1f,
                new[] { 2 },
                4,
                8);

            manager.Tick(0.2f);
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
            Assert.That(instance.RayDamageId, Is.EqualTo(0));
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(50));
        }
    }

    /// <summary>
    /// 验证调用方事后修改阵营集合不影响已注册实例。
    /// </summary>
    [Test]
    public void RegisterCopiesAffectedCamps()
    {
        using (var manager = new RayDamageManager(_camps))
        {
            var camps = new HashSet<int> { 2 };
            manager.Register(
                Vector2.zero,
                Vector2.right,
                0.5f,
                10f,
                10f,
                camps,
                4,
                8);
            camps.Remove(2);
            camps.Add(1);

            manager.Tick(0.2f);

            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(46));
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
        }
    }

    /// <summary>
    /// 验证非法参数返回 null，零方向和零命中上限也拒绝注册。
    /// </summary>
    [Test]
    public void RegisterRejectsInvalidParameters()
    {
        using (var manager = new RayDamageManager(_camps))
        {
            int[] camps = { 1 };
            Assert.That(
                manager.Register(new Vector2(float.NaN, 0f), Vector2.right, 1f, 1f, 1f, camps, 1, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.zero, 1f, 1f, 1f, camps, 1, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.right, -1f, 1f, 1f, camps, 1, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.right, 1f, 0f, 1f, camps, 1, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.right, 1f, 1f, -1f, camps, 1, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.right, 1f, 1f, 1f, camps, -1, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.right, 1f, 1f, 1f, camps, 1, 0),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.right, 1f, 1f, 1f, null, 1, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, Vector2.right, 1f, 1f, 1f, Array.Empty<int>(), 1, 1),
                Is.Null);
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证释放管理器会回收飞行中的实例，且可重复释放。
    /// </summary>
    [Test]
    public void DisposeRecyclesActiveInstances()
    {
        var manager = new RayDamageManager(_camps);
        RayDamageRuntimeData instance = manager.Register(
            Vector2.zero,
            Vector2.right,
            0.5f,
            1f,
            10f,
            new[] { 1 },
            3,
            4);

        manager.Dispose();

        Assert.That(manager.ActiveCount, Is.EqualTo(0));
        Assert.That(instance.RayDamageId, Is.EqualTo(0));
        Assert.That(instance.HitUniqueIds, Is.Empty);
        Assert.DoesNotThrow(manager.Dispose);
        Assert.Throws<ObjectDisposedException>(
            () => manager.Register(Vector2.zero, Vector2.right, 0.5f, 1f, 10f, new[] { 1 }, 1, 1));
    }

    /// <summary>
    /// 验证 BattleManager 持有射线伤害管理器并用指定时间步推进命中。
    /// </summary>
    [Test]
    public void BattleManagerOwnsAndTicksRayDamageManager()
    {
        using (var battleManager = new BattleManager())
        {
            battleManager.Configs.Register(CreateConfig(1, 50));
            int allyId = battleManager.RegisterChess(1, 1, Vector2.zero);
            int enemyId = battleManager.RegisterChess(2, 1, new Vector2(1f, 0f));
            Assert.That(battleManager.TryGetChess(allyId, out ChessController ally), Is.True);
            Assert.That(battleManager.TryGetChess(enemyId, out ChessController enemy), Is.True);

            Assert.That(
                battleManager.RegisterRayDamage(
                    Vector2.zero,
                    Vector2.right,
                    0.5f,
                    10f,
                    10f,
                    new[] { 2 },
                    11,
                    4),
                Is.True);
            Assert.That(
                battleManager.RegisterRayDamage(
                    Vector2.zero,
                    Vector2.right,
                    0.5f,
                    10f,
                    10f,
                    Array.Empty<int>(),
                    11,
                    4),
                Is.False);

            battleManager.Tick(0.2f);

            Assert.That(enemy.RuntimeData.Hp, Is.EqualTo(39));
            Assert.That(ally.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(battleManager.RayDamageManager.ActiveCount, Is.EqualTo(1));
        }
    }

    /// <summary>创建已注册指定 ConfigId 的阵营管理器。</summary>
    private static CampManager CreateCampManager(params int[] configIds)
    {
        var configs = new ChessConfigRepository();
        for (int i = 0; i < configIds.Length; i++)
        {
            configs.Register(CreateConfig(configIds[i], 50));
        }

        return new CampManager(configs);
    }

    /// <summary>创建测试用棋子配置。</summary>
    private static ChessBaseData CreateConfig(int configId, int maxHp)
    {
        return new ChessBaseData
        {
            ConfigId = configId,
            Name = "Config" + configId,
            MaxHp = maxHp,
            Atk = 1,
            Range = 1f,
            MoveSpeed = 1f,
            AtkSpeed = 1f,
            WindUpTime = 0.1f,
            BulletSpeed = 0f
        };
    }
}
#endif
