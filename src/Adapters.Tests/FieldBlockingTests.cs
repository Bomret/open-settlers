using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// What a field blocks (docs/rules/farming.md#what-a-field-blocks): the class the program's table (0x41e640) gives each field code, and the
// refusals the existing class reads give a node of that class. The growing codes 0x69 to 0x6e and the ripe codes 0x79 to 0x7e have class 2,
// the class of a stone; the harvested field 0x6f has class 0. No emulator verb places a field, so the refusals are the engine's own
// reads of the install's classes, not emulator verdicts.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class FieldBlockingTests
{
    const int StoneClass = 2;
    const int EmptyClass = 0;

    [TestMethod]
    [DataRow(0x69)]
    [DataRow(0x6a)]
    [DataRow(0x6e)]
    [DataRow(0x79)]
    [DataRow(0x7b)]
    [DataRow(0x7e)]
    public void TheInstallGivesAGrowingOrRipeFieldTheClassOfAStone(int code)
    {
        var objectClass = TestProgram.Tables().ClassOf(new MapObject { Code = (byte)code });

        Assert.AreEqual(StoneClass, objectClass.Value);
    }

    [TestMethod]
    public void TheInstallGivesTheHarvestedFieldAndTheSignsNoClass()
    {
        var tables = TestProgram.Tables();

        Assert.AreEqual(EmptyClass, tables.ClassOf(new MapObject { Code = MapObject.HarvestedFieldCode }).Value);
        Assert.AreEqual(EmptyClass, tables.ClassOf(new MapObject { Code = 0x70 }).Value);
        Assert.AreEqual(EmptyClass, tables.ClassOf(new MapObject { Code = 0x78 }).Value);
    }

    [TestMethod]
    [DataRow(MapObject.SownFieldCode)]
    [DataRow(MapObject.GrownFieldCode)]
    [DataRow(MapObject.RipeFieldCode)]
    [DataRow(MapObject.LastRipeFieldCode)]
    public void ARoadStepOntoAFieldIsRefused(int code)
    {
        var map = OwnedLandWithFlags();
        map.At(11, 10).Object = new MapObject { Code = (byte)code };

        var status = RoadFromTheFlagEast(map);

        Assert.AreEqual(RoadStatus.Refused, status);
    }

    [TestMethod]
    public void ARoadStepOntoAHarvestedFieldCompletesAtAFlag()
    {
        var map = OwnedLandWithFlags();
        map.At(11, 10).Object = new MapObject { Code = MapObject.HarvestedFieldCode };

        var status = RoadFromTheFlagEast(map);

        Assert.AreEqual(RoadStatus.Complete, status);
    }

    [TestMethod]
    [DataRow(MapObject.SownFieldCode)]
    [DataRow(MapObject.GrownFieldCode)]
    [DataRow(MapObject.RipeFieldCode)]
    [DataRow(MapObject.LastRipeFieldCode)]
    public void NeitherAFlagNorABuildingMayStandOnAField(int code)
    {
        var map = OwnedLandWithFlags();
        map.At(20, 20).Object = new MapObject { Code = (byte)code };

        var site = BuildSite.Evaluate(TestProgram.Tables(), map, 20, 20, 0, hasCastle: true);

        Assert.AreEqual(0, site.Site);
        Assert.IsFalse(site.AllowsFlag);
    }

    [TestMethod]
    public void AFlagAndABuildingMayStandOnAHarvestedFieldAsOnAnEmptyNode()
    {
        var map = OwnedLandWithFlags();
        map.At(20, 20).Object = new MapObject { Code = MapObject.HarvestedFieldCode };

        var site = BuildSite.Evaluate(TestProgram.Tables(), map, 20, 20, 0, hasCastle: true);

        Assert.AreNotEqual(0, site.Site);
        Assert.IsTrue(site.AllowsFlag);
    }

    // The click's judgment of the road drawn from the flag at 10,10 two steps east, through the road planner: it judges a road
    // only once a castle stands, so a castle stands at 1,1.
    static RoadStatus RoadFromTheFlagEast(Map map)
    {
        var tables = TestProgram.Tables();
        var castle = new CastleStock(tables.Catalog);
        castle.Place(
            new Castle
            {
                X = 1,
                Y = 1,
                Height = 1,
            },
            new System.Collections.Generic.Dictionary<Good, int>()
        );

        return new RoadPlanner(tables, map, castle).Check(10, 10, [0, 0]);
    }

    // A map whose every node is player 0's and land, with flags (object code 1) at 10,10 and 12,10.
    static Map OwnedLandWithFlags()
    {
        var map = new Map(MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Owner = Owner.Player(0);
            map.At(index).Upper = Terrain.Lowland;
            map.At(index).Lower = Terrain.Lowland;
        }

        map.At(10, 10).Object = MapObject.Flag;
        map.At(12, 10).Object = MapObject.Flag;

        return map;
    }
}
