#if UNITY_EDITOR
using System.Collections.Generic;
using AutoChess;
using AutoChess.Navigation;
using Camp;
using Chess;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证阵营注册、注销和稳定查询顺序。</summary>
public class CampManagerTests
{
    /// <summary>验证棋子按阵营 ID 和代理 ID 稳定排序。</summary>
    [Test]
    public void GetAllChessesSortsByAgentIdThenChessId()
    {
        var board = new ChessBoard { Width = 20, Height = 20 };
        var camps = new CampManager();
        var first = new ChessController();
        var second = new ChessController();
        first.InitializeMovement(board, new NavigationAgentHandle(2));
        second.InitializeMovement(board, new NavigationAgentHandle(1));
        camps.RegisterChess(2, 8, first);
        camps.RegisterChess(1, 3, second);

        CollectionAssert.AreEqual(new[] { second, first }, new List<ChessController>(camps.GetAllChesses()));
        Assert.DoesNotThrow(() => camps.Tick(0.016f));
        Assert.That(camps.UnregisterChess(first), Is.True);
        CollectionAssert.AreEqual(new[] { second }, new List<ChessController>(camps.GetAllChesses()));
    }

    /// <summary>验证敌方棋子查询排除自身阵营。</summary>
    [Test]
    public void GetEnemyChessesExcludesOwnerCamp()
    {
        var camps = new CampManager();
        var ally = new ChessController();
        var enemy = new ChessController();
        camps.RegisterChess(1, 1, ally);
        camps.RegisterChess(2, 1, enemy);

        CollectionAssert.AreEquivalent(new[] { enemy }, camps.GetEnemyChesses(1));
        CollectionAssert.AreEquivalent(new[] { ally }, camps.GetEnemyChesses(2));
    }
}
#endif
