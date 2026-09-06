#if UNITY_EDITOR
using System;
using System.Threading;
using AutoChess;
using AutoChess.Bullet;
using AutoChess.Render;
using Chess;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>覆盖主线程渲染视图的创建、快照同步和死亡/命中清除。</summary>
public class RenderManagerTests
{
    private Transform _root;
    private GameObject _chessPrefab;
    private GameObject _bulletPrefab;
    private RenderManager _renderManager;

    [SetUp]
    public void SetUp()
    {
        GameObject rootObject = new GameObject("RenderManagerTestsRoot");
        _root = rootObject.transform;
        _chessPrefab = new GameObject("ChessViewPrefab");
        _chessPrefab.AddComponent<ChessView>();
        _bulletPrefab = new GameObject("BulletViewPrefab");
        _bulletPrefab.AddComponent<BulletView>();
        _renderManager = new RenderManager(_chessPrefab, _bulletPrefab, _root);
    }

    [TearDown]
    public void TearDown()
    {
        _renderManager?.Dispose();
        _renderManager = null;
        DestroyImmediateIfExists(_chessPrefab);
        DestroyImmediateIfExists(_bulletPrefab);
        DestroyImmediateIfExists(_root == null ? null : _root.gameObject);
    }

    /// <summary>验证棋子实例 ID 从 1 起单调递增。</summary>
    [Test]
    public void ChessUniqueIdsAreMonotonic()
    {
        var first = new ChessController();
        var second = new ChessController();
        Assert.That(first.UniqueId, Is.GreaterThan(0));
        Assert.That(second.UniqueId, Is.GreaterThan(first.UniqueId));
    }

    /// <summary>验证子弹初始化分配新 ID，回收后再初始化使用更大的 ID。</summary>
    [Test]
    public void BulletIdsIncreaseAfterRecycle()
    {
        var owner = new ChessController(CreateConfig());
        var target = new ChessController(CreateConfig());
        owner.RuntimeData.CurrentPosition = Vector2.zero;
        target.RuntimeData.CurrentPosition = new Vector2(0.1f, 0f);

        using (var manager = new BulletManager())
        {
            BulletRuntimeData first = manager.RegisterBullet(owner, target, 10f);
            int firstId = first.BulletId;
            Assert.That(firstId, Is.GreaterThan(0));

            manager.Tick(1f);
            Assert.That(manager.ActiveBulletCount, Is.EqualTo(0));

            BulletRuntimeData second = manager.RegisterBullet(owner, target, 10f);
            Assert.That(second.BulletId, Is.GreaterThan(firstId));
        }
    }

    /// <summary>验证 ApplyFrame 会创建棋子和子弹视图并写入 XZ 位置与朝向。</summary>
    [Test]
    public void ApplyFrameCreatesViewsAndWritesTransform()
    {
        var frame = new BattleRenderFrame(
            1,
            new[] { new ChessRenderSnapshot(11, 1, new Vector2(3f, 4f), Vector2.right) },
            new[] { new BulletRenderSnapshot(21, new Vector2(5f, 6f), Vector2.up) });

        _renderManager.ApplyFrame(frame);

        Assert.That(_renderManager.ChessViewCount, Is.EqualTo(1));
        Assert.That(_renderManager.BulletViewCount, Is.EqualTo(1));
        Assert.That(_renderManager.TryGetChessView(11, out ChessView chessView), Is.True);
        Assert.That(_renderManager.TryGetBulletView(21, out BulletView bulletView), Is.True);
        Assert.That(chessView.transform.position, Is.EqualTo(new Vector3(3f, 0.5f, 4f)));
        Assert.That(bulletView.transform.position, Is.EqualTo(new Vector3(5f, 0.3f, 6f)));
        Assert.That(Vector3.Dot(chessView.transform.forward, Vector3.right), Is.GreaterThan(0.99f));
        Assert.That(Vector3.Dot(bulletView.transform.forward, Vector3.forward), Is.GreaterThan(0.99f));
    }

