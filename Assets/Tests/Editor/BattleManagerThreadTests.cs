#if UNITY_EDITOR
using System;
using System.Threading;
using AutoChess;
using AutoChess.Navigation;
using NUnit.Framework;

/// <summary>验证 BattleManager 的固定步线程生命周期和快照交换。</summary>
public class BattleManagerThreadTests
{
    /// <summary>验证 BattleManager 可以启动、接受步进信号并在有限时间内停止。</summary>
    [Test]
    public void BattleThreadStartsAdvancesAndStops()
    {
        using (var manager = new BattleManager())
        {
            manager.Start();
            Assert.That(manager.IsRunning, Is.True);
            manager.RequestAdvance(1);

            bool received = false;
            for (int i = 0; i < 50; i++)
            {
                if (manager.TryDequeueNavigationInput(out NavigationInputSnapshot snapshot)
                    && snapshot.IsStepComplete)
                {
                    received = true;
                    break;
                }

                Thread.Sleep(20);
            }

            Assert.That(received, Is.True);
            Assert.That(manager.Stop(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(manager.IsRunning, Is.False);
        }
    }

    /// <summary>验证重复启动会被拒绝。</summary>
    [Test]
    public void BattleThreadCannotStartTwice()
    {
        using (var manager = new BattleManager())
        {
            manager.Start();
            Assert.Throws<InvalidOperationException>(() => manager.Start());
        }
    }
}
#endif
