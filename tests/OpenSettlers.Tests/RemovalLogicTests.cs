using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tests of the game's own removal logic, on made-up tables (no install): every object class is 0 except that of the flag, code
// 1, which is 3, and that of the hut, code 2, which is 4; every step takes 100 clock units, the doors have one speed, a road of
// any length class holds up to `capacity` carriers, and the log counts against byte 9 of a building's fill. The values the
// original gives are in RemovalTests. The map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at 16,6 (slot 0);
// the flags at 18,6 (slot 1), 19,4 (slot 2), and 17,3 (slot 3) stand on it, with the roads 16,6 to 18,6, 18,6 to 19,4, and 17,3 to
// 19,4, and a hut stands north-west of the flag at 17,3 (16,2).
[TestClass]
public sealed class RemovalLogicTests
{
    internal static RuleTables Made(byte capacity = 8, byte[]? transportPriorities = null)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;
        var kinds = Enumerable.Repeat((byte)0xff, RuleTables.GoodKindCount).ToArray();
        kinds[WoodcutterRules.GoodLog] = 1;

        return new RuleTables(
            classes,
            Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount).ToArray(),
            goodKinds: kinds,
            roadCapacities: Enumerable.Repeat(capacity, RuleTables.RoadCapacityCount).ToArray(),
            transportPriorities: transportPriorities
        );
    }

    private static Game World(byte capacity = 8, bool hut = true, byte[]? transportPriorities = null)
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            // No tree stands anywhere, so the worker waits in its hut.
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var game = GameFactory.Create(Made(capacity, transportPriorities), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, y));
            game.Tick();
        }

        Build(game, 16, 6, 0, 0);
        Build(game, 18, 6, 5, 5, 0);
        Build(game, 17, 3, 0, 1);
        if (hut)
        {
            Huts.PlaceAndTick(game, 16, 2);
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

    private static bool Applied(Game game, int kind, int x, int y)
    {
        var digest = game.Digest();
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y));
        game.ApplyCommands();

        return digest != game.Digest();
    }

    [TestMethod]
    public void AFlagTakenDownLeavesItsSlotToTheNextFlagPlaced()
    {
        var game = World(hut: false);
        Assert.IsTrue(Applied(game, CommandKind.PlaceFlag, 13, 4), "a flag with no road");
        Assert.AreEqual(5, game.Flags.Count);
        Assert.IsTrue(Applied(game, CommandKind.PlaceFlag, 13, 7));
        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 4));
        Assert.IsTrue(game.Flags[4].IsFree, "the slot is free");
        Assert.AreEqual(6, game.Flags.Count, "a slot that is not the last leaves the count");
        Assert.IsNull(game.Map.At(13, 4).Link, "the node holds no link");
        Assert.IsFalse(game.Map.At(13, 4).Object.Exists, "or object");
        Assert.IsFalse(game.Map.At(13, 4).FlagMark, "or flag mark");

        Assert.IsTrue(Applied(game, CommandKind.PlaceFlag, 13, 4));
        Assert.AreEqual((13, 4), (game.Flags[4].X, game.Flags[4].Y), "the next flag takes the free slot");
        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 4));
        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 7));
        Assert.AreEqual(4, game.Flags.Count, "the count falls past the free slots at its end");
    }

    [TestMethod]
    public void TheClickOnAFlagWithABuildingOrOneRoadOrOnTheCastleIsRefused()
    {
        var game = World();
        Assert.IsFalse(Applied(game, CommandKind.RemoveFlag, 17, 3), "a flag with a hut and a road");
        Assert.IsFalse(Applied(game, CommandKind.RemoveFlag, 16, 6), "the castle's flag");
        Assert.IsFalse(Applied(game, CommandKind.RemoveBuilding, 15, 5), "the castle");
        Assert.IsFalse(Applied(game, CommandKind.RemoveRoad, 16, 6), "a flag is no node of a road");
        Assert.IsFalse(Applied(game, CommandKind.RemoveFlag, 1, 1), "a node the player does not own");
        Assert.IsNull(game.Removal.CommandAt(16, 6));
        Assert.AreEqual(CommandKind.RemoveFlag, game.Removal.CommandAt(18, 6));
        Assert.AreEqual(CommandKind.RemoveBuilding, game.Removal.CommandAt(16, 2));
        Assert.AreEqual(CommandKind.RemoveRoad, game.Removal.CommandAt(17, 6));
    }

    [TestMethod]
    public void ARoadTakenDownLosesItsBitsAndItsFlagsLoseTheRoad()
    {
        var game = World();
        Assert.IsTrue(Applied(game, CommandKind.RemoveRoad, 17, 6));

        Assert.AreEqual(2, game.Network.Roads.Count);
        Assert.IsTrue(game.Map.At(17, 6).Roads.IsEmpty, "the node of the road is bare");
        Assert.IsFalse(game.Map.At(16, 6).Roads.Has(0), "the castle's flag holds no east road");
        Assert.IsFalse(game.Map.At(18, 6).Roads.Has(3), "nor the flag at the other end a west road");
        Assert.IsNull(game.Removal.CommandAt(16, 6), "the castle's flag is still refused");
    }

    // The node 17,6 is a node of the road between the castle's flag and the flag at 18,6.
    [TestMethod]
    [DataRow((byte)0)]
    [DataRow((byte)0b0000_0001)]
    [DataRow((byte)0b0100_0001)]
    [DataRow((byte)0b1000_0001)]
    [DataRow((byte)0b1100_0000)]
    [Timeout(10_000)]
    public void ASplitAtANodeWithoutTwoRoadDirectionsFailsNamingTheNode(byte bits)
    {
        var game = World();
        var node = game.Map.Index(17, 6);
        game.Map.At(node).Roads = new RoadSet(bits);

        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => game.Removal.SplitRoad(node, 0, Cause.Removal)
        );

        StringAssert.Contains(failure.Message, node.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow((byte)0)]
    [DataRow((byte)0b0000_0001)]
    [DataRow((byte)0b0100_0001)]
    [DataRow((byte)0b1100_0000)]
    public void ARoadTakenDownAtANodeWithoutTwoRoadDirectionsIsRefusedAndNothingChanges(byte bits)
    {
        var game = World();
        game.Map.At(17, 6).Roads = new RoadSet(bits);
        var digest = game.Digest();

        Assert.IsFalse(game.Removal.RemoveRoadAt(17, 6));
        Assert.AreEqual(digest, game.Digest());
    }

    [TestMethod]
    public void AFlagWithTwoRoadsJoinsThemIntoOneRoadOfTheSummedLength()
    {
        var game = World();
        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 18, 6));

        Assert.AreEqual(2, game.Network.Roads.Count);
        var joined = game.Network.Roads.Single(road => road.FromSlot == 0);
        Assert.AreEqual(5, joined.Directions.Count, "2 steps and 3 steps");
        Assert.AreEqual(2, joined.ToSlot, "from the castle's flag to the flag at 19,4");
        Assert.IsTrue(game.Flags[1].IsFree);
        Assert.IsFalse(game.Map.At(18, 6).FlagMark, "the flag mark is gone");
        Assert.IsTrue(game.Map.At(18, 6).Roads.Has(3) || game.Map.At(18, 6).Roads.Has(5), "the road passes the node");
    }

    // **Inferred:** the test runs on this file's made-up tables (Made), and no emulator or debugger run reaches more settlers on
    // a joined road than its length class holds. Open lead in docs/milestones/m3-first-loop.md.
    [TestMethod]
    public void AJoinedRoadHoldsAsManyCarriersAsItsLengthClassAllowsAndTheOthersAreSentAway()
    {
        var game = World(capacity: 1, hut: false);
        for (var tick = 0; tick < 4000 && game.Settlers.Count(settler => settler.Activity is WaitsAtFlag) < 3; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(
            3,
            game.Settlers.Count(settler => settler.Activity is WaitsAtFlag),
            "a carrier waits on each of the roads"
        );
        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 18, 6));

        var states = game
            .Settlers.Where(settler => settler.Job == new Carrier())
            .Select(settler => settler.Activity.Code)
            .Order()
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { 0x42, 0x44, 0x45 },
            states,
            "the third road's carrier waits on; the joined road keeps one carrier and the other goes home"
        );
        Assert.AreEqual(
            1,
            game.Flags[0].Side(0).Carriers,
            "the joined road counts one carrier at its end at the castle's flag"
        );
    }

    [TestMethod]
    public void AGoodLostWithAFlagIsCountedAgainstTheBuildingItWasToGoTo()
    {
        var game = World();
        Assert.IsTrue(Applied(game, CommandKind.PlaceFlag, 13, 4));
        var lost = game.Flags.Count - 1;
        game.Goods.LayGood(lost, 0, WoodcutterRules.GoodLog, destination: 3);
        var hut = game.Buildings[1];
        Assert.AreEqual(0, hut.FillHigh);

        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 4));

        Assert.AreEqual(0xff, hut.FillHigh, "the byte of the log's kind counts one less (the byte wraps)");
        Assert.AreEqual(0, hut.FillLow);
    }

    [TestMethod]
    public void WhenARemovalFreesARoadsWaitingPlaceTheGoodOfTheHighestTransportPriorityForThatRoadWaitsNext()
    {
        const int stone = 9;
        const int board = 1;
        const int way = 0;
        var ranks = new byte[StartStock.Goods];
        ranks[WoodcutterRules.GoodLog - 1] = 5;
        ranks[stone - 1] = 9;
        ranks[board - 1] = 2;
        var game = World(transportPriorities: ranks);
        game.Goods.LayGood(1, 0, board, way: way);
        game.Goods.LayGood(1, 1, WoodcutterRules.GoodLog, way: way);
        game.Goods.LayGood(1, 2, stone, way: way);
        game.Goods.LayGood(1, 5, board, destination: 3, way: way);
        game.Goods.WaitForRoad(1, way, 5);

        Assert.IsTrue(Applied(game, CommandKind.RemoveBuilding, 16, 2));

        Assert.IsNull(game.Flags[1].Places[5].Way, "the good for the hut loses its direction");
        Assert.AreEqual(new Good(board), game.Flags[1].Places[5].Good);
        Assert.AreEqual(2, game.Flags[1].Side(way).WaitingPlace, "the stone waits for the road next");
    }

    [TestMethod]
    public void ABuildingTakenDownBurnsAndThenLeavesItsNodeAndItsSlotFree()
    {
        var game = World();
        var hut = game.Buildings[1];
        var node = game.Map.Index(16, 2);
        Assert.IsTrue(Applied(game, CommandKind.RemoveBuilding, 16, 2));

        Assert.IsTrue(hut.IsDemolishing);
        Assert.AreEqual(0x7ff, (int)hut.BurnLeft, "it burns for 0x7ff clock units");
        Assert.AreEqual(MapObjectKind.Hut, game.Map.At(node).Object.Kind, "its object stays while it burns");
        Assert.IsFalse(game.Map.At(node).Roads.Has(1), "its node lost the road to its flag");
        Assert.IsFalse(game.Map.At(17, 3).Roads.Has(4), "and its flag the road to it");

        Run(game, 400);

        Assert.AreEqual(1, game.Buildings.Count, "the castle only");
        Assert.IsFalse(game.Map.At(node).Object.Exists);
        Assert.IsFalse(game.Map.At(node).LakeMark);
        Assert.IsTrue(Huts.Place(game, 16, 2), "a hut may stand there again");
    }

    // Acceptance: a removal changes nothing else. The terrain and the heights, the stock, and the second road with its flags, the
    // same before and after the road next to it is taken down. (The hut is no part of it: its worker may walk the road that goes,
    // and then the hut's status changes, as the original's dismissal of the worker does.)
    [TestMethod]
    public void ARemovalLeavesAnUnrelatedPartOfTheGameAsItWas()
    {
        var game = World(hut: false);
        Run(game, 100);
        string Part() =>
            string.Join(
                ' ',
                Layers.Digest(game.Map, MapLayer.Terrain),
                Layers.Digest(game.Map, MapLayer.Height),
                game.Network.Roads.Single(road => road.FromSlot == 3 && road.ToSlot == 2)
                    .Directions.Sum(step => step.Value),
                string.Concat(
                    Enumerable
                        .Range(2, 2)
                        .Select(flag =>
                            $"{FlagEncoder.Status(game.Flags[flag])}{FlagEncoder.RoadByte(game.Flags[flag].Side(0))}{FlagEncoder.RoadByte(game.Flags[flag].Side(5))}"
                        )
                ),
                string.Concat(game.CastleStock.Stock)
            );
        var before = Part();
        var whole = game.Digest();

        Assert.IsTrue(Applied(game, CommandKind.RemoveRoad, 17, 6));

        Assert.AreEqual(before, Part());
        Assert.AreNotEqual(whole, game.Digest(), "the digest of the whole game notes the removal");
    }

    // The view: the goods of the flags that were taken down are not drawn (their slots hold the bytes they had), and a hut that
    // burns is not drawn.
    [TestMethod]
    public void TheViewDrawsNeitherAFreeFlagsGoodsNorAHutThatBurns()
    {
        var game = World();
        foreach (var (x, y) in new[] { (13, 4), (13, 7), (14, 8) })
        {
            Assert.IsTrue(Applied(game, CommandKind.PlaceFlag, x, y));
            game.Goods.LayGood(game.Flags.Count - 1, 0, WoodcutterRules.GoodLog);
        }

        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 4));
        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 7));

        var goods = OpenSettlers.MapView.ObjectDraws.FlagGoodsOf(game);
        CollectionAssert.AreEqual(new[] { (14, 8) }, goods.Keys.ToArray());

        int Huts() =>
            OpenSettlers
                .MapView.ObjectDraws.Of(game.Map, game.CastleStock.Castle, game.Buildings)
                .OfType<OpenSettlers.MapView.BuildingDraw>()
                .Count(draw => draw.Type == BuildingType.Woodcutter);
        Assert.AreEqual(1, Huts());
        Assert.IsTrue(Applied(game, CommandKind.RemoveBuilding, 16, 2));
        Assert.AreEqual(0, Huts());
    }

    [TestMethod]
    public void TheDigestHoldsWhatARemovalChanges()
    {
        var first = World();
        var second = World();
        Assert.AreEqual(first.Digest(), second.Digest());

        Assert.IsTrue(Applied(first, CommandKind.RemoveBuilding, 16, 2));
        Assert.AreNotEqual(first.Digest(), second.Digest(), "a building taken down");
        Run(first, 10);
        Run(second, 10);
        Assert.IsTrue(Applied(second, CommandKind.RemoveBuilding, 16, 2));
        Run(first, 2);
        Run(second, 2);
        Assert.AreNotEqual(first.Digest(), second.Digest(), "the countdown of the burning building is in the digest");
    }

    // The removal on a bench: a map, flag table, and road network made by hand, with fake carrier and goods seams, so a test
    // sees what the removal asks of the carriers without a castle or a game.
    private const int East = 0;
    private const int West = 3;

    // The carrier behaviour as the removal sees it: it notes what the removal asks of it.
    private sealed class FakeCarriers : IRoadCarriers
    {
        public List<Road> Forgotten { get; } = [];

        public List<(Road Road, Cause Cause)> Caused { get; } = [];

        public void ForgetCause(Road road) => Forgotten.Add(road);

        public void CarryCause(Road road, Cause command) => Caused.Add((road, command));

        public void Claim(int slot, Settler settler) { }
    }

    private sealed class FakePriorities : ITransportPriorities
    {
        public int TransportPriority(Good good) => good.Code;
    }

    private sealed class Rig
    {
        public Rig()
        {
            Bench = new BehaviourBench();
            Bench.RaiseAndOwn();
            // Flag 0 and flag 1 at the ends of a road of four steps east, flag 2 on a node of its own.
            PutFlag(4, 4);
            PutFlag(8, 4);
            PutFlag(4, 8);
            Road = Bench.Join(0, East, 1, West, steps: 4, carrier: false);
            for (var x = 4; x <= 8; x++)
            {
                Bench.Map.At(x, 4).Roads = x switch
                {
                    4 => RoadSet.Of(East),
                    8 => RoadSet.Of(West),
                    _ => RoadSet.Of(East).With(West),
                };
            }

            Removal = new Removal(
                Bench.Movement,
                Bench.Settlers,
                Bench.Buildings,
                Bench.Flags,
                Bench.Roads,
                Bench.History,
                Carriers,
                new FakePriorities(),
                _ => { },
                () => true,
                _ => { }
            );
        }

        public BehaviourBench Bench { get; }

        public FakeCarriers Carriers { get; } = new();

        public Removal Removal { get; }

        public Road Road { get; }

        private void PutFlag(int x, int y)
        {
            var slot = Bench.Flags.Add(x, y);
            ref var node = ref Bench.Map.At(x, y);
            node.Object = MapObject.Flag;
            node.FlagMark = true;
            node.LinkTo(slot);
        }
    }

    [TestMethod]
    public void ARoadTakenDownIsForgottenByTheCarriersAndFreesTheCarrierMarkOfItsFlags()
    {
        var rig = new Rig();
        rig.Bench.Flags[1].Side(West).HoldsCarrier = true;

        Assert.IsTrue(rig.Removal.RemoveRoadAt(6, 4));

        Assert.IsFalse(rig.Bench.Flags[1].Side(West).HoldsCarrier);
        CollectionAssert.AreEqual(new[] { rig.Road }, rig.Carriers.Forgotten);
    }

    [TestMethod]
    public void ARoadSplitByAFlagIsForgottenByTheCarriersAndEachHalfIsAskedForWithTheCommandsCause()
    {
        var rig = new Rig();
        var slot = rig.Bench.Flags.Add(6, 4);
        rig.Bench.Map.At(6, 4).FlagMark = true;
        rig.Bench.Map.At(6, 4).LinkTo(slot);
        var cause = Cause.Razed(rig.Bench.History.FlagId(slot));

        rig.Removal.SplitRoad(rig.Bench.Map.Index(6, 4), slot, cause);

        CollectionAssert.AreEqual(new[] { rig.Road }, rig.Carriers.Forgotten);
        Assert.AreEqual(
            2,
            rig.Carriers.Caused.Count,
            "each half without a carrier is asked for with the command's cause"
        );
    }
}
