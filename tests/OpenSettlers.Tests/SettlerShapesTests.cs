using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The names the rules read in place of the numbers of the original: a heading gives its idle frame, a job says how its settler
// arrives home (docs/rules/settlers.md).
[TestClass]
public sealed class SettlerShapesTests
{
    [TestMethod]
    public void AHeadingGivesTheIdleFrameOfACarrierThatStopsFacingIt()
    {
        Assert.AreEqual(0x70, new Heading(2).IdleFrame);
    }

    [TestMethod]
    public void AHeadingAStepTriedAgainMarkedKeepsTheIdleFrameOfTheOriginalsByteArithmetic()
    {
        Assert.AreEqual(0x68, new Heading(-6).IdleFrame);
    }

    [TestMethod]
    public void TheJobsWithAWalkHomeArrivalOfTheirOwnDoNotArriveGenerically()
    {
        foreach (var code in new[] { 5, 7, 8, 11, 14, 20, 22, 26 })
        {
            Assert.IsFalse(Job.Of(code).ArrivesHomeGenerically, $"job {code}");
        }
    }

    [TestMethod]
    public void TheCarrierAndTheOtherJobsArriveHomeGenerically()
    {
        foreach (var code in new[] { 0, 4, 21 })
        {
            Assert.IsTrue(Job.Of(code).ArrivesHomeGenerically, $"job {code}");
        }
    }
}
