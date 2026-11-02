using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectSpritesTests
{
    [TestMethod]
    public void ACodeBelowEightAndACodeAbove127HaveNoSprite()
    {
        Assert.IsNull(ObjectSprites.Entry(0));
        Assert.IsNull(ObjectSprites.Entry(7));
        Assert.IsNull(ObjectSprites.Entry(128));
    }

    [TestMethod]
    public void AStaticCodeTakesTheEntryOfItsCodeMinusEightPlus1250()
    {
        Assert.AreEqual(1274, ObjectSprites.Entry(32));
        Assert.AreEqual(1369, ObjectSprites.Entry(127));
    }

    [TestMethod]
    public void CodesEightToFifteenShareOneEightFrameAnimationThatTheCounterAdvances()
    {
        Assert.AreEqual(1250, ObjectSprites.Entry(8, 0));
        Assert.AreEqual(1250, ObjectSprites.Entry(15, 0));
        Assert.AreEqual(1251, ObjectSprites.Entry(8, 16));
        Assert.AreEqual(1250, ObjectSprites.Entry(8, 128));
    }

    [TestMethod]
    public void TheLowBitsOfACodeOffsetItsPhase()
    {
        // The counter plus the code's index, shifted right by 4: code 15 (index 7) turns over 9 counts before code 8.
        Assert.AreEqual(1251, ObjectSprites.Entry(15, 9));
        Assert.AreEqual(1250, ObjectSprites.Entry(8, 9));
    }

    [TestMethod]
    public void CodesSixteenToTwentyThreeAnimateTheNextEightEntries()
    {
        Assert.AreEqual(1258, ObjectSprites.Entry(16, 0));
        Assert.AreEqual(1259, ObjectSprites.Entry(16, 16));
    }

    [TestMethod]
    public void CodesTwentyFourToThirtyOneAnimateTwoGroupsOfFourEntries()
    {
        Assert.AreEqual(1267, ObjectSprites.Entry(24, 0));
        Assert.AreEqual(1266, ObjectSprites.Entry(24, 48));
        Assert.AreEqual(1271, ObjectSprites.Entry(28, 0));
        Assert.AreEqual(1270, ObjectSprites.Entry(28, 48));
    }
}
