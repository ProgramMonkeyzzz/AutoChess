#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AutoChess;
using AutoChess.Navigation;
using AutoChess.Utils;
using Camp;
using Chess;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证阵营工厂注册、注销和稳定查询顺序。</summary>
public class CampManagerTests
{
    [TearDown]
    public void TearDown()
    {
        while (ThreadLogQueue.TryDequeue(out _))
        {
        }
    }

    /// <summary>验证棋子按导航代理 ID 排序，代理 ID 相同时再按 UniqueId。</summary>
    [Test]
    public void GetAllChessesSortsByAgentIdThenUniqueId()
    {
        CampManager camps = CreateManager(8, 3);
        var board = new ChessBoard { Width = 20, Height = 20 };
        ChessController first = camps.RegisterChess(2, 8, 20, Vector2.zero);
        ChessController second = camps.RegisterChess(1, 3, 10, Vector2.zero);
        first.InitializeMovement(board, new NavigationAgentHandle(2));
        second.InitializeMovement(board, new NavigationAgentHandle(1));

        CollectionAssert.AreEqual(new[] { second, first }, new List<ChessController>(camps.GetAllChesses()));

        first.InitializeMovement(board, new NavigationAgentHandle(5));
        second.InitializeMovement(board, new NavigationAgentHandle(5));
        CollectionAssert.AreEqual(new[] { second, first }, new List<ChessController>(camps.GetAllChesses()));

        Assert.DoesNotThrow(() => camps.Tick(0.016f));
        Assert.That(camps.UnregisterChess(first.UniqueId), Is.True);
        CollectionAssert.AreEqual(new[] { second }, new List<ChessController>(camps.GetAllChesses()));
        Assert.That(camps.TryGetChess(first.UniqueId, out _), Is.False);
    }

    /// <summary>验证敌方棋子查询排除自身阵营。</summary>
    [Test]
    public void GetEnemyChessesExcludesOwnerCamp()
    {
        CampManager camps = CreateManager(1);
        ChessController ally = camps.RegisterChess(1, 1, new Vector2(1f, 2f));
        ChessController enemy = camps.RegisterChess(2, 1, new Vector2(3f, 4f));

        CollectionAssert.AreEquivalent(new[] { enemy }, camps.GetEnemyChesses(1));
        CollectionAssert.AreEquivalent(new[] { ally }, camps.GetEnemyChesses(2));
        Assert.That(ally.RuntimeData.CurrentPosition, Is.EqualTo(new Vector2(1f, 2f)));
        Assert.That(enemy.RuntimeData.CurrentPosition, Is.EqualTo(new Vector2(3f, 4f)));
        Assert.That(ally.ConfigId, Is.EqualTo(1));
        Assert.That(enemy.ConfigId, Is.EqualTo(1));
    }

    /// <summary>验证 ConfigId=0 可以工厂注册，且创建时会 clone 配置。</summary>
    [Test]
    public void RegisterChessClonesConfigAndAllowsZeroConfigId()
    {
        var configs = new ChessConfigRepository();
        configs.Register(CreateConfig(0, 50));
        var camps = new CampManager(configs);
        Assert.That(configs.TryGet(0, out ChessBaseData stored), Is.True);

        ChessController chess = camps.RegisterChess(1, 0, new Vector2(5f, 6f));
        Assert.That(chess, Is.Not.Null);
        Assert.That(chess.UniqueId, Is.GreaterThan(0));
        Assert.That(chess.ConfigId, Is.EqualTo(0));
        Assert.That(chess.BaseData, Is.Not.SameAs(stored));
        chess.BaseData.MaxHp = 1;
        Assert.That(stored.MaxHp, Is.EqualTo(50));
        Assert.That(camps.TryGetChess(chess.UniqueId, out ChessController found), Is.True);
        Assert.That(found, Is.SameAs(chess));
    }

    /// <summary>验证找不到配置时工厂注册返回 null，不写入 UniqueId 索引。</summary>
    [Test]
    public void RegisterChessReturnsNullWhenConfigMissing()
    {
        var camps = new CampManager();
        Assert.That(camps.RegisterChess(1, 99, Vector2.zero), Is.Null);
        Assert.That(new List<ChessController>(camps.GetAllChesses()), Is.Empty);
    }

    /// <summary>验证同一 UniqueId 不能重复注册。</summary>
    [Test]
    public void RegisterChessRejectsDuplicateUniqueId()
    {
        CampManager camps = CreateManager(1);
        Assert.That(camps.RegisterChess(1, 1, 7, Vector2.zero), Is.Not.Null);
        Assert.Throws<InvalidOperationException>(() => camps.RegisterChess(1, 1, 7, Vector2.one));
    }

    /// <summary>创建已注册指定 ConfigId 的阵营管理器。</summary>
    private static CampManager CreateManager(params int[] configIds)
    {
        var configs = new ChessConfigRepository();
        for (int i = 0; i < configIds.Length; i++)
        {
            configs.Register(CreateConfig(configIds[i], 100));
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
