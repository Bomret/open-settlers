using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The road cursor's verdict of each direction at the end of a road being drawn (docs/rules/roads.md, "The allowed
// directions" and "The road markers"), on a made-up rule table (no install): the flag is class 3, the small building
// (code 2) class 4, the stones and rocks class 2, and the trees class 1, as the original's class table gives them.
[TestClass]
public sealed class RoadCursorTests
{
    const byte TreeCode = 0x08;
    const byte StoneCode = 0x48;
    const byte RockCode = 0x50;
    const byte SmallBuildingCode = 0x02;

    static readonly RoadCursorVerdict[] AllBlocked = [.. Enumerable.Repeat(RoadCursorVerdict.Blocked, 6)];

    static RuleTables Made()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[SmallBuildingCode] = 4;
        classes[TreeCode] = 1;
        classes[StoneCode] = 2;
        classes[RockCode] = 2;

        return new RuleTables(classes, TestProgram.MadeStartStock(), catalog: TestCatalog.StandIn());
    }

    // A map whose every node is player 0's and land, with a flag at 10,10.
    static Map OwnedLandWithAFlag()
    {
        var map = new Map(MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Owner = Owner.Player(0);
            map.At(index).Upper = Terrain.Lowland;
            map.At(index).Lower = Terrain.Lowland;
        }

        map.At(10, 10).Object = MapObject.Flag;

        return map;
    }

    static RoadCursorVerdict East(Map map, params int[] directions) =>
        RoadBuilding.Verdicts(Made(), map, 10, 10, directions)[0];

    [TestMethod]
    public void AtTheStartFlagEveryNeighborOfTheOpenLandIsOpen()
    {
        var verdicts = RoadBuilding.Verdicts(Made(), OwnedLandWithAFlag(), 10, 10, []);

        CollectionAssert.AreEqual(Enumerable.Repeat(RoadCursorVerdict.Open, 6).ToArray(), verdicts.ToArray());
    }

    [TestMethod]
    public void ANodeOfAnotherOwnerIsBlocked()
    {
        var map = OwnedLandWithAFlag();
        map.At(11, 10).Owner = Owner.Player(1);

        Assert.AreEqual(RoadCursorVerdict.Blocked, East(map));
    }

    [TestMethod]
    [DataRow(StoneCode)]
    [DataRow(RockCode)]
    [DataRow(SmallBuildingCode)]
    public void AStoneARockOrASmallBuildingIsBlocked(byte code)
    {
        var map = OwnedLandWithAFlag();
        map.At(11, 10).Object = new MapObject { Code = code };

        Assert.AreEqual(RoadCursorVerdict.Blocked, East(map));
    }

    [TestMethod]
    public void ATreeOrAFlagIsOpen()
    {
        var tree = OwnedLandWithAFlag();
        tree.At(11, 10).Object = new MapObject { Code = TreeCode };
        var flag = OwnedLandWithAFlag();
        flag.At(11, 10).Object = MapObject.Flag;

        Assert.AreEqual(RoadCursorVerdict.Open, East(tree));
        Assert.AreEqual(RoadCursorVerdict.Open, East(flag));
    }

    // A road node with no flag is open to the cursor where a flag could stand on it, and the click still refuses the step
    // onto it; where a flag stands next to it, no flag could, and the cursor blocks it.
    [TestMethod]
    public void ARoadNodeIsOpenWhereAFlagCouldStandAndBlockedWhereNone()
    {
        var site = OwnedLandWithAFlag();
        site.At(13, 10).Roads = RoadSet.Of(1).With(4);
        var none = OwnedLandWithAFlag();
        none.At(13, 10).Roads = RoadSet.Of(1).With(4);
        none.At(14, 10).Object = MapObject.Flag;

        Assert.AreEqual(RoadCursorVerdict.Open, East(site, 0, 0));
        Assert.AreEqual(RoadStatus.Refused, RoadBuilding.Walk(Made(), site, 10, 10, [0, 0, 0], keep: false).Status);
        Assert.AreEqual(RoadCursorVerdict.Blocked, East(none, 0, 0));
    }

    // The road 10,10 to 11,10 to 12,11 to 11,11 ends next to its own node 11,10, which holds the road's bits and stands next
    // to the start flag, so no flag could stand there: blocked. The node it came from, 12,11, is the way back.
    [TestMethod]
    public void TheRoadsOwnEarlierNodeIsBlockedAndTheNodeItCameFromIsTheWayBack()
    {
        var verdicts = RoadBuilding.Verdicts(Made(), OwnedLandWithAFlag(), 10, 10, [0, 1, 3]);

        Assert.AreEqual(RoadCursorVerdict.Blocked, verdicts[5], "north, 11,10");
        Assert.AreEqual(RoadCursorVerdict.WayBack, verdicts[0], "east, 12,11");
        Assert.AreEqual(1, verdicts.Count(verdict => verdict == RoadCursorVerdict.WayBack));
    }

    [TestMethod]
    public void AtTheStartFlagNoDirectionIsTheWayBackEvenAlongARoadThatLeavesIt()
    {
        var map = OwnedLandWithAFlag();
        map.At(10, 10).Roads = RoadSet.Of(0);
        map.At(11, 10).Roads = RoadSet.Of(3).With(0);

        var verdicts = RoadBuilding.Verdicts(Made(), map, 10, 10, []);

        Assert.AreEqual(RoadCursorVerdict.Blocked, verdicts[0]);
        Assert.IsFalse(verdicts.Contains(RoadCursorVerdict.WayBack));
    }

    [TestMethod]
    public void AfterAStepTheNodeTheRoadCameFromIsTheWayBack()
    {
        var verdicts = RoadBuilding.Verdicts(Made(), OwnedLandWithAFlag(), 10, 10, [0]);

        Assert.AreEqual(RoadCursorVerdict.WayBack, verdicts[3]);
        Assert.AreEqual(1, verdicts.Count(verdict => verdict == RoadCursorVerdict.WayBack));
    }

    [TestMethod]
    public void ANeighborAcrossTheMapsEdgeIsJudgedAsTheNodeItWrapsTo()
    {
        var map = OwnedLandWithAFlag();
        var last = map.Size.Width - 1;
        map.At(0, 10).Object = MapObject.Flag;
        map.At(last, 10).Owner = Owner.Player(1);
        map.At(last, 9).Object = new MapObject { Code = StoneCode };

        var verdicts = RoadBuilding.Verdicts(Made(), map, 0, 10, []);

        Assert.AreEqual(RoadCursorVerdict.Blocked, verdicts[3], "west, wrapping to the last column");
        Assert.AreEqual(RoadCursorVerdict.Blocked, verdicts[4], "north-west, wrapping to the last column");
        Assert.AreEqual(RoadCursorVerdict.Open, verdicts[0]);
    }

    [TestMethod]
    public void JudgingTheVerdictsLeavesTheMapAndTheGameUnchanged()
    {
        var game = RoadsTests.TwoFlags();
        var digest = game.Digest();
        var roads = Enumerable.Range(0, game.Map.Count).Select(index => game.Map.At(index).Roads).ToArray();

        game.RoadPlanner.Verdicts(16, 6, [0]);

        Assert.AreEqual(digest, game.Digest());
        CollectionAssert.AreEqual(
            roads,
            Enumerable.Range(0, game.Map.Count).Select(index => game.Map.At(index).Roads).ToArray()
        );
    }

    [TestMethod]
    public void BeforeTheCastleStandsEveryDirectionIsBlocked()
    {
        var map = OwnedLandWithAFlag();
        var game = GameFactory.Create(Made(), map);

        CollectionAssert.AreEqual(AllBlocked, game.RoadPlanner.Verdicts(10, 10, []).ToArray());
    }
}
