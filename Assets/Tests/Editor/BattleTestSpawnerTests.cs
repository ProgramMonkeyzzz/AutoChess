#if UNITY_EDITOR
using System.Collections.Generic;
using AutoChess;
using AutoChess.Sandbox;
using NUnit.Framework;
using UnityEngine;

/// <summary>覆盖战斗测试布阵收集与注册顺序、跳过规则和失败隔离。</summary>
public class BattleTestSpawnerTests
{
    private ChessBoard _board;
    private List<(int CampId, int ConfigId, Vector2 Position)> _registered;
    private List<string> _messages;
    private int _nextUniqueId;

    [SetUp]
    public void SetUp()
    {
        _board = new ChessBoard { Width = 100, Height = 100 };
        _registered = new List<(int, int, Vector2)>();
        _messages = new List<string>();
        _nextUniqueId = 1;
    }

    /// <summary>验证空或 null 阵营、空棋子列表都不会调用注册。</summary>
    [Test]
    public void SpawnSkipsNullOrEmptyCamps()
    {
        Assert.That(BattleTestSpawner.Spawn(null, _board, Register, _messages), Is.EqualTo(0));
        Assert.That(
            BattleTestSpawner.Spawn(new List<BattleTestCampConfig>(), _board, Register, _messages),
            Is.EqualTo(0));
        Assert.That(
            BattleTestSpawner.Spawn(
                new List<BattleTestCampConfig>
                {
                    new BattleTestCampConfig { CampId = 1 },
                    new BattleTestCampConfig { CampId = 2, Chesses = null }
                },
                _board,
                Register,
                _messages),
            Is.EqualTo(0));
        Assert.That(_registered, Is.Empty);
    }

    /// <summary>验证多阵营按配置顺序提交，重复 CampId 仍全部注册。</summary>
    [Test]
    public void SpawnSubmitsCampsInOrderAndKeepsDuplicateCampIds()
    {
        var camps = new List<BattleTestCampConfig>
        {
            new BattleTestCampConfig
            {
                CampId = 1,
                Chesses = new List<BattleTestChessConfig>
                {
                    new BattleTestChessConfig { ConfigId = 0, X = 10f, Z = 11f },
                    new BattleTestChessConfig { ConfigId = 1, X = 12f, Z = 13f }
                }
            },
            new BattleTestCampConfig
            {
                CampId = 1,
                Chesses = new List<BattleTestChessConfig>
                {
                    new BattleTestChessConfig { ConfigId = 0, X = 14f, Z = 15f }
                }
            },
            new BattleTestCampConfig
            {
                CampId = 2,
                Chesses = new List<BattleTestChessConfig>
                {
                    new BattleTestChessConfig { ConfigId = 1, X = 16f, Z = 17f }
                }
            }
        };

        Assert.That(BattleTestSpawner.Spawn(camps, _board, Register, _messages), Is.EqualTo(4));
        CollectionAssert.AreEqual(
            new[]
            {
                (1, 0, new Vector2(10f, 11f)),
                (1, 1, new Vector2(12f, 13f)),
                (1, 0, new Vector2(14f, 15f)),
                (2, 1, new Vector2(16f, 17f))
            },
            _registered);
        Assert.That(_messages, Is.Empty);
    }

    /// <summary>验证 NaN、Infinity 和棋盘外坐标跳过，合法项仍会注册。</summary>
    [Test]
    public void SpawnSkipsInvalidAndOutOfBoardPositions()
    {
        var camps = new List<BattleTestCampConfig>
        {
            new BattleTestCampConfig
            {
                CampId = 1,
                Chesses = new List<BattleTestChessConfig>
                {
                    new BattleTestChessConfig { ConfigId = 0, X = 50f, Z = 50f },
                    new BattleTestChessConfig { ConfigId = 0, X = float.NaN, Z = 50f },
                    new BattleTestChessConfig { ConfigId = 0, X = 50f, Z = float.PositiveInfinity },
                    new BattleTestChessConfig { ConfigId = 1, X = -1f, Z = 50f },
                    new BattleTestChessConfig { ConfigId = 1, X = 50f, Z = 101f },
                    new BattleTestChessConfig { ConfigId = 1, X = 1f, Z = 2f }
                }
            }
        };

        Assert.That(BattleTestSpawner.Spawn(camps, _board, Register, _messages), Is.EqualTo(2));
        CollectionAssert.AreEqual(
            new[]
            {
                (1, 0, new Vector2(50f, 50f)),
                (1, 1, new Vector2(1f, 2f))
            },
            _registered);
        Assert.That(_messages.Count, Is.EqualTo(4));
        Assert.That(_messages[0], Does.Contain("非法坐标").And.Contain("configId=0"));
        Assert.That(_messages[1], Does.Contain("非法坐标"));
        Assert.That(_messages[2], Does.Contain("棋盘外坐标").And.Contain("configId=1"));
        Assert.That(_messages[3], Does.Contain("棋盘外坐标"));
    }

    /// <summary>验证注册返回 0 时记警告且不影响后续棋子。</summary>
    [Test]
    public void SpawnContinuesAfterRegisterReturnsZero()
    {
        var camps = new List<BattleTestCampConfig>
        {
            new BattleTestCampConfig
            {
                CampId = 2,
                Chesses = new List<BattleTestChessConfig>
                {
                    new BattleTestChessConfig { ConfigId = 99, X = 3f, Z = 4f },
                    new BattleTestChessConfig { ConfigId = 1, X = 5f, Z = 6f }
                }
            }
        };

        int spawned = BattleTestSpawner.Spawn(camps, _board, RegisterFailMissingConfig, _messages);

        Assert.That(spawned, Is.EqualTo(1));
        CollectionAssert.AreEqual(
            new[]
            {
                (2, 99, new Vector2(3f, 4f)),
                (2, 1, new Vector2(5f, 6f))
            },
            _registered);
        Assert.That(_messages.Count, Is.EqualTo(1));
        Assert.That(_messages[0], Does.Contain("注册失败").And.Contain("configId=99"));
    }

    /// <summary>记录一次成功注册并返回单调递增 UniqueId。</summary>
    private int Register(int campId, int configId, Vector2 position)
    {
        _registered.Add((campId, configId, position));
        return _nextUniqueId++;
    }

    /// <summary>ConfigId 99 模拟查不到配置，返回 0；其他配置正常注册。</summary>
    private int RegisterFailMissingConfig(int campId, int configId, Vector2 position)
    {
        _registered.Add((campId, configId, position));
        if (configId == 99)
        {
            return 0;
        }

        return _nextUniqueId++;
    }
}
#endif