    /// <summary>验证后续快照缺少死亡棋子和命中子弹时会销毁对应视图。</summary>
    [Test]
    public void ApplyFrameRemovesDeadChessAndHitBullets()
    {
        _renderManager.ApplyFrame(new BattleRenderFrame(
            1,
            new[]
            {
                new ChessRenderSnapshot(1, 1, new Vector2(1f, 1f), Vector2.up),
                new ChessRenderSnapshot(2, 2, new Vector2(2f, 2f), Vector2.up)
            },
            new[]
            {
                new BulletRenderSnapshot(8, new Vector2(3f, 3f), Vector2.up),
                new BulletRenderSnapshot(9, new Vector2(4f, 4f), Vector2.up)
            }));

        _renderManager.ApplyFrame(new BattleRenderFrame(
            2,
            new[] { new ChessRenderSnapshot(1, 1, new Vector2(8f, 9f), Vector2.up) },
            Array.Empty<BulletRenderSnapshot>()));

        Assert.That(_renderManager.ChessViewCount, Is.EqualTo(1));
        Assert.That(_renderManager.BulletViewCount, Is.EqualTo(0));
        Assert.That(_renderManager.TryGetChessView(1, out ChessView living), Is.True);
        Assert.That(living.transform.position, Is.EqualTo(new Vector3(8f, 0.5f, 9f)));
        Assert.That(_renderManager.TryGetChessView(2, out _), Is.False);
        Assert.That(_renderManager.TryGetBulletView(8, out _), Is.False);
        Assert.That(_renderManager.TryGetBulletView(9, out _), Is.False);
    }

    /// <summary>验证同一 UniqueId 重复创建不会实例化第二份视图。</summary>
    [Test]
    public void CreateChessViewIsIdempotent()
    {
        ChessView first = _renderManager.CreateChessView(5, 1);
        ChessView second = _renderManager.CreateChessView(5, 2);
        Assert.That(second, Is.SameAs(first));
        Assert.That(_renderManager.ChessViewCount, Is.EqualTo(1));
        Assert.That(_root.GetComponentsInChildren<ChessView>(true).Length, Is.EqualTo(1));
    }

    /// <summary>验证 Clear/Dispose 后全部视图被销毁。</summary>
    [Test]
    public void DisposeClearsAllViews()
    {
        _renderManager.CreateChessView(1, 1);
        _renderManager.CreateBulletView(2);
        _renderManager.Dispose();
        _renderManager = null;

        Assert.That(_root.GetComponentsInChildren<ChessView>(true), Is.Empty);
        Assert.That(_root.GetComponentsInChildren<BulletView>(true), Is.Empty);
    }

    /// <summary>验证零向量朝向不会改写当前旋转。</summary>
    [Test]
    public void SetForwardIgnoresZeroVector()
    {
        ChessView view = _renderManager.CreateChessView(3, 1);
        view.SetForward(Vector2.right);
        Vector3 before = view.transform.forward;
        view.SetForward(Vector2.zero);
        Assert.That(view.transform.forward, Is.EqualTo(before));
    }

    private static ChessBaseData CreateConfig()
    {
        return new ChessBaseData
        {
            MaxHp = 100,
            Atk = 1,
            Range = 10,
            AtkSpeed = 1f,
            WindUpTime = 0f,
            BulletSpeed = 10f
        };
    }

