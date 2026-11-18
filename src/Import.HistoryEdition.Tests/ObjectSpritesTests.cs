using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectSpritesTests
{
    // The finished building's object (the table at 0x44fd16, and the first sprite of the draw stub the jump table at 0x44e3aa reaches) and
    // the scaffold's object (the table at 0x44fcfe) of the six food types, as the emulator's peek of both tables prints them (ADR 0008):
    // peek 44fcfe 00BABABABAB9B9B9B9BAC1BAB1B8B1BBB7B5B6B0B8B3AFB4, peek 44fd16 00A7A8AEA9A3A4A5A6AAC0AB9A9C9BBCA2A0A1999D9E989F.
    [TestMethod]
    [DataRow(1, 167, 186)]
    [DataRow(0xc, 154, 177)]
    [DataRow(0xd, 156, 184)]
    [DataRow(0xe, 155, 177)]
    [DataRow(0xf, 188, 187)]
    [DataRow(0x10, 162, 183)]
    public void AFoodBuildingTakesTheFinishedAndScaffoldObjectsOfTheOriginalsTables(
        int code,
        int finished,
        int scaffold
    )
    {
        var type = TestCatalog.BuildingOf(code);

        Assert.AreEqual(1250 + finished, ObjectSprites.FinishedEntry(type));
        Assert.AreEqual(1250 + scaffold, ObjectSprites.StageEntry(type));
    }

    [TestMethod]
    public void EachFoodGoodOnAFlagTakesTheEntryOf320PlusItsNumber()
    {
        var entries = OpenSettlers.Domain.Good.Foods.Select(ObjectSprites.GoodEntry).ToArray();

        CollectionAssert.AreEqual(new int?[] { 321, 322, 323, 324, 325, 326 }, entries);
    }

    [TestMethod]
    public void EveryBuildingEntryIsOneTheSpritesAreReadFor()
    {
        foreach (var entry in new[] { 167, 154, 156, 155, 188, 162, 177, 184, 187, 183 })
        {
            CollectionAssert.Contains(ObjectSprites.BuildingEntries.ToArray(), 1250 + entry);
        }
    }

    [TestMethod]
    public void ACodeBelowEightAndACodeAbove127HaveNoSprite()
    {
        Assert.IsNull(ObjectSprites.Entry(new MapObject { Code = 0 }));
        Assert.IsNull(ObjectSprites.Entry(new MapObject { Code = 7 }));
        Assert.IsNull(ObjectSprites.Entry(new MapObject { Code = 128 }));
    }

    // Emulator: 0x44db26 hands 0x44da4d the object number 0x80 for a flag (docs/rules/flags.md); 0x44da4d adds 1250.
    [TestMethod]
    public void AFlagTakesTheEntryOfObjectNumber0x80AndTheOtherBuildingCodesHaveNoSprite()
    {
        Assert.AreEqual(1378, ObjectSprites.Entry(new MapObject { Code = 1 }));
        Assert.AreEqual(ObjectSprites.FlagEntry, ObjectSprites.Entry(MapObject.Flag));
        Assert.IsNull(ObjectSprites.Entry(new MapObject { Code = 2 }));
        Assert.IsNull(ObjectSprites.Entry(new MapObject { Code = 4 }));
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
        Assert.AreEqual(1274, ObjectSprites.Entry(new MapObject { Code = 32 }));
        Assert.AreEqual(1369, ObjectSprites.Entry(new MapObject { Code = 127 }));
    }

    [TestMethod]
    public void CodesEightToFifteenShareTheFirstFrameOfTheirEightFrameAnimation()
    {
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject { Code = 8 }));
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject { Code = 15 }));
    }

    [TestMethod]
    public void CodesSixteenToTwentyThreeTakeTheFirstFrameOfTheNextEightEntries()
    {
        Assert.AreEqual(1258, ObjectSprites.Entry(new MapObject { Code = 16 }));
        Assert.AreEqual(1258, ObjectSprites.Entry(new MapObject { Code = 23 }));
    }

    [TestMethod]
    public void CodesTwentyFourToThirtyOneTakeTheSecondFrameOfTheirGroupOfFour()
    {
        Assert.AreEqual(1267, ObjectSprites.Entry(new MapObject { Code = 24 }));
        Assert.AreEqual(1267, ObjectSprites.Entry(new MapObject { Code = 27 }));
        Assert.AreEqual(1271, ObjectSprites.Entry(new MapObject { Code = 28 }));
    }

    // docs/rules/map-objects.md: the phase is (counter + index) >> 4 at 0x44d5b4.
    [TestMethod]
    public void AnEightFrameCodeStepsAFrameEverySixteenCounterUnitsAndWrapsAfterEight()
    {
        Assert.AreEqual(1251, ObjectSprites.Entry(new MapObject { Code = 8 }, 16));
        Assert.AreEqual(1257, ObjectSprites.Entry(new MapObject { Code = 8 }, 112));
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject { Code = 8 }, 128));
    }

    [TestMethod]
    public void TheLowBitsOfTheCodeOffsetThePhase()
    {
        Assert.AreEqual(1250, ObjectSprites.Entry(new MapObject { Code = 9 }, 14));
        Assert.AreEqual(1251, ObjectSprites.Entry(new MapObject { Code = 9 }, 15));
    }

    [TestMethod]
    public void AFourFrameCodeWrapsAfterFourFramesAndAStillCodeIgnoresTheCounter()
    {
        Assert.AreEqual(1269, ObjectSprites.Entry(new MapObject { Code = 24 }, 32));
        Assert.AreEqual(1266, ObjectSprites.Entry(new MapObject { Code = 24 }, 48));
        Assert.AreEqual(1274, ObjectSprites.Entry(new MapObject { Code = 32 }, 200));
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
