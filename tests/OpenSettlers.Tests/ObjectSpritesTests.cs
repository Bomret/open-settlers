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

    // Emulator: 0x44db26 hands 0x44da4d the object number 0x80 for a flag (docs/rules/flags.md); 0x44da4d adds 1250.
    [TestMethod]
    public void AFlagTakesTheEntryOfObjectNumber0x80AndTheOtherBuildingCodesHaveNoSprite()
    {
        Assert.AreEqual(1378, ObjectSprites.Entry(1));
        Assert.AreEqual(ObjectSprites.FlagEntry, ObjectSprites.Entry(ObjectSprites.FlagCode));
        Assert.IsNull(ObjectSprites.Entry(2));
        Assert.IsNull(ObjectSprites.Entry(4));
    }

    [TestMethod]
    public void AStaticCodeTakesTheEntryOfItsCodeMinusEightPlus1250()
    {
        Assert.AreEqual(1274, ObjectSprites.Entry(32));
        Assert.AreEqual(1369, ObjectSprites.Entry(127));
    }

    [TestMethod]
    public void CodesEightToFifteenShareTheFirstFrameOfTheirEightFrameAnimation()
    {
        Assert.AreEqual(1250, ObjectSprites.Entry(8));
        Assert.AreEqual(1250, ObjectSprites.Entry(15));
    }

    [TestMethod]
    public void CodesSixteenToTwentyThreeTakeTheFirstFrameOfTheNextEightEntries()
    {
        Assert.AreEqual(1258, ObjectSprites.Entry(16));
        Assert.AreEqual(1258, ObjectSprites.Entry(23));
    }

    [TestMethod]
    public void CodesTwentyFourToThirtyOneTakeTheSecondFrameOfTheirGroupOfFour()
    {
        Assert.AreEqual(1267, ObjectSprites.Entry(24));
        Assert.AreEqual(1267, ObjectSprites.Entry(27));
        Assert.AreEqual(1271, ObjectSprites.Entry(28));
    }
}