    private static void DestroyImmediateIfExists(GameObject target)
    {
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

/// <summary>覆盖 BattleManager 发布渲染快照时排除死亡棋子和已回收子弹。</summary>
public class BattleRenderSnapshotTests
{
    [TearDown]
    public void TearDown()
    {
        EventDispatcher.Instance.ClearAll();
    }

    /// <summary>验证战斗步进后可以取到包含存活棋子的最新渲染帧。</summary>
    [Test]
    public void BattleStepPublishesLivingChessSnapshot()
    {
        using (var manager = new BattleManager())
        {
            manager.Configs.Register(CreateConfig(1, 50));
            int uniqueId = manager.RegisterChess(1, 1, new Vector2(5f, 7f));
            Assert.That(manager.TryGetChess(uniqueId, out ChessController chess), Is.True);
            manager.Start();
            manager.RequestAdvance(1);

            BattleRenderFrame frame = WaitForFrame(manager);
            Assert.That(frame.Chess.Length, Is.EqualTo(1));
            Assert.That(frame.Chess[0].UniqueId, Is.EqualTo(uniqueId));
            Assert.That(frame.Chess[0].CampId, Is.EqualTo(1));
            Assert.That(frame.Chess[0].Position, Is.EqualTo(new Vector2(5f, 7f)));
            Assert.That(frame.Chess[0].HpProgress, Is.EqualTo(1f));
            Assert.That(frame.Bullets, Is.Empty);
        }
    }

    /// <summary>验证受伤棋子快照写入 Hp / MaxHp 进度。</summary>
    [Test]
    public void DamagedChessSnapshotReportsHpProgress()
    {
        using (var manager = new BattleManager())
        {
            manager.Configs.Register(CreateConfig(1, 50));
            int uniqueId = manager.RegisterChess(1, 1, new Vector2(5f, 7f));
            Assert.That(manager.TryGetChess(uniqueId, out ChessController chess), Is.True);
            chess.DamageReceiver.ApplyDamage(20);
            manager.Start();
            manager.RequestAdvance(1);

            BattleRenderFrame frame = WaitForFrame(manager);
            Assert.That(frame.Chess.Length, Is.EqualTo(1));
            Assert.That(frame.Chess[0].HpProgress, Is.EqualTo(0.6f).Within(0.0001f));
        }
    }

    /// <summary>验证 CreateRenderSnapshot 直接使用当前 HP 与 MaxHp。</summary>
    [Test]
    public void CreateRenderSnapshotUsesHpOverMaxHp()
    {
        var chess = new ChessController(CreateConfig(1, 100));
        chess.RuntimeData.Hp = 40;
        ChessRenderSnapshot snapshot = chess.CreateRenderSnapshot();
        Assert.That(snapshot.HpProgress, Is.EqualTo(0.4f).Within(0.0001f));
    }

    /// <summary>验证 MaxHp 非法时快照进度按满血处理。</summary>
    [Test]
    public void CreateRenderSnapshotTreatsInvalidMaxHpAsFull()
    {
        var chess = new ChessController(CreateConfig(1, 0));
        ChessRenderSnapshot snapshot = chess.CreateRenderSnapshot();
        Assert.That(snapshot.HpProgress, Is.EqualTo(1f));
    }

    /// <summary>验证死亡棋子和命中回收后的子弹不会出现在渲染帧中。</summary>
    [Test]
    public void DeadChessAndRecycledBulletsAreOmitted()
    {
        using (var manager = new BattleManager())
        {
            manager.Configs.Register(CreateConfig(1, 10));
            manager.Configs.Register(CreateConfig(2, 10));
            int ownerId = manager.RegisterChess(1, 1, Vector2.zero);
            int targetId = manager.RegisterChess(2, 2, new Vector2(3f, 0f));
            Assert.That(manager.TryGetChess(ownerId, out ChessController owner), Is.True);
            Assert.That(manager.TryGetChess(targetId, out ChessController target), Is.True);
            Assert.That(manager.BulletManager.RegisterBullet(owner, target, 10f), Is.Not.Null);
            owner.DamageReceiver.ApplyDamage(10);
            Assert.That(owner.Fsm.CurrentState, Is.TypeOf<DeadState>());

            manager.Start();
            manager.RequestAdvance(1);

            BattleRenderFrame frame = WaitForFrame(manager);
            Assert.That(frame.Chess.Length, Is.EqualTo(1));
            Assert.That(frame.Chess[0].UniqueId, Is.EqualTo(targetId));
            Assert.That(frame.Bullets.Length, Is.EqualTo(1));

            for (int i = 0; i < 10; i++)
            {
                manager.RequestAdvance(4);
                frame = WaitForFrame(manager);
                if (frame.Bullets.Length == 0)
                {
                    break;
                }
            }

            Assert.That(frame.Bullets, Is.Empty);
            foreach (ChessRenderSnapshot snapshot in frame.Chess)
            {
                Assert.That(snapshot.UniqueId, Is.Not.EqualTo(ownerId));
            }
        }
    }

    private static BattleRenderFrame WaitForFrame(BattleManager manager)
    {
        for (int i = 0; i < 50; i++)
        {
            if (manager.TryConsumeRenderFrame(out BattleRenderFrame frame))
            {
                return frame;
            }

            Thread.Sleep(20);
        }

        Assert.Fail("未在限定时间内收到渲染帧。");
        return null;
    }

    private static ChessBaseData CreateConfig(int configId, int maxHp)
    {
        return new ChessBaseData
        {
            ConfigId = configId,
            MaxHp = maxHp,
            Atk = 1,
            Range = 10,
            AtkSpeed = 1f,
            WindUpTime = 0f,
            BulletSpeed = 10f
        };
    }
}
#endif