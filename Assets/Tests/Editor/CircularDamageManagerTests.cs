#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AutoChess;
using AutoChess.CircularDamage;
using AutoChess.Utils;
using Camp;
using Chess;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证圆形范围伤害的注册快照、延迟结算、阵营过滤和 BattleManager 驱动。</summary>
public class CircularDamageManagerTests
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
    /// 验证注册会写入圆心、半径、延迟、阵营拷贝和伤害快照，且实例来自对象池。
    /// </summary>
    [Test]
    public void RegisterStoresSnapshotAndUsesPool()
    {
        using (var manager = new CircularDamageManager(_camps))
        {
            var camps = new HashSet<int> { 2 };
            CircularDamageRuntimeData instance = manager.Register(
                new Vector2(3f, 4f),
                2.5f,
                0.2f,
                camps,
                12);

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.IsFromPool, Is.True);
            Assert.That(instance.CircularDamageId, Is.GreaterThan(0));
            Assert.That(instance.Center, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(instance.Radius, Is.EqualTo(2.5f));
            Assert.That(instance.RemainingDelay, Is.EqualTo(0.2f));
            Assert.That(instance.Damage, Is.EqualTo(12));
            Assert.That(instance.AffectedCamps, Does.Contain(2));
            Assert.That(instance.AffectedCamps.Count, Is.EqualTo(1));
            Assert.That(manager.ActiveCount, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// 验证延迟未到时不扣血，到期后只对圆内受影响阵营结算注册时的伤害。
    /// </summary>
    [Test]
    public void TickAppliesLockedDamageWhenDelayElapses()
    {
        using (var manager = new CircularDamageManager(_camps))
        {
            manager.Register(Vector2.zero, 2f, 0.2f, new[] { 2 }, 7);

            manager.Tick(0.1f);
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(manager.ActiveCount, Is.EqualTo(1));

            manager.Tick(0.1f);
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(43));
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证圆外棋子、其他阵营和已死亡棋子都不会被扣血，边界距离命中。
    /// </summary>
    [Test]
    public void TickSkipsOutOfRangeUnaffectedAndDeadChess()
    {
        ChessController outside = _camps.RegisterChess(2, 1, new Vector2(2.1f, 0f));
        ChessController onBoundary = _camps.RegisterChess(2, 1, new Vector2(2f, 0f));
        ChessController corpse = _camps.RegisterChess(2, 1, new Vector2(0.5f, 0f));
        corpse.DamageReceiver.ApplyDamage(50);
        corpse.RuntimeData.Hp = 50;

        using (var manager = new CircularDamageManager(_camps))
        {
            manager.Register(Vector2.zero, 2f, 0f, new[] { 2 }, 9);
            manager.Tick(0f);

            Assert.That(corpse.Fsm.CurrentState, Is.TypeOf<DeadState>());
            Assert.That(corpse.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(outside.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(onBoundary.RuntimeData.Hp, Is.EqualTo(41));
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(41));
        }
    }

    /// <summary>
    /// 验证延迟期间走进圆会被打到，走出圆则不会。
    /// </summary>
    [Test]
    public void TickUsesPositionAtTriggerTime()
    {
        using (var manager = new CircularDamageManager(_camps))
        {
            _enemy.RuntimeData.CurrentPosition = new Vector2(5f, 0f);
            manager.Register(Vector2.zero, 2f, 0.2f, new[] { 2 }, 6);
            manager.Tick(0.1f);
            _enemy.RuntimeData.CurrentPosition = new Vector2(1f, 0f);
            manager.Tick(0.1f);
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(44));
        }

        using (var manager = new CircularDamageManager(_camps))
        {
            _ally.RuntimeData.CurrentPosition = Vector2.zero;
            manager.Register(Vector2.zero, 2f, 0.2f, new[] { 1 }, 6);
            manager.Tick(0.1f);
            _ally.RuntimeData.CurrentPosition = new Vector2(5f, 0f);
            manager.Tick(0.1f);
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
        }
    }

    /// <summary>
    /// 验证调用方事后修改阵营集合不影响已注册实例。
    /// </summary>
    [Test]
    public void RegisterCopiesAffectedCamps()
    {
        using (var manager = new CircularDamageManager(_camps))
        {
            var camps = new HashSet<int> { 2 };
            manager.Register(Vector2.zero, 2f, 0f, camps, 4);
            camps.Remove(2);
            camps.Add(1);

            manager.Tick(0f);

            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(46));
            Assert.That(_ally.RuntimeData.Hp, Is.EqualTo(50));
        }
    }

    /// <summary>
    /// 验证 delay 为 0 时不在 Register 内结算，而在随后一次 Tick 结算。
    /// </summary>
    [Test]
    public void ZeroDelayWaitsUntilTick()
    {
        using (var manager = new CircularDamageManager(_camps))
        {
            CircularDamageRuntimeData instance = manager.Register(
                Vector2.zero,
                2f,
                0f,
                new[] { 2 },
                8);

            Assert.That(instance, Is.Not.Null);
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(manager.ActiveCount, Is.EqualTo(1));

            manager.Tick(0f);
            Assert.That(_enemy.RuntimeData.Hp, Is.EqualTo(42));
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
            Assert.That(instance.CircularDamageId, Is.EqualTo(0));
            Assert.That(instance.Damage, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证非法参数返回 null，空阵营集合也拒绝注册。
    /// </summary>
    [Test]
    public void RegisterRejectsInvalidParameters()
    {
        using (var manager = new CircularDamageManager(_camps))
        {
            int[] camps = { 1 };
            Assert.That(
                manager.Register(new Vector2(float.NaN, 0f), 1f, 0f, camps, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, -1f, 0f, camps, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, 1f, -0.1f, camps, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, 1f, 0f, camps, -1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, 1f, 0f, null, 1),
                Is.Null);
            Assert.That(
                manager.Register(Vector2.zero, 1f, 0f, Array.Empty<int>(), 1),
                Is.Null);
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证释放管理器会回收未触发实例，且可重复释放。
    /// </summary>
    [Test]
    public void DisposeRecyclesPendingInstances()
    {
        var manager = new CircularDamageManager(_camps);
        CircularDamageRuntimeData instance = manager.Register(
            Vector2.zero,
            1f,
            1f,
            new[] { 1 },
            3);

        manager.Dispose();

        Assert.That(manager.ActiveCount, Is.EqualTo(0));
        Assert.That(instance.CircularDamageId, Is.EqualTo(0));
        Assert.That(instance.AffectedCamps, Is.Empty);
        Assert.DoesNotThrow(manager.Dispose);
        Assert.Throws<ObjectDisposedException>(
            () => manager.Register(Vector2.zero, 1f, 0f, new[] { 1 }, 1));
    }

    /// <summary>
    /// 验证 BattleManager 持有圆形伤害管理器并用指定时间步推进到期结算。
    /// </summary>
    [Test]
    public void BattleManagerOwnsAndTicksCircularDamageManager()
    {
        using (var battleManager = new BattleManager())
        {
            battleManager.Configs.Register(CreateConfig(1, 50));
            int allyId = battleManager.RegisterChess(1, 1, Vector2.zero);
            int enemyId = battleManager.RegisterChess(2, 1, new Vector2(1f, 0f));
            Assert.That(battleManager.TryGetChess(allyId, out ChessController ally), Is.True);
            Assert.That(battleManager.TryGetChess(enemyId, out ChessController enemy), Is.True);

            Assert.That(
                battleManager.RegisterCircularDamage(Vector2.zero, 2f, 0.1f, new[] { 2 }, 11),
                Is.True);
            Assert.That(
                battleManager.RegisterCircularDamage(Vector2.zero, 2f, 0.1f, Array.Empty<int>(), 11),
                Is.False);

            battleManager.Tick(0.1f);

            Assert.That(enemy.RuntimeData.Hp, Is.EqualTo(39));
            Assert.That(ally.RuntimeData.Hp, Is.EqualTo(50));
            Assert.That(battleManager.CircularDamageManager.ActiveCount, Is.EqualTo(0));
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
