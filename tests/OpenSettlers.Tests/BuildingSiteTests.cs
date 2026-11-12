using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The placing of a building site by the command PlaceBuilding, on made-up tables (no install): every object class is 0
// except that of the flag, code 1, which is 3; the woodcutter, stonecutter, and forester are small and the sawmill is
// large, with the needs 2/0, 2/0, 2/0, and 3/2 the made tables below hold. The values the original writes are in
// BuildingSitePlacementTests. The map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at 16,6.
[TestClass]
public sealed class BuildingSiteTests
{
    private static readonly BuildingType[] Types =
    [
        BuildingType.Woodcutter,
        BuildingType.Stonecutter,
        BuildingType.Forester,
        BuildingType.Sawmill,
    ];

    private static RuleTables Made()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var sizes = new byte[RuleTables.BuildingTypeCount];
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        foreach (var type in Types)
        {
            sizes[type.Code] = (byte)(type == BuildingType.Sawmill ? 2 : 1);
            needs[2 * type.Code] = (byte)(type == BuildingType.Sawmill ? 3 : 2);
            needs[2 * type.Code + 1] = (byte)(type == BuildingType.Sawmill ? 2 : 0);
        }

        return new RuleTables(
            classes,
            TestProgram.Made().StartStock,
            buildingSizes: sizes,
            buildingNeeds: needs,
            workers: TestProgram.MadeWorkers(),
            jobData: TestProgram.MadeJobData()
        );
    }

    private static Game World()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = GameFactory.Create(Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        return game;
    }

    private static bool Place(Game game, BuildingType? type, int x, int y)
    {
        var revision = game.Revision;
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, x, y, Building: type));
        game.Tick();

        return game.Revision != revision;
    }

    // A node of the castle's land that the evaluator gives the site value and the node state 7 (nothing on or around its
    // south-east node), the first in row-major order.
    private static (int X, int Y, BuildSiteResult Site) Find(Game game, int site)
    {
        for (var y = 0; y < game.Map.Size.Height - 1; y++)
        {
            for (var x = 0; x < game.Map.Size.Width - 1; x++)
            {
                var result = BuildSite.Evaluate(game.Tables, game.Map, x, y, 0, hasCastle: true);
                if (result.Node == 7 && result.Site == site)
                {
                    return (x, y, result);
                }
            }
        }

        throw new AssertFailedException($"the map holds no free node of the site {site}");
    }

    [TestMethod]
    [DataRow(2, 2, 0)]
    [DataRow(4, 2, 0)]
    [DataRow(9, 2, 0)]
    [DataRow(0x11, 3, 2)]
    public void EachOfTheFourTypesIsPlacedAsABuildingSiteOfItsNeeds(int code, int planks, int stones)
    {
        var type = new BuildingType(code);
        var game = World();
        var (x, y, site) = Find(game, 4);

        Assert.IsTrue(Place(game, type, x, y));

        var placed = game.Buildings[1];
        Assert.AreEqual(type, placed.Type);
        Assert.IsTrue(placed.Constructing, "a placed building is a building site, never a finished building");
        Assert.AreEqual(planks, placed.Site?.Planks);
        Assert.AreEqual(stones, placed.Site?.Stones);
        Assert.AreEqual(site.Height, placed.Site?.TargetHeight);
        Assert.IsFalse(placed.WorkerInside || placed.WorkerOnTheWay);
    }

    // The pass of the building table gives a finished building to the handler of its type (0x42cd1d), which asks for its worker; a
    // building under construction goes to the construction's own handler (0x42d55f), which asks for a builder. Both ask the castle
    // through a road to their flag: with none, the request finds no worker and sets the request-failed bit.
    [TestMethod]
    public void AWoodcutterSiteAsksForItsBuilderAsAFinishedHutAsksForItsWorker()
    {
        var game = World();
        var (x, y, _) = Find(game, 4);
        Place(game, BuildingType.Woodcutter, x, y);
        var (hutX, hutY, _) = Find(game, 4);
        Assert.IsTrue(game.PlaceFinishedBuilding(BuildingType.Woodcutter, hutX, hutY));

        for (var tick = 0; tick < 300; tick++)
        {
            game.Tick();
        }

        Assert.IsTrue(game.Buildings[2].RequestFailed, "a finished hut with no road to it asked and found no worker");
        Assert.IsTrue(game.Buildings[1].RequestFailed, "a building site with no road to it asked and found no builder");
        Assert.IsFalse(game.Buildings[1].WorkerOnTheWay);
    }

    [TestMethod]
    public void ASmallSiteStartsWithProgress1AndALargeOneWith0()
    {
        var game = World();
        var (x, y, _) = Find(game, 4);
        Place(game, BuildingType.Sawmill, x, y);
        var (smallX, smallY, _) = Find(game, 4);

        Place(game, BuildingType.Woodcutter, smallX, smallY);

        Assert.AreEqual(0, game.Buildings[1].Progress);
        Assert.AreEqual(1, game.Buildings[2].Progress);
    }

    [TestMethod]
    public void ASmallSiteTakesTheObjectCode2AndALargeSiteTheObjectCode3()
    {
        var game = World();
        var (x, y, _) = Find(game, 4);
        Place(game, BuildingType.Sawmill, x, y);
        var (smallX, smallY, _) = Find(game, 4);

        Place(game, BuildingType.Forester, smallX, smallY);

        Assert.AreEqual(3, game.Map.At(x, y).Object.Code);
        Assert.AreEqual(2, game.Map.At(smallX, smallY).Object.Code);
    }

    [TestMethod]
    public void TheSiteBringsItsFlagSouthEastOfIt()
    {
        var game = World();
        var (x, y, _) = Find(game, 4);

        Place(game, BuildingType.Stonecutter, x, y);

        var flag = game.Flags[game.Buildings[1].Flag];
        Assert.AreEqual((x + 1, y + 1), (flag.X, flag.Y));
    }

    // The node state 5 is a flag with no building standing south-east of the node: the building takes that flag, as the
    // original's Building_Place does, and puts no second flag down.
    [TestMethod]
    public void ALargeSiteOnANodeWhoseSouthEastNodeHoldsAFlagTakesThatFlag()
    {
        var game = World();
        var (x, y, _) = Find(game, 4);
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x + 1, y + 1));
        game.Tick();
        var flags = game.Flags.Count;
        Assert.AreEqual(5, BuildSite.Evaluate(game.Tables, game.Map, x, y, 0, hasCastle: true).Node);

        Assert.IsTrue(Place(game, BuildingType.Sawmill, x, y));

        Assert.AreEqual(flags, game.Flags.Count);
        var flag = game.Flags[game.Buildings[1].Flag];
        Assert.AreEqual((x + 1, y + 1), (flag.X, flag.Y));
    }

    [TestMethod]
    public void ASmallBuildingTakesASiteOf3()
    {
        var game = World();
        var (x, y, _) = Find(game, 3);

        Assert.IsTrue(Place(game, BuildingType.Woodcutter, x, y));
        Assert.AreEqual(0, game.Buildings[1].Site?.TargetHeight, "the evaluator leaves the height at 0 for the site 3");
    }

    [TestMethod]
    public void ALargeTypeOnASiteOfASmallBuildingChangesNoState()
    {
        var refused = World();
        var control = World();
        var (x, y, _) = Find(refused, 3);

        var placed = Place(refused, BuildingType.Sawmill, x, y);
        control.Tick();

        Assert.IsFalse(placed);
        Assert.AreEqual(1, refused.Buildings.Count);
        Assert.AreEqual(control.Digest(), refused.Digest());
        Assert.AreEqual(0, refused.History.Events.All.Count(e => e.Type == EventType.PlaceBuilding));
    }

    [TestMethod]
    public void ASiteOnAnOccupiedNodeChangesNoState()
    {
        var game = World();
        var control = World();
        var (x, y, _) = Find(game, 4);
        Place(game, BuildingType.Woodcutter, x, y);
        Place(control, BuildingType.Woodcutter, x, y);

        var placed = Place(game, BuildingType.Forester, x, y);
        control.Tick();

        Assert.IsFalse(placed);
        Assert.AreEqual(2, game.Buildings.Count);
        Assert.AreEqual(control.Digest(), game.Digest());
    }

    [TestMethod]
    public void ASiteOnANodeNobodyOwnsChangesNoState()
    {
        var refused = World();
        var control = World();

        // The far corner of the map lies outside the castle's land.
        var placed = Place(refused, BuildingType.Woodcutter, 2, 2);
        control.Tick();

        Assert.IsFalse(placed);
        Assert.AreEqual(control.Digest(), refused.Digest());
    }

    [TestMethod]
    public void ASiteWithNoRoomForItsFlagChangesNoState()
    {
        var refused = World();
        var control = World();

        // The castle's flag stands south-east of the castle: no building fits on the node next to it.
        var placed = Place(refused, BuildingType.Woodcutter, 16, 5);
        control.Tick();

        Assert.IsFalse(placed);
        Assert.AreEqual(control.Digest(), refused.Digest());
    }

    [TestMethod]
    public void ACommandWithNoTypeOrATypeNoSliceBuildsChangesNoState()
    {
        var game = World();
        var control = World();
        var (x, y, _) = Find(game, 4);

        Assert.IsFalse(Place(game, null, x, y));
        Assert.IsFalse(Place(game, BuildingType.Storehouse, x, y));
        Assert.IsFalse(Place(game, BuildingType.Castle, x, y));
        control.Tick();
        control.Tick();
        control.Tick();

        Assert.AreEqual(1, game.Buildings.Count);
        Assert.AreEqual(control.Digest(), game.Digest());
    }

    [TestMethod]
    public void TwoCommandsThatDifferInTheTypeAreTwoCommands()
    {
        var wood = new Command(1, 0, CommandKind.PlaceBuilding, 3, 4, Building: BuildingType.Woodcutter);
        var stone = wood with { Building = BuildingType.Stonecutter };

        Assert.AreNotEqual(wood, stone);
        Assert.AreEqual(wood, wood with { });
        Assert.AreEqual(wood.GetHashCode(), (wood with { }).GetHashCode());
    }

    [TestMethod]
    [DataRow(2, "woodcutter-hut", "planks=2 stones=0")]
    [DataRow(4, "stonecutter-hut", "planks=2 stones=0")]
    [DataRow(9, "forester-hut", "planks=2 stones=0")]
    [DataRow(0x11, "sawmill", "planks=3 stones=2")]
    public void TheThingReadoutOfASiteNamesItsTypeAndShowsItsNeeds(int code, string name, string needs)
    {
        var game = World();
        var (x, y, _) = Find(game, 4);
        Place(game, new BuildingType(code), x, y);

        var line = OpenSettlers.MapView.Thing.Of(game.Buildings[1]).Line();

        StringAssert.Contains(line, "constructing=true");
        StringAssert.Contains(line, needs);
        Assert.IsTrue(line.EndsWith(name), line);
    }

    [TestMethod]
    public void ASiteIsPartOfTheDigest()
    {
        var wood = World();
        var stone = World();
        var (x, y, _) = Find(wood, 4);

        Place(wood, BuildingType.Woodcutter, x, y);
        Place(stone, BuildingType.Sawmill, x, y);

        Assert.AreNotEqual(wood.Digest(), stone.Digest());
    }

    [TestMethod]
    public void TwoPlacementsAtOneNodeGiveTheSameDigestInEitherOrderOfEnqueue()
    {
        var forward = World();
        var backward = World();
        var (x, y, _) = Find(forward, 4);
        var woodcutter = new Command(
            forward.TickCount,
            0,
            CommandKind.PlaceBuilding,
            x,
            y,
            Building: BuildingType.Woodcutter
        );
        var sawmill = woodcutter with { Building = BuildingType.Sawmill };

        forward.Enqueue(woodcutter);
        forward.Enqueue(sawmill);
        backward.Enqueue(sawmill);
        backward.Enqueue(woodcutter);
        forward.Tick();
        backward.Tick();

        Assert.AreEqual(forward.Digest(), backward.Digest());
    }
}
