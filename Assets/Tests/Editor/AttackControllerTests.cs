#if UNITY_EDITOR
using System.Reflection;
using Chess;
using Chess.FSM.State;
using Config;
using NUnit.Framework;
using UnityEngine;

public class AttackControllerTests
{
    private ChessController _attacker;
    private ChessController _target;

    [SetUp]
    public void SetUp()
    {
        _attacker = new ChessController(CreateConfig(100, 12, 5, 2, 0.5f));
        _target = new ChessController(CreateConfig(100, 1, 5, 1, 0f));
        _attacker.RuntimeData.CurrentPosition = Vector2.zero;
        _target.RuntimeData.CurrentPosition = new Vector2(3f, 0f);
        SelectTarget(_attacker, _target);
    }

    /// <summary>
    /// 验证带配置构造的棋子初始化运行时战斗数据并暴露攻击相关控制器。
    /// </summary>
    [Test]
    public void ConfiguredChessInitializesAttackDataAndControllers()
    {
        Assert.That(_attacker.RuntimeData.Hp, Is.EqualTo(100));
        Assert.That(_attacker.RuntimeData.GetCurrentAtk(_attacker.BaseData.Atk), Is.EqualTo(12));
        Assert.That(_attacker.RuntimeData.GetCurrentRange(_attacker.BaseData.Range), Is.EqualTo(5));
        Assert.That(_attacker.TargetSelector, Is.Not.Null);
        Assert.That(_attacker.AttackController, Is.Not.Null);
        Assert.That(_attacker.TargetSelector.SelectedChessController, Is.SameAs(_target));
    }

