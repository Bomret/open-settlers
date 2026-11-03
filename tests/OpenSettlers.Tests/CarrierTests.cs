using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tests of the game's own carrier logic, on made-up tables (no install): every object class is 0 except that of the flag,
// code 1, which is 3, every step takes 100 clock units, and the doors have one speed. The values the original gives are in
// CarrierWalkTests. The map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at 16,6; the flags at 18,6
// (slot 1), 19,4 (slot 2), and 17,3 (slot 3) stand on it.
[TestClass]
public sealed class CarrierTests
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

    // The castle and the three flags.
    private static Game World()
    {
        var game = new Game(Made(), MapGenerator.Generate(new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]));
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, y));
            game.Tick();
        }

        return game;
    }

    private static void Build(Game game, int x, int y, params int[] directions)
    {
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, x, y, directions));
        game.Tick();
    }

    private static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }
    }

    private static Settler[] Carriers(Game game) =>
        [.. game.Settlers.Where(settler => settler.Job == Settler.CarrierJob)];

    [TestMethod]
    public void ARoadAsksTheCastleForACarrierAtItsFlagsTurnAndTheCastleSendsASettlerWithoutAJob()
    {
        var game = World();
        Build(game, 16, 6, 0, 0);
        Run(game, 27);
        Assert.AreEqual(0, Carriers(game).Length, "the flag's turn has not come");

        game.Tick();

        var carrier = Carriers(game).Single();
        Assert.AreEqual(0xf, carrier.State, "the carrier leaves the castle");
        Assert.AreEqual(0, carrier.Mode, "it takes the road that leaves the castle's flag east");
        Assert.AreEqual(0, carrier.Target, "it walks to the castle's flag");
        Assert.AreEqual(0x80, game.RoadByte(0, 0) & 0x80, "a carrier is on the way: the near end");
        Assert.AreEqual(0x80, game.RoadByte(1, 3) & 0x80, "a carrier is on the way: the far end");
    }

    [TestMethod]
    public void ARoadHoldsOneCarrierAndAsksForNoSecond()
    {
        var game = World();
        Build(game, 16, 6, 0, 0);

        Run(game, 600);

        Assert.AreEqual(1, Carriers(game).Length);
        Assert.AreEqual(1, game.RoadByte(0, 0) & 0xf);
        Assert.AreEqual(1, game.RoadByte(1, 3) & 0xf);
        Assert.AreEqual(0, game.RoadByte(0, 0) & 0x80, "the carrier is on the road: no carrier is on the way");
    }

    [TestMethod]
    public void TheCarrierWaitsOnTheRoadNextToTheFlagAtItsFarEnd()
    {
        var game = World();
        Build(game, 16, 6, 0, 0);

        Run(game, 600);

        var carrier = Carriers(game).Single();
        Assert.AreEqual(0x42, carrier.State);
        Assert.AreEqual((17, 6), (carrier.X, carrier.Y), "the node of the road next to the flag at 18,6");
        Assert.AreEqual(1, carrier.Target, "the flag it waits next to");
        Assert.AreEqual(3, carrier.Mode, "the direction in which the road leaves that flag");
        Assert.AreEqual(0x71, carrier.Frame, "the frame of the idle carrier: 0x6e plus the direction it came by");
    }

    [TestMethod]
    public void EachOfTwoRoadsBuiltAtTheSameTickGetsItsOwnCarrierInTheOrderOfTheirFlags()
    {
        var game = World();
        Build(game, 16, 6, 0, 0);
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, 18, 6, [5, 5, 0]));
        game.Tick();

        Run(game, 27);
        var first = Carriers(game).Single();
        Assert.AreEqual(0, first.Target, "the first turn is the castle's flag, for the road that leaves it");

        Run(game, 600);

        Assert.AreEqual(2, Carriers(game).Length);
        Assert.AreEqual(1, game.RoadByte(1, 5) & 0xf, "the second road holds its carrier as well");
    }

    [TestMethod]
    public void ARoadThatLeadsNowhereNearTheCastleGetsNoCarrierAndTheFlagAsksAgainAfterTheRetryScan()
    {
        var game = World();
        Build(game, 18, 6, 5, 5, 0);

        Run(game, 33);

        Assert.AreEqual(0, Carriers(game).Length);
        Assert.AreEqual(0, game.FlagStatus(1) & 0x80, "the retry scan cleared the mark of the failed request");
    }

    [TestMethod]
    public void TheCarriersStateGoesIntoTheDigest()
    {
        var a = World();
        var b = World();
        Build(a, 16, 6, 0, 0);
        Build(b, 16, 6, 0, 0);
        Run(a, 40);
        Run(b, 40);
        Assert.AreEqual(a.Digest(), b.Digest());

        Run(a, 1);
        Run(b, 1);
        a.Enqueue(new Command(a.TickCount, 0, CommandKind.BuildRoad, 18, 6, [5, 5, 0]));
        a.Tick();
        b.Tick();

        Assert.AreNotEqual(a.Digest(), b.Digest());
    }
}
