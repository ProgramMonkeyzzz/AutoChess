#if UNITY_EDITOR
using AutoChess;
using AutoChess.Bullet;
using Chess;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

public class BulletManagerTests
{
    private ChessController _owner;
    private ChessController _target;

    [SetUp]
    public void SetUp()
    {
        EventDispatcher.Instance.ClearAll();
        _owner = new ChessController(CreateConfig(100, 12));
        _target = new ChessController(CreateConfig(100, 1));
        _owner.RuntimeData.CurrentPosition = Vector2.zero;
        _target.RuntimeData.CurrentPosition = new Vector2(3f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        EventDispatcher.Instance.ClearAll();
    }

    /// <summary>
    /// 验证发射事件会从对象池注册一枚具有正确初始数据的子弹。
    /// </summary>
    [Test]
    public void SpawnEventRegistersPooledBullet()
    {
        using (var manager = new BulletManager())
        {
            EventDispatcher.Instance.Send(new BulletSpawnEvent(_owner, _target, 5f));

            Assert.That(manager.ActiveBulletCount, Is.EqualTo(1));
            BulletRuntimeData bullet = GetOnlyBullet(manager);
            Assert.That(bullet.IsFromPool, Is.True);
            Assert.That(bullet.OwnerChess, Is.SameAs(_owner));
            Assert.That(bullet.TargetChess, Is.SameAs(_target));
            Assert.That(bullet.CurrentPosition, Is.EqualTo(Vector2.zero));
            Assert.That(bullet.Speed, Is.EqualTo(5f));
            Assert.That(bullet.Damage, Is.EqualTo(12));
        }
    }

    /// <summary>
    /// 验证子弹按时间步长移动并持续追踪存活目标的最新位置。
    /// </summary>
    [Test]
    public void TickMovesBulletAndTracksMovingTarget()
    {
        using (var manager = new BulletManager())
        {
            BulletRuntimeData bullet = manager.RegisterBullet(_owner, _target, 10f);

            manager.Tick(0.1f);
            Assert.That(bullet.CurrentPosition, Is.EqualTo(new Vector2(1f, 0f)));

            _target.RuntimeData.CurrentPosition = new Vector2(4f, 0f);
            manager.Tick(0.1f);

            Assert.That(bullet.CurrentPosition, Is.EqualTo(new Vector2(2f, 0f)));
            Assert.That(bullet.LastTargetPosition, Is.EqualTo(new Vector2(4f, 0f)));
        }
    }

    /// <summary>
    /// 验证子弹命中使用创建时锁定的攻击力，飞行期间攻击者增益或死亡都不改变伤害。
    /// </summary>
    [Test]
    public void HitUsesSnapshotAttackEvenWhenOwnerBuffsOrDiesDuringFlight()
    {
        using (var manager = new BulletManager())
        {
            manager.RegisterBullet(_owner, _target, 10f);
            manager.Tick(0.1f);
            _owner.RuntimeData.CurrentAtkMultipler = 2f;
            _owner.DamageReceiver.ApplyDamage(100);

            manager.Tick(0.2f);

            Assert.That(_owner.Fsm.CurrentState, Is.TypeOf<DeadState>());
            Assert.That(_target.RuntimeData.Hp, Is.EqualTo(88));
            Assert.That(manager.ActiveBulletCount, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证创建时已存在的攻击力增益会被快照，飞行期间倍率回落不影响命中伤害。
    /// </summary>
    [Test]
    public void HitKeepsAttackSnapshotTakenAtBulletCreation()
    {
        using (var manager = new BulletManager())
        {
            _owner.RuntimeData.CurrentAtkMultipler = 2f;
            BulletRuntimeData bullet = manager.RegisterBullet(_owner, _target, 10f);
            Assert.That(bullet.Damage, Is.EqualTo(24));

            _owner.RuntimeData.CurrentAtkMultipler = 1f;
            manager.Tick(0.3f);

            Assert.That(_target.RuntimeData.Hp, Is.EqualTo(76));
            Assert.That(manager.ActiveBulletCount, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证目标失效后子弹飞向最后有效位置，抵达时不造成伤害。
    /// </summary>
    [Test]
    public void InvalidTargetUsesLastPositionAndDealsNoDamage()
    {
        using (var manager = new BulletManager())
        {
            BulletRuntimeData bullet = manager.RegisterBullet(_owner, _target, 10f);
            manager.Tick(0.1f);
            _target.RuntimeData.Hp = 0;
            _target.Fsm.CheckCurrentStateTransition();
            // 恢复 HP 以便断言失效目标抵达时不会被补伤。
            _target.RuntimeData.Hp = 100;
            _target.RuntimeData.CurrentPosition = new Vector2(9f, 0f);

            manager.Tick(0.2f);

            Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));
            Assert.That(manager.ActiveBulletCount, Is.EqualTo(0));
            Assert.That(bullet.OwnerChess, Is.Null);
            Assert.That(bullet.TargetChess, Is.Null);
            Assert.That(bullet.Speed, Is.EqualTo(0f));
        }
    }

    /// <summary>
    /// 验证命中只结算一次，并清空回收到对象池的运行时数据。
    /// </summary>
    [Test]
    public void HitAppliesDamageOnceAndRecyclesRuntimeData()
    {
        using (var manager = new BulletManager())
        {
            BulletRuntimeData bullet = manager.RegisterBullet(_owner, _target, 10f);

            manager.Tick(0.3f);
            manager.Tick(1f);

            Assert.That(_target.RuntimeData.Hp, Is.EqualTo(88));
            Assert.That(manager.ActiveBulletCount, Is.EqualTo(0));
            Assert.That(bullet.IsFromPool, Is.True);
            Assert.That(bullet.OwnerChess, Is.Null);
            Assert.That(bullet.CurrentPosition, Is.EqualTo(Vector2.zero));
            Assert.That(bullet.Damage, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// 验证释放管理器会反注册事件监听并回收全部活动子弹。
    /// </summary>
    [Test]
    public void DisposeUnregistersListenerAndRecyclesActiveBullets()
    {
        var manager = new BulletManager();
        BulletRuntimeData bullet = manager.RegisterBullet(_owner, _target, 1f);

        manager.Dispose();

        Assert.That(EventDispatcher.Instance.GetListenerCount<BulletSpawnEvent>(), Is.EqualTo(0));
        Assert.That(manager.ActiveBulletCount, Is.EqualTo(0));
        Assert.That(bullet.OwnerChess, Is.Null);
        Assert.DoesNotThrow(manager.Dispose);
    }

    /// <summary>
    /// 验证 BattleManager 初始化并使用指定时间步长推进子弹管理器。
    /// </summary>
    [Test]
    public void BattleManagerOwnsAndTicksBulletManager()
    {
        using (var battleManager = new BattleManager())
        {
            EventDispatcher.Instance.Send(new BulletSpawnEvent(_owner, _target, 10f));

            battleManager.Tick(0.3f);

            Assert.That(_target.RuntimeData.Hp, Is.EqualTo(88));
            Assert.That(battleManager.BulletManager.ActiveBulletCount, Is.EqualTo(0));
        }
    }

    private static ChessBaseData CreateConfig(int maxHp, int atk)
    {
        return new ChessBaseData
        {
            MaxHp = maxHp,
            Atk = atk,
            Range = 10,
            AtkSpeed = 1f,
            WindUpTime = 0f,
            BulletSpeed = 10f
        };
    }

    /// <summary>
    /// 获取测试中唯一的活动子弹。
    /// </summary>
    private static BulletRuntimeData GetOnlyBullet(BulletManager manager)
    {
        foreach (BulletRuntimeData bullet in manager.Bullets)
        {
            return bullet;
        }

        Assert.Fail("预期存在一枚活动子弹。");
        return null;
    }
}
#endif