    /// <summary>
    /// 验证首次满足条件时立即进入前摇，前摇结束后才造成配置伤害。
    /// </summary>
    [Test]
    public void FirstAttackWaitsForWindUpThenDealsConfiguredDamage()
    {
        _attacker.Tick(0f);

        Assert.That(_attacker.Fsm.CurrentState, Is.TypeOf<AttackState>());
        Assert.That(_attacker.AttackController.IsWindingUp, Is.True);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));

        _attacker.Tick(0.49f);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));

        _attacker.Tick(0.01f);

        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(88));
        Assert.That(_attacker.AttackController.IsWindingUp, Is.False);
        Assert.That(_attacker.AttackController.CooldownRemaining, Is.EqualTo(0.5f).Within(0.0001f));
    }

    /// <summary>
    /// 验证退出攻击状态会取消前摇，但不会清除已经产生的攻击冷却。
    /// </summary>
    [Test]
    public void LeavingAttackStateStopsWindUpAndPreservesCooldown()
    {
        _attacker.Tick(0.5f);
        Assert.That(_attacker.AttackController.CooldownRemaining, Is.EqualTo(0.5f).Within(0.0001f));

        _attacker.Fsm.ChangeState<MovementState>();

        Assert.That(_attacker.AttackController.IsWindingUp, Is.False);
        Assert.That(_attacker.AttackController.CooldownRemaining, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(_attacker.Fsm.CurrentState, Is.TypeOf<MovementState>());
    }

    /// <summary>
    /// 验证攻击状态下受到致命伤害会立即进入死亡状态并停止攻击。
    /// </summary>
    [Test]
    public void LethalDamageDuringAttackEntersDeadState()
    {
        _attacker.Tick(0f);
        Assert.That(_attacker.Fsm.CurrentState, Is.TypeOf<AttackState>());

        _attacker.DamageReceiver.ApplyDamage(100);

        Assert.That(_attacker.Fsm.CurrentState, Is.TypeOf<DeadState>());
        Assert.That(_attacker.AttackController.IsWindingUp, Is.False);
    }

    /// <summary>
    /// 验证命中后必须等待完整攻击间隔才能开始下一次前摇。
    /// </summary>
    [Test]
    public void AttackCooldownStartsAfterHit()
    {
        _attacker.Tick(0.5f);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(88));

        _attacker.Tick(0.49f);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(88));
        Assert.That(_attacker.AttackController.IsWindingUp, Is.False);

        _attacker.Tick(0.01f);
        Assert.That(_attacker.AttackController.IsWindingUp, Is.True);

        _attacker.Tick(0.5f);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(76));
    }

    /// <summary>
    /// 验证前摇期间目标失去射程后会取消本次攻击。
    /// </summary>
    [Test]
    public void TargetLeavingRangeDuringWindUpCancelsAttack()
    {
        _attacker.AttackController.Tick(0f);
        _target.RuntimeData.CurrentPosition = new Vector2(6f, 0f);

        _attacker.AttackController.Tick(0.5f);

        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));
        Assert.That(_attacker.AttackController.IsWindingUp, Is.False);
        Assert.That(_attacker.AttackController.CooldownRemaining, Is.EqualTo(0f));
    }

    /// <summary>
    /// 验证前摇期间目标被替换后不会误伤原目标。
    /// </summary>
    [Test]
    public void ReplacedTargetDuringWindUpCancelsAttack()
    {
        var replacement = new ChessController(CreateConfig(100, 1, 5, 1, 0f));
        replacement.RuntimeData.CurrentPosition = new Vector2(3f, 1f);

        _attacker.AttackController.Tick(0f);
        SelectTarget(_attacker, replacement);
        _attacker.AttackController.Tick(0.5f);

        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));
        Assert.That(replacement.RuntimeData.Hp, Is.EqualTo(100));
    }

    /// <summary>
    /// 验证零前摇攻击会在当前时间步立即造成伤害。
    /// </summary>
    [Test]
    public void ZeroWindUpDealsDamageImmediately()
    {
        _attacker = new ChessController(CreateConfig(100, 12, 5, 2, 0f));
        _attacker.RuntimeData.CurrentPosition = Vector2.zero;
        SelectTarget(_attacker, _target);

        _attacker.Tick(0f);

        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(88));
    }

    /// <summary>
    /// 验证无目标、超出射程、自身目标和死亡目标均不会造成伤害。
    /// </summary>
    [Test]
    public void InvalidTargetsDoNotReceiveDamage()
    {
        SelectTarget(_attacker, null);
        _attacker.AttackController.Tick(1f);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));

        SelectTarget(_attacker, _target);
        _target.RuntimeData.CurrentPosition = new Vector2(6f, 0f);
        _attacker.AttackController.Tick(1f);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));

        SelectTarget(_attacker, _attacker);
        _attacker.AttackController.Tick(1f);
        Assert.That(_attacker.RuntimeData.Hp, Is.EqualTo(100));

        SelectTarget(_attacker, _target);
        _target.RuntimeData.Hp = 0;
        _target.Fsm.ChangeState<DeadState>();
        _attacker.AttackController.Tick(1f);
        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(0));
    }

    /// <summary>
    /// 验证任一负数攻击参数或非正攻击速度都会使棋子无法攻击。
    /// </summary>
    [TestCase(-1, 5, 0.5f)]
    [TestCase(2, -1, 0.5f)]
    [TestCase(2, 5, -1f)]
    [TestCase(0, 5, 0.5f)]
    public void InvalidAttackConfigurationDisablesAttack(int atkSpeed, int range, float windUpTime)
    {
        _attacker = new ChessController(CreateConfig(100, 12, range, atkSpeed, windUpTime));
        _attacker.RuntimeData.CurrentPosition = Vector2.zero;
        SelectTarget(_attacker, _target);

        _attacker.AttackController.Tick(2f);

        Assert.That(_target.RuntimeData.Hp, Is.EqualTo(100));
        Assert.That(_attacker.AttackController.IsWindingUp, Is.False);
    }

    /// <summary>
    /// 验证无参 ChessController.Tick 会转发 Unity 帧时间并安全驱动攻击控制器。
    /// </summary>
    [Test]
    public void ParameterlessChessTickDrivesAttackSafely()
    {
        _attacker = new ChessController(CreateConfig(100, 12, 5, 2, 0f));
        _attacker.RuntimeData.CurrentPosition = Vector2.zero;
        SelectTarget(_attacker, _target);

        Assert.DoesNotThrow(_attacker.Tick);
    }

    private static ChessBaseData CreateConfig(int maxHp, int atk, int range, int atkSpeed, float windUpTime)
    {
        return new ChessBaseData
        {
            MaxHp = maxHp,
            Atk = atk,
            Range = range,
            AtkSpeed = atkSpeed,
            WindUpTime = windUpTime
        };
    }

    /// <summary>
    /// 测试侧设置当前索敌目标，保持生产目标选择器不开放手动写入接口。
    /// </summary>
    private static void SelectTarget(ChessController owner, ChessController target)
    {
        PropertyInfo property = typeof(ChessTargetSelector).GetProperty(nameof(ChessTargetSelector.SelectedChessController));
        property.GetSetMethod(true).Invoke(owner.TargetSelector, new object[] { target });
    }
}
#endif
