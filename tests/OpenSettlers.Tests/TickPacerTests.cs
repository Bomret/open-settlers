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
    public void ALongFrameRunsEveryTickItHoldsAtOnce()
    {
        Assert.AreEqual(25, new TickPacer().Due(2));
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

        Assert.AreEqual(25, pacer.Due(2));
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

        Assert.AreEqual(100, pacer.Due(2), "4 times 25 ticks");
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
