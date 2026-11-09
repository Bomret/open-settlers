using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectSpritesTests
{
    [TestMethod]
    public void ACodeBelowEightAndACodeAbove127HaveNoSprite()
    {
        Assert.IsNull(ObjectSprites.Entry(new MapObject(0)));
        Assert.IsNull(ObjectSprites.Entry(new MapObject(7)));
        Assert.IsNull(ObjectSprites.Entry(new MapObject(128)));
    }

    // Emulator: 0x44db26 hands 0x44da4d the object number 0x80 for a flag (docs/rules/flags.md); 0x44da4d adds 1250.
    [TestMethod]
    public void AFlagTakesTheEntryOfObjectNumber0x80AndTheOtherBuildingCodesHaveNoSprite()
    {
        Assert.AreEqual(1378, ObjectSprites.Entry(new MapObject(1)));
        Assert.AreEqual(ObjectSprites.FlagEntry, ObjectSprites.Entry(MapObject.Flag));
        Assert.IsNull(ObjectSprites.Entry(new MapObject(2)));
        Assert.IsNull(ObjectSprites.Entry(new MapObject(4)));
    }

    // 0x44db26 adds the state's +0x206 shifted right 3, two bits, to the object number 0x80.
    [TestMethod]
    public void AFlagWavesThroughFourFramesOneEveryEightClockUnits()
    {
        Assert.AreEqual(1378, ObjectSprites.Entry(MapObject.Flag, 7));
        Assert.AreEqual(1379, ObjectSprites.Entry(MapObject.Flag, 8));
        Assert.AreEqual(1381, ObjectSprites.Entry(MapObject.Flag, 31));
        Assert.AreEqual(1378, ObjectSprites.Entry(MapObject.Flag, 32));
    }

    [TestMethod]
    public void AStaticCodeTakesTheEntryOfItsCodeMinusEightPlus1250()
    {
        Assert.AreEqual(1274, ObjectSprites.Entry(new MapObject(32)));
        Assert.AreEqual(1369, ObjectSprites.Entry(new MapObject(127)));
    }

    [TestMethod]
    public void CodesEightToFifteenShareTheFirstFrameOfTheirEightFrameAnimation()
    {
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject(8)));
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject(15)));
    }

    [TestMethod]
    public void CodesSixteenToTwentyThreeTakeTheFirstFrameOfTheNextEightEntries()
    {
        Assert.AreEqual(1258, ObjectSprites.Entry(new MapObject(16)));
        Assert.AreEqual(1258, ObjectSprites.Entry(new MapObject(23)));
    }

    [TestMethod]
    public void CodesTwentyFourToThirtyOneTakeTheSecondFrameOfTheirGroupOfFour()
    {
        Assert.AreEqual(1267, ObjectSprites.Entry(new MapObject(24)));
        Assert.AreEqual(1267, ObjectSprites.Entry(new MapObject(27)));
        Assert.AreEqual(1271, ObjectSprites.Entry(new MapObject(28)));
    }

    // docs/rules/map-objects.md: the phase is (counter + index) >> 4 at 0x44d5b4.
    [TestMethod]
    public void AnEightFrameCodeStepsAFrameEverySixteenCounterUnitsAndWrapsAfterEight()
    {
        Assert.AreEqual(1251, ObjectSprites.Entry(new MapObject(8), 16));
        Assert.AreEqual(1257, ObjectSprites.Entry(new MapObject(8), 112));
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject(8), 128));
    }

    [TestMethod]
    public void TheLowBitsOfTheCodeOffsetThePhase()
    {
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject(9), 14));
        Assert.AreEqual(1251, ObjectSprites.Entry(new MapObject(9), 15));
    }

    [TestMethod]
    public void AFourFrameCodeWrapsAfterFourFramesAndAStillCodeIgnoresTheCounter()
    {
        Assert.AreEqual(1269, ObjectSprites.Entry(new MapObject(24), 32));
        Assert.AreEqual(1266, ObjectSprites.Entry(new MapObject(24), 48));
        Assert.AreEqual(1274, ObjectSprites.Entry(new MapObject(32), 200));
    }

    // 0x44ff59: ((((tile * 4) & 0x3c) * 2 ^ 0xa8) + counter) >> 3 & 15, plus 630.
    [TestMethod]
    public void AWaterTileStepsOneOfSixteenFramesEveryEightCounterUnits()
    {
        Assert.AreEqual(630 + 5, ObjectSprites.WaterEntry(0, 0));
        Assert.AreEqual(630 + 6, ObjectSprites.WaterEntry(0, 8));
        Assert.AreEqual(630 + 5, ObjectSprites.WaterEntry(0, 128));
    }

    [TestMethod]
    public void TheTileIndexOffsetsTheWaterPhaseAndRepeatsEverySixteenTiles()
    {
        Assert.AreEqual(630 + 4, ObjectSprites.WaterEntry(1, 0));
        Assert.AreEqual(ObjectSprites.WaterEntry(1, 40), ObjectSprites.WaterEntry(17, 40));
    }
}
