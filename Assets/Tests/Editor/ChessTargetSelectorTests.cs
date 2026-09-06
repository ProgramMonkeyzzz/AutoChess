#if UNITY_EDITOR
using System.Reflection;
using Camp;
using Chess;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 覆盖索敌枚举分发、运行时属性比较、跳过死亡敌人、阵营缺失和非法枚举行为。
/// </summary>
public class ChessTargetSelectorTests
{
    /// <summary>测试用阵营管理器。</summary>
    private CampManager _camps;

    /// <summary>己方棋子。</summary>
    private ChessController _owner;

    /// <summary>较近且生命较低、攻击较高、射程较短的敌人。</summary>
    private ChessController _closeEnemy;

    /// <summary>较远且生命较高、攻击较低、射程较长的敌人。</summary>
    private ChessController _farEnemy;

    /// <summary>
    /// 布置一个己方和两个属性不同的敌人。
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _camps = CreateManager();
        _owner = _camps.RegisterChess(1, 1, Vector2.zero);
        _closeEnemy = _camps.RegisterChess(2, 1, new Vector2(1f, 0f));
        _farEnemy = _camps.RegisterChess(2, 1, new Vector2(10f, 0f));

        _closeEnemy.RuntimeData.Hp = 10;
        _farEnemy.RuntimeData.Hp = 50;
        _closeEnemy.BaseData.Atk = 20;
        _farEnemy.BaseData.Atk = 5;
        _closeEnemy.BaseData.Range = 1f;
        _farEnemy.BaseData.Range = 8f;
    }

    /// <summary>
    /// 验证六种索敌方式分别选中对应敌人。
    /// </summary>
    [Test]
    public void SelectPicksExpectedEnemyForEachMode()
    {
        AssertSelected(ChessTargetSelector.TargetSelectMode.Nearest, _closeEnemy);
        AssertSelected(ChessTargetSelector.TargetSelectMode.Farthest, _farEnemy);
        AssertSelected(ChessTargetSelector.TargetSelectMode.LowestHp, _closeEnemy);
        AssertSelected(ChessTargetSelector.TargetSelectMode.HighestHp, _farEnemy);
        AssertSelected(ChessTargetSelector.TargetSelectMode.LongestRange, _farEnemy);
        AssertSelected(ChessTargetSelector.TargetSelectMode.HighestAtk, _closeEnemy);
    }

    /// <summary>
    /// 验证运行时射程乘区和增量会影响最长射程索敌。
    /// </summary>
    [Test]
    public void LongestRangeUsesRuntimeRange()
    {
        _closeEnemy.BaseData.Range = 4f;
        _farEnemy.BaseData.Range = 4f;
        _closeEnemy.RuntimeData.CurrentRangeMultipler = 1f;
        _closeEnemy.RuntimeData.CurrentRangeIncrement = 0f;
        _farEnemy.RuntimeData.CurrentRangeMultipler = 2f;
        _farEnemy.RuntimeData.CurrentRangeIncrement = 0.5f;

        AssertSelected(ChessTargetSelector.TargetSelectMode.LongestRange, _farEnemy);
    }

    /// <summary>
    /// 验证运行时攻击力乘区和增量会影响最高攻击索敌。
    /// </summary>
    [Test]
    public void HighestAtkUsesRuntimeAtk()
    {
        _closeEnemy.BaseData.Atk = 10;
        _farEnemy.BaseData.Atk = 10;
        _closeEnemy.RuntimeData.CurrentAtkMultipler = 1f;
        _closeEnemy.RuntimeData.CurrentAtkIncrement = 0;
        _farEnemy.RuntimeData.CurrentAtkMultipler = 2f;
        _farEnemy.RuntimeData.CurrentAtkIncrement = 3;

        AssertSelected(ChessTargetSelector.TargetSelectMode.HighestAtk, _farEnemy);
    }

    /// <summary>
    /// 验证没有敌人时目标为空。
    /// </summary>
    [Test]
    public void SelectClearsTargetWhenNoEnemies()
    {
        CampManager emptyCamps = CreateManager();
        ChessController lonely = emptyCamps.RegisterChess(1, 1, Vector2.zero);

        Assert.That(lonely.TargetSelector.Select(ChessTargetSelector.TargetSelectMode.Nearest), Is.True);
        Assert.That(lonely.TargetSelector.SelectedChessController, Is.Null);
    }

    /// <summary>
    /// 验证未加入阵营时索敌不抛异常且目标为空。
    /// </summary>
    [Test]
    public void SelectDoesNotThrowWithoutCamp()
    {
        ChessController chess = new ChessController(CreateConfig());
        SetSelected(chess, chess);

        bool selected = false;
        Assert.DoesNotThrow(
            () => selected = chess.TargetSelector.Select(ChessTargetSelector.TargetSelectMode.Nearest));
        Assert.That(selected, Is.True);
        Assert.That(chess.TargetSelector.SelectedChessController, Is.Null);
    }

    /// <summary>
    /// 验证非法枚举保持原目标并返回 false。
    /// </summary>
    [Test]
    public void InvalidModeKeepsPreviousTarget()
    {
        Assert.That(_owner.TargetSelector.Select(ChessTargetSelector.TargetSelectMode.Nearest), Is.True);
        Assert.That(_owner.TargetSelector.SelectedChessController, Is.SameAs(_closeEnemy));

        Assert.That(
            _owner.TargetSelector.Select((ChessTargetSelector.TargetSelectMode)99),
            Is.False);
        Assert.That(_owner.TargetSelector.SelectedChessController, Is.SameAs(_closeEnemy));
    }

    /// <summary>
    /// 验证近处尸体加远处活人时，索敌会跳过尸体并选中活人。
    /// </summary>
    [Test]
    public void SelectSkipsDeadEnemyAndPicksLivingEnemy()
    {
        _closeEnemy.RuntimeData.Hp = 0;
        _closeEnemy.Fsm.CheckCurrentStateTransition();

        AssertSelected(ChessTargetSelector.TargetSelectMode.Nearest, _farEnemy);
        AssertSelected(ChessTargetSelector.TargetSelectMode.LowestHp, _farEnemy);
        AssertSelected(ChessTargetSelector.TargetSelectMode.Farthest, _farEnemy);
    }

    /// <summary>
    /// 验证只剩死亡敌人时索敌成功但当前目标为空。
    /// </summary>
    [Test]
    public void SelectClearsTargetWhenOnlyDeadEnemiesRemain()
    {
        _closeEnemy.RuntimeData.Hp = 0;
        _farEnemy.RuntimeData.Hp = 0;
        _closeEnemy.Fsm.CheckCurrentStateTransition();
        _farEnemy.Fsm.CheckCurrentStateTransition();

        Assert.That(_owner.TargetSelector.Select(ChessTargetSelector.TargetSelectMode.Nearest), Is.True);
        Assert.That(_owner.TargetSelector.SelectedChessController, Is.Null);
    }

    /// <summary>
    /// 断言指定索敌方式选中期望敌人。
    /// </summary>
    private void AssertSelected(ChessTargetSelector.TargetSelectMode mode, ChessController expected)
    {
        Assert.That(_owner.TargetSelector.Select(mode), Is.True);
        Assert.That(_owner.TargetSelector.SelectedChessController, Is.SameAs(expected));
    }

    /// <summary>
    /// 创建已注册默认配置的阵营管理器。
    /// </summary>
    private static CampManager CreateManager()
    {
        var configs = new ChessConfigRepository();
        configs.Register(CreateConfig());
        return new CampManager(configs);
    }

    /// <summary>
    /// 创建测试用棋子配置。
    /// </summary>
    private static ChessBaseData CreateConfig()
    {
        return new ChessBaseData
        {
            ConfigId = 1,
            Name = "SelectorTest",
            MaxHp = 100,
            Atk = 1,
            Range = 1f,
            MoveSpeed = 1f,
            AtkSpeed = 1f,
            WindUpTime = 0f,
            BulletSpeed = 0f
        };
    }

    /// <summary>
    /// 测试侧写入当前索敌目标。
    /// </summary>
    private static void SetSelected(ChessController owner, ChessController target)
    {
        PropertyInfo property = typeof(ChessTargetSelector).GetProperty(
            nameof(ChessTargetSelector.SelectedChessController));
        property.GetSetMethod(true).Invoke(owner.TargetSelector, new object[] { target });
    }
}
#endif
