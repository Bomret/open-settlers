using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The boundaries of the simulation's application layer: the castle stock's guards, the clock's wrap, the handler table, the storage
// hand-out's guards, the cursor windows of the retry scans, the walk home's search and random node, and the lake detail. Parts are
// made by hand (no install). The expected values are hand arithmetic from the rules the routines cite: the 0x428728 scan and the
// 0x428d6e lake detail in docs/rules/ and the routines' own comments, and the spiral table's first entries.
[TestClass]
public sealed class SimulationBoundaryTests
{
    // ----- Castle stock (F26) -----

    [TestMethod]
    [DataRow(0)]
    [DataRow(27)]
    [DataRow(-1)]
    public void AGoodCodeOutsideOneToTwentySixHasNoCountAndTheStockIgnoresIt(int code)
    {
        var castle = new CastleStock();
        castle.Place(new Castle(1, 1, 1), new int[StartStock.Goods]);
        var good = new Good(code);

        castle.Add(good);

        Assert.AreEqual((0, false, 0), (castle.Count(good), castle.Take(good), castle.Stock.Sum()));
    }

    [TestMethod]
    public void BeforeTheCastleIsPlacedTheCountsAreZeroAndAddAndTakeChangeNothing()
    {
        var castle = new CastleStock();

        castle.Add(Good.Plank);

        Assert.AreEqual(
            (0, false, 0, null),
            (castle.Count(Good.Plank), castle.Take(Good.Plank), castle.Stock.Count, castle.Castle)
        );
    }

    // ----- Game clock (F27) and the handler table (F28) -----

    [TestMethod]
    public void TheClockWrapsPastFFFFAtItsStepOfEight()
    {
        var clock = new GameClock();
        // 8191 steps of 8 reach 0xFFF8, the last value below the wrap (hand arithmetic: 0x10000 / 8 = 8192).
        for (var i = 0; i < 8191; i++)
        {
            clock.Advance();
        }

        var before = clock.Now;
        clock.Advance();

        Assert.AreEqual((0xFFF8, 0), (before, (int)clock.Now));
    }

    sealed class KindHandler(int kind) : ICommandHandler
    {
        public int Kind => kind;

        public bool Handle(Command command) => true;
    }

    static GameParts PartsOf(params ICommandHandler[] handlers) =>
        new()
        {
            Tables = null!,
            Map = null!,
            CastleStock = null!,
            Flags = null!,
            Network = null!,
            Buildings = null!,
            Settlers = null!,
            History = null!,
            Causes = null!,
            Clock = null!,
            Draws = null!,
            Movement = null!,
            Behaviours = null!,
            Pass = null!,
            Roster = null!,
            Carriers = null!,
            Goods = null!,
            Removal = null!,
            WalkHome = null!,
            Walks = null!,
            Hut = null!,
            Placement = null!,
            RoadPlanner = null!,
            Queue = null!,
            Handlers = handlers,
            Digest = null!,
        };

