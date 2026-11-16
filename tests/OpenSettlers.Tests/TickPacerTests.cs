using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TickPacerTests
{
    [TestMethod]
    public void AFrameShorterThanATickRunsNoTickAndTheFractionCarries()
    {
        var pacer = new TickPacer();

        Assert.AreEqual(0, pacer.Due(0.05));
        Assert.AreEqual(1, pacer.Due(0.05), "two frames of 50 ms make 1.25 ticks at 12.5 ticks a second");
    }

    [TestMethod]
    public void ASecondOfFramesRunsTwelveAndAHalfTicksWhateverTheFrameRate()
    {
        var pacer = new TickPacer();
        var ticks = 0;

        for (var frame = 0; frame < 100; frame++)
        {
            ticks += pacer.Due(0.01);
        }

        Assert.AreEqual(12, ticks);
        Assert.AreEqual(1, pacer.Due(0.05), "the half tick that carried and 0.625 more make a tick");
    }

    [TestMethod]
    public void AFrameWithinTheBoundRunsEveryTickItHoldsAtOnce()
    {
        Assert.AreEqual(3, new TickPacer().Due(0.3), "0.3 s holds 3.75 ticks, below the bound of 4");
    }

    [TestMethod]
    public void ResetDropsTheCarry()
    {
        var pacer = new TickPacer();
        pacer.Due(0.07);

        pacer.Reset();

        Assert.AreEqual(0, pacer.Due(0.07));
    }

    [TestMethod]
    public void ResetReleasesAHeldPacerSoANewGameTicks()
    {
        var pacer = new TickPacer();
        pacer.Hold();

        pacer.Reset();

        Assert.AreEqual(3, pacer.Due(0.3));
    }

    [TestMethod]
    public void TheSpeedStepsThroughTheFactorsOfTheHistoryEditionAndBackToNormal()
    {
        var pacer = new TickPacer();
        var factors = new List<int> { pacer.Speed };

        for (var press = 0; press < 4; press++)
        {
            pacer.NextSpeed();
            factors.Add(pacer.Speed);
        }

        CollectionAssert.AreEqual(new[] { 1, 2, 4, 8, 1 }, factors);
    }

    [TestMethod]
    public void TheSpeedMultipliesTheTicksAFrameRuns()
    {
        var pacer = new TickPacer();
        pacer.NextSpeed();
        pacer.NextSpeed();

        Assert.AreEqual(15, pacer.Due(0.3), "4 times 3.75 ticks");
    }

    [TestMethod]
    public void AFrameRunsAtMostFourTicksTimesTheSpeedAndDropsTheRest()
    {
        var pacer = new TickPacer();
        for (var press = 0; press < 3; press++)
        {
            pacer.NextSpeed();
        }

        Assert.AreEqual(32, pacer.Due(10), "a 10 s frame at 8x holds 1000 ticks; 4 times 8 run");
    }

    [TestMethod]
    public void ResetKeepsTheSpeedForTheNextGame()
    {
        var pacer = new TickPacer();
        pacer.NextSpeed();

        pacer.Reset();

        Assert.AreEqual(2, pacer.Speed);
    }
}
