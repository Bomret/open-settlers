using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tests of the game's own hut and walk logic, on made-up tables (no install): every object class is 0 except that of
// the flag, code 1, which is 3, every step takes 100 clock units, and the doors have one speed. The values the
// original gives are in WorkerWalkTests. The map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at
// 16,6; a hut stands north-west of the flag at 17,3 (16,2), and roads run from the castle's flag east to 18,6, from
// there north and east to 19,4, and from 17,3 east and south-east to 19,4.
[TestClass]
public sealed class HutTests
{
    private static ProgramTables Made()
    {
        var classes = new byte[ProgramTables.ClassCount];
        classes[1] = 3;

        return new ProgramTables(
            classes,
            Enumerable
                .Range(0, ProgramTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, ProgramTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, ProgramTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, ProgramTables.ScanWindowCount).ToArray()
        );
    }

    private static readonly (int X, int Y)[] Flags = [(18, 6), (19, 4), (17, 3)];

    // The castle, the three flags (slots 1 to 3), and the roads between the flags 0 and 1 and between 1 and 2.
    private static Game World()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            // No tree stands anywhere, so the worker waits in its hut (WoodcuttingTests holds the work).
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var game = new Game(Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        foreach (var (x, y) in Flags)
        {
            game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, y));
            game.Tick();
        }

        Build(game, 16, 6, 0, 0);
        Build(game, 18, 6, 5, 5, 0);

        return game;
    }

    private static void Build(Game game, int x, int y, params int[] directions)
    {
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, x, y, directions));
        game.Tick();
    }

    private static void PlaceHut(Game game)
    {
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 16, 2));
        game.Tick();
    }

    private static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }
    }

    [TestMethod]
    public void ACastleStandsWithItsTwentyFirstSettlersInState1()
    {
        var game = World();

        Assert.AreEqual(1, game.Buildings.Count);
        Assert.AreEqual(20, game.Settlers.Count);
        Assert.AreEqual(Settler.WoodcutterJob, game.Settlers[10].Job);
        Assert.IsTrue(game.Settlers.Skip(1).All(settler => settler.State == 1 && settler.X == 15 && settler.Y == 5));
    }

    [TestMethod]
    public void AHutPlacedAtAFlagStandsFinishedAtOnce()
    {
        var game = World();

        PlaceHut(game);

        var hut = game.Buildings[1];
        Assert.AreEqual(Building.WoodcutterType, hut.Type);
        Assert.AreEqual((16, 2), (hut.X, hut.Y));
        Assert.AreEqual(3, hut.Flag);
        Assert.IsFalse(hut.Constructing);
        Assert.AreEqual(0, hut.Status & 0x40);
        Assert.AreEqual(MapObjectKind.Hut, game.Map.At(16, 2).Object.Kind);
    }

    // The original's placement (0x449a63) puts the building's flag on the south-east node where none stands there (node
    // state other than 5): the hut and its flag go down in one command.
    [TestMethod]
    public void AHutPlacedWhereNoFlagStandsPutsItsFlagDownSouthEastOfIt()
    {
        var game = World();
        var (x, y) = FreeSite(game);

        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, x, y));
        game.Tick();

        Assert.AreEqual(2, game.Buildings.Count);
        var hut = game.Buildings[1];
        Assert.AreEqual((x, y), (hut.X, hut.Y));
        Assert.AreEqual((x + 1, y + 1), (game.Flags[hut.Flag].X, game.Flags[hut.Flag].Y));
        Assert.AreEqual(Flags.Length + 1, hut.Flag);
        Assert.AreEqual(MapObjectKind.Flag, game.Map.At(x + 1, y + 1).Object.Kind);
    }

    [TestMethod]
    public void ASpotWithNoRoomForABuildingChangesNoState()
    {
        var refused = World();
        var control = World();

        // 17,2 has the flag at 17,3 as its south neighbour, which leaves it no site for a building.
        refused.Enqueue(new Command(refused.TickCount, 0, CommandKind.PlaceBuilding, 17, 2));
        refused.Tick();
        control.Tick();

        Assert.AreEqual(1, refused.Buildings.Count);
        Assert.AreEqual(control.Digest(), refused.Digest(), "a refused command changes no state");
    }

    // A node of the castle's land with a site for a building and nothing on or around its south-east node (node state 7).
    private static (int X, int Y) FreeSite(Game game)
    {
        var tables = Made();
        for (var y = 0; y < game.Map.Size.Height - 1; y++)
        {
            for (var x = 0; x < game.Map.Size.Width - 1; x++)
            {
                var site = BuildSite.Evaluate(tables, game.Map, x, y, 0, hasCastle: true);
                if (site is { Node: 7, Site: >= 3 })
                {
                    return (x, y);
                }
            }
        }

        throw new AssertFailedException("the map holds no free site");
    }

    [TestMethod]
    public void ASecondHutOnTheSameFlagIsRefused()
    {
        var game = World();
        PlaceHut(game);

        PlaceHut(game);

        Assert.AreEqual(2, game.Buildings.Count);
    }

    [TestMethod]
    public void NothingCanBePlacedBeforeTheCastleStands()
    {
        var game = new Game(
            Made(),
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );

        PlaceHut(game);

        Assert.AreEqual(0, game.Buildings.Count);
    }

    [TestMethod]
    public void TheRouteSearchGivesTheFirstDirectionOfTheWay()
    {
        var game = World();
        Build(game, 17, 3, 0, 1);

        Assert.AreEqual(0, game.Route(0, 1));
        Assert.AreEqual(0, game.Route(0, 3), "the castle's flag leads on only east");
        Assert.AreEqual(3, game.Route(1, 0), "from the flag at 18,6 the road to the castle leaves west");
        Assert.AreEqual(5, game.Route(1, 2), "the road to 19,4 leaves north");
    }

    [TestMethod]
    public void TheRouteSearchGivesNoDirectionWhereNoRoadJoinsTheFlagsOrTheFlagsAreTheSame()
    {
        var game = World();

        Assert.AreEqual(-1, game.Route(0, 3), "the flag at 17,3 has no road yet");
        Assert.AreEqual(-1, game.Route(1, 1));
        Assert.AreEqual(-1, game.Route(0, 9));
    }

    [TestMethod]
    public void AWorkerWalksFromTheCastleToAHutOverTheRoadsAndEntersIt()
    {
        var game = World();
        Build(game, 17, 3, 0, 1);
        PlaceHut(game);

        Run(game, 1200);

        var worker = game.Settlers[10];
        Assert.AreEqual(0x12, worker.State, "inside the hut, looking for a tree");
        Assert.AreEqual((16, 2), (worker.X, worker.Y));
        Assert.AreEqual(10, game.Buildings[1].Worker);
        Assert.AreEqual(0x40, game.Buildings[1].Status & 0xc0, "the worker is inside, and no longer on the way");
    }

    [TestMethod]
    public void AHutNotJoinedToTheCastleGetsNoWorkerUntilARoadJoinsIt()
    {
        var game = World();
        PlaceHut(game);

        Run(game, 300);
        var idle = game.Settlers[10];
        Assert.AreEqual(1, idle.State, "the worker waits in the castle");
        Assert.AreEqual(0, game.Buildings[1].Status & 0x80);

        Build(game, 17, 3, 0, 1);
        Run(game, 1200);

        Assert.AreEqual(0x12, game.Settlers[10].State);
        Assert.AreEqual(10, game.Buildings[1].Worker);
    }

    [TestMethod]
    public void TheSameCommandsGiveTheSameDigest()
    {
        Game Walk()
        {
            var game = World();
            Build(game, 17, 3, 0, 1);
            PlaceHut(game);
            Run(game, 400);

            return game;
        }

        Assert.AreEqual(Walk().Digest(), Walk().Digest());
    }

    [TestMethod]
    public void TheWorkersPositionIsPartOfTheDigest()
    {
        var game = World();
        Build(game, 17, 3, 0, 1);
        PlaceHut(game);
        Run(game, 100);
        var other = World();
        Build(other, 17, 3, 0, 1);
        PlaceHut(other);
        Run(other, 100);
        Assert.AreEqual(game.Digest(), other.Digest());

        other.Settlers[10].X++;

        Assert.AreNotEqual(game.Digest(), other.Digest());
    }
}