    [TestMethod]
    public void TwoHandlersOfOneKindRefuseTheGame()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Game(PartsOf(new KindHandler(3), new KindHandler(3))));
    }

    [TestMethod]
    public void HandlersOfDifferentKindsBuildTheGame()
    {
        var game = new Game(PartsOf(new KindHandler(3), new KindHandler(4)));

        Assert.AreEqual(2, game.Handlers.Count);
    }

    // ----- Cursor windows (F31): 0x428728 clears a window of entries from a cursor, stops after the tenth flagged one -----

    static Game WindowGame(int window)
    {
        var tables = LayoutSettlersTests.MadeTables();
        tables.ScanWindows[MapSize.Offered[0].Value] = (ushort)window;

        return GameFactory.Create(
            tables,
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );
    }

    static BuildingTable WithBuildings(Game game, int count, bool failed = false)
    {
        var table = (BuildingTable)game.Buildings;
        for (var i = 0; i < count; i++)
        {
            table.Add(new Building(BuildingType.Woodcutter, 2 + i, 2, 0) { RequestFailed = failed });
        }

        return table;
    }

    [TestMethod]
    public void TheBuildingScanWindowIsClampedAtTheTableEndAndTheNextPassStartsOver()
    {
        var game = WindowGame(4);
        WithBuildings(game, 6);
        var cursors = new int[3];

        for (var pass = 0; pass < cursors.Length; pass++)
        {
            game.Pass.Run();
            cursors[pass] = game.Pass.ScanCursor;
        }

        // 6 entries, window 4: 0 to 4, then min(6 - 4, 4) = 2 entries to 6 (not 8), then the cursor at the count resets.
        CollectionAssert.AreEqual(new[] { 4, 6, 4 }, cursors);
    }

    [TestMethod]
    public void TheBuildingScanCursorPastTheCountResetsToTheTableStart()
    {
        var game = WindowGame(4);
        var table = WithBuildings(game, 6);
        game.Pass.Run();
        game.Pass.Run();
        table.Free(5);
        table.Free(4);

        game.Pass.Run();

        // The cursor, 6, lies past the 4 entries left: the scan starts at 0 and takes the 4 (a cursor not reset would take none).
        Assert.AreEqual(4, game.Pass.ScanCursor);
    }

    [TestMethod]
    public void TheBuildingScanStopsAfterTheTenthFlaggedEntryAndTheNextPassResumesThere()
    {
        var game = WindowGame(16);
        var table = WithBuildings(game, 15, failed: true);

        game.Pass.Run();

        var afterFirst = (game.Pass.ScanCursor, table.Count(building => building.RequestFailed));
        game.Pass.Run();

        // Ten flagged entries (0 to 9) are cleared and the cursor stays on the tenth; the pass after clears the other 5.
        Assert.AreEqual(
            ((9, 5), (15, 0)),
            (afterFirst, (game.Pass.ScanCursor, table.Count(building => building.RequestFailed)))
        );
    }

    static void WithFlags(Game game, int count, bool failed = false)
    {
        for (var i = 0; i < count; i++)
        {
            game.Flags.Add(2 + i, 2);
            game.Flags[i].CarrierRequestFailed = failed;
        }
    }

    [TestMethod]
    public void TheFlagScanWindowIsClampedAtTheTableEndAndTheNextScanStartsOver()
    {
        var game = WindowGame(4);
        WithFlags(game, 6);
        var cursors = new int[3];

        for (var scan = 0; scan < cursors.Length; scan++)
        {
            game.Carriers.ClearFlagRetryBits();
            cursors[scan] = game.Carriers.FlagCursor;
        }

        CollectionAssert.AreEqual(new[] { 4, 6, 4 }, cursors);
    }

    [TestMethod]
    public void TheFlagScanCursorPastTheCountResetsToTheTableStart()
    {
        var game = WindowGame(4);
        WithFlags(game, 6);
        game.Carriers.ClearFlagRetryBits();
        game.Carriers.ClearFlagRetryBits();
        game.Flags.Free(5);
        game.Flags.Free(4);

        game.Carriers.ClearFlagRetryBits();

        Assert.AreEqual(4, game.Carriers.FlagCursor);
    }

    [TestMethod]
    public void TheFlagScanStopsAfterTheTenthFlaggedEntryAndTheNextScanResumesThere()
    {
        var game = WindowGame(16);
        WithFlags(game, 15, failed: true);

        game.Carriers.ClearFlagRetryBits();

        var afterFirst = (game.Carriers.FlagCursor, game.Flags.Count(flag => flag.CarrierRequestFailed));
        game.Carriers.ClearFlagRetryBits();

        Assert.AreEqual(
            ((9, 5), (15, 0)),
            (afterFirst, (game.Carriers.FlagCursor, game.Flags.Count(flag => flag.CarrierRequestFailed)))
        );
    }

    // ----- Walk home (F33 the far end first, F34 the rings of the random node) -----

    static WalkHomeBehavior Behaviour(BehaviourBench bench) =>
        new(bench.Movement, bench.Buildings, bench.Flags, bench.Roads, bench.History, bench.Draws, (_, _) => -1);

    // A flag of player 0 on the node with a road leaving it (to a flag far off).
    static void HomeFlag(BehaviourBench bench, int x, int y, int direction)
    {
        var slot = bench.Flags.Add(x, y);
        bench.Map.At(x, y).FlagMark = true;
        bench.Map.At(x, y).Owner = Owner.Player(0);
        var elsewhere = bench.Flags.Add(24 + slot, 24);
        bench.Join(slot, direction, elsewhere, (direction + 3) % 6);
    }

    static Settler Searching(BehaviourBench bench, bool far, out int slot)
    {
        var settler = new Settler(new Woodcutter(), 0, 10, 10, 0, new SearchesHome(far));
        slot = bench.Place(settler);
        bench.Clock = 8;

        return settler;
    }

    static Vector WalkTarget(Settler settler) =>
        ((WalksFree)(settler.Activity with { Residue = default })).Trip.Remaining;

    [TestMethod]
    [DataRow(false, 1, 0)]
    [DataRow(true, 2, 1)]
    public void TheSearchTakesTheNearestFlagFromTheNearEndOrTheFarthestFromTheFarEnd(bool far, int dx, int dy)
    {
        // The spiral's entry 1 is (1, 0) and its entry 7 is (2, 1) (the ring of 6 comes first, then the seed (2, 1) of ring 2).
        var bench = new BehaviourBench();
        HomeFlag(bench, 11, 10, 0);
        HomeFlag(bench, 12, 11, 1);
        var settler = Searching(bench, far, out var slot);

        Behaviour(bench).Search(slot, settler);

        Assert.AreEqual(new Vector(dx, dy), WalkTarget(settler));
    }

    static (Vector Walk, int Draws) HomeNodeAfter(ushort[] draws, Action<Map>? prepare = null)
    {
        var bench = new BehaviourBench(draws);
        prepare?.Invoke(bench.Map);
        var settler = Searching(bench, false, out var slot);

        Behaviour(bench).Search(slot, settler);

        return (WalkTarget(settler), bench.Draws.Taken);
    }

    static ushort[] Draws(int fillers, ushort last) => [.. Enumerable.Repeat((ushort)0, fillers).Append(last)];

    static void Qualify(Map map, int x, int y, Owner owner)
    {
        map.At(x, y).Height = 1;
        map.At(x, y).Owner = owner;
    }

    // The settler stands at (10, 10) on the map of 32 by 32. Ring 1 takes 10 draws (mask 15), ring 2 the next 20 (the draw that
    // opens it and 19 more, mask 31), ring 3 the next 20 (mask 63), then any node of any owner qualifies (0x43560c, inferred).
    [TestMethod]
    public void TheFirstRingTakesANodeOfTheSettlersPlayerWithinTenDraws()
    {
        // The draw 0x0a0b: offsets 11 - 8 = 3 and 10 - 8 = 2, the ring of mask 15.
        var result = HomeNodeAfter([0, 0x0a0b], map => Qualify(map, 13, 12, Owner.Player(0)));

        Assert.AreEqual((new Vector(3, 2), 2), result);
    }

    [TestMethod]
    public void TheSecondRingOpensAtTheEleventhDrawWithAMaskOf31()
    {
        // The draw 0x0505 in ring 2: offsets 5 - 16 = -11 each, the node (31, 31).
        var result = HomeNodeAfter(Draws(10, 0x0505), map => Qualify(map, 31, 31, Owner.Player(0)));

        Assert.AreEqual((new Vector(-11, -11), 11), result);
    }

    [TestMethod]
    public void TheThirdRingOpensAtTheThirtyFirstDrawWithAMaskOf63()
    {
        // The draw 0x0505 in ring 3: offsets 5 - 32 = -27 each, the node (15, 15) of the wrapped map.
        var result = HomeNodeAfter(Draws(30, 0x0505), map => Qualify(map, 15, 15, Owner.Player(0)));

        Assert.AreEqual((new Vector(-27, -27), 31), result);
    }

    [TestMethod]
    public void AfterThreeRingsAnyNodeWithAHeightIsTakenWhoseverItIs()
    {
        // The filler draw 0 names (-8, -8) in ring 1, (-16, -16) in ring 2, and (-32, -32), the settler's own node, in ring 3.
        // Only the node (2, 2) has a height, and it is another player's: ring 1 refuses it, the 51st draw (mask 15 again) takes it.
        var result = HomeNodeAfter(Draws(50, 0), map => Qualify(map, 2, 2, Owner.Player(1)));

        Assert.AreEqual((new Vector(-8, -8), 51), result);
    }

    // ----- Lake detail (F35): 0x428d6e takes one draw; the object pass reaches (23, 0) first -----

    static (HutWork Work, BehaviourBench Bench) LakeBench(params ushort[] draws)
    {
        var bench = new BehaviourBench(draws);
        var walkHome = Behaviour(bench);
        var work = new HutWork(
            bench.Movement,
            bench.Settlers,
            bench.Flags,
            bench.History,
            bench.Draws,
            walkHome,
            (_, _, _) => { },
            () => false
        );
        // A clock of 8 makes the pass take exactly one node, (23, 0): the timer falls to 0xFFF8, which is not below 0xFFEC.
        bench.Clock = 8;

        return (work, bench);
    }

    static void Lake(Map map, int x, int y, int detail)
    {
        map.At(x, y).WaterTriangle = true;
        map.At(x, y).LakeMark = true;
        map.At(x, y).LakeDetail = detail;
    }

    [TestMethod]
    public void ALakeDetailBelowTenGrowsByOneWhenTheDrawsBitsEightToThirteenAreClear()
    {
        var (work, bench) = LakeBench(0x0000);
        Lake(bench.Map, 23, 0, 3);

        work.ObjectPass();

        // The draw 0 names direction 1, the node (24, 1), which is no lake node: nothing moves.
        Assert.AreEqual((4, 1), (bench.Map.At(23, 0).LakeDetail, bench.Draws.Taken));
    }

    [TestMethod]
    public void ALakeDetailDoesNotGrowWhenAnyOfTheDrawsBitsEightToThirteenIsSet()
    {
        var (work, bench) = LakeBench(0x0100);
        Lake(bench.Map, 23, 0, 3);

        work.ObjectPass();

        Assert.AreEqual((3, 1), (bench.Map.At(23, 0).LakeDetail, bench.Draws.Taken));
    }

    // The draw's bits 2 and 3 pick the neighbour: 0 south-east (1), 4 south (2), 8 north-west (4), 12 north (5). The node (23, 0) is on the
    // top row, so the directions 4 and 5 cross the map's edge to the row 31.
    [TestMethod]
    [DataRow(0x0100, 24, 1)]
    [DataRow(0x0104, 23, 1)]
    [DataRow(0x0108, 22, 31)]
    [DataRow(0x010c, 23, 31)]
    public void ALakeDetailMovesOneToTheLakeNeighbourOfTheDrawnDirection(int draw, int x, int y)
    {
        var (work, bench) = LakeBench((ushort)draw);
        Lake(bench.Map, 23, 0, 3);
        Lake(bench.Map, x, y, 2);

        work.ObjectPass();

        Assert.AreEqual((2, 3, 1), (bench.Map.At(23, 0).LakeDetail, bench.Map.At(x, y).LakeDetail, bench.Draws.Taken));
    }

    [TestMethod]
    public void ALakeDetailOfTenStaysAndDoesNotMoveYetTheDrawIsTaken()
    {
        var (work, bench) = LakeBench(0x0000);
        Lake(bench.Map, 23, 0, 10);
        Lake(bench.Map, 24, 1, 2);

        work.ObjectPass();

        Assert.AreEqual(
            (10, 2, 1),
            (bench.Map.At(23, 0).LakeDetail, bench.Map.At(24, 1).LakeDetail, bench.Draws.Taken)
        );
    }

    [TestMethod]
    public void ALakeDetailOfNineGrowsToTenAndThenMovesOneAway()
    {
        var (work, bench) = LakeBench(0x0000);
        Lake(bench.Map, 23, 0, 9);
        Lake(bench.Map, 24, 1, 2);

        work.ObjectPass();

        // The growth comes before the move: 9 grows to 10, and the move takes 1 of it to the neighbour.
        Assert.AreEqual((9, 3, 1), (bench.Map.At(23, 0).LakeDetail, bench.Map.At(24, 1).LakeDetail, bench.Draws.Taken));
    }
}
