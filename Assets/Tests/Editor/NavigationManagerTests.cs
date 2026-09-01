#if UNITY_EDITOR
using System.Collections.Generic;
using AutoChess;
using AutoChess.Navigation;
using Chess;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证主线程 NavigationManager 与 SimulatorBurst 的生命周期和边界行为。</summary>
public class NavigationManagerTests
{
    private ChessBoard _board;
    private NavigationManager _navigation;

    [SetUp]
    public void SetUp()
    {
        _board = new ChessBoard { Width = 100, Height = 100 };
        _navigation = new NavigationManager(_board);
    }

    [TearDown]
    public void TearDown()
    {
        _navigation.Dispose();
    }

    /// <summary>验证代理 ID 单调递增且注销后不会复用旧 ID。</summary>
    [Test]
    public void AgentIdsAreMonotonic()
    {
        NavigationAgentHandle first = _navigation.RegisterAgent(new Vector2(1f, 1f), 0.5f, 2f);
        Assert.That(_navigation.UnregisterAgent(first), Is.True);
        NavigationAgentHandle second = _navigation.RegisterAgent(new Vector2(2f, 2f), 0.5f, 2f);

        Assert.That(second.AgentId, Is.GreaterThan(first.AgentId));
    }

    /// <summary>验证固定目标移动会使用 RVO 输出并限制在半径内边界。</summary>
    [Test]
    public void TickClampsPositionInsideBoard()
    {
        NavigationAgentHandle agent = _navigation.RegisterAgent(new Vector2(0f, 0f), 0.5f, 10f);
        NavigationInputSnapshot input = new NavigationInputSnapshot(
            1,
            agent,
            new Vector2(0f, 0f),
            new Vector2(100f, 100f),
            10f,
            10f,
            true,
            1);

        IReadOnlyList<NavigationResultSnapshot> results = _navigation.Tick(1, new[] { input }, 0.1f);

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Position.x, Is.InRange(0.5f, 99.5f));
        Assert.That(results[0].Position.y, Is.InRange(0.5f, 99.5f));
    }

    /// <summary>验证两个代理可以同时推进且结果按输入代理返回。</summary>
    [Test]
    public void TickReturnsResultsForRegisteredAgents()
    {
        NavigationAgentHandle left = _navigation.RegisterAgent(new Vector2(2f, 2f), 0.5f, 4f);
        NavigationAgentHandle right = _navigation.RegisterAgent(new Vector2(8f, 2f), 0.5f, 4f);
        NavigationInputSnapshot[] inputs =
        {
            new NavigationInputSnapshot(1, left, new Vector2(2f, 2f), new Vector2(6f, 2f), 4f, 4f, true, 1),
            new NavigationInputSnapshot(1, right, new Vector2(8f, 2f), new Vector2(4f, 2f), 4f, 4f, true, 1)
        };

        IReadOnlyList<NavigationResultSnapshot> results = _navigation.Tick(1, inputs, 0.25f);
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results[0].Agent == left || results[1].Agent == left, Is.True);
        Assert.That(results[0].Agent == right || results[1].Agent == right, Is.True);
    }

    /// <summary>验证未注册代理的输入不会产生有效结果。</summary>
    [Test]
    public void UnknownAgentInputIsIgnored()
    {
        NavigationInputSnapshot input = new NavigationInputSnapshot(
            1,
            new NavigationAgentHandle(999),
            Vector2.zero,
            Vector2.one,
            1f,
            1f,
            true,
            1);

        Assert.That(_navigation.Tick(1, new[] { input }, 0.1f).Count, Is.EqualTo(0));
    }
}
#endif
