using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static OpenSettlers.Domain.Testing.SettlerCases;
using static OpenSettlers.Testing.GraphicEntries;
using static OpenSettlers.Tests.SettlerTables;

namespace OpenSettlers.Tests;

// The picks of the History Edition's figure tables for the terms of a walking settler, on made-up tables and a made-up walk
// sequence (no install). The shape of the sequences (entry 2 of the graphics archive), the tables, and the use of the timer
// follow docs/rules/settlers.md; the values are made up here, and the rule's own details are marked inferred there. The
// placement of the terms (rows, positions, order) is checked by the class of the same name in src/MapView.Tests; the terms
// here come from SettlerTerms.
[TestClass]
public sealed class SettlerFiguresTests
{
    // The adds of the woodcutter's handler (0x43fb6b): the high byte of the figure word is 0x0b for the walk, 0x10 for the
    // way back with the log, and 0x0f for a figure byte from 0x80.
    static readonly FigureAdds Adds = new()
    {
        Walk = 0x0b00,
        Carrying = 0x1000,
        Cutting = 0x0e80,
    };

    // The adds of the fisher's handler (0x43fed8), with made-up rows: 0x11 for the walk, 0x12 for the way back with a load, 0x13 and
    // 0x14 for a figure byte from 0x80.
    static readonly FisherFigureAdds HutAdds = new()
    {
        Walk = 0x1100,
        Carrying = 0x1200,
        High = 0x1280,
        HighFacing = 0x1380,
    };

    // The adds of the builder's handler (0x43faf4), made up: 0x1500 for the walk and 0x1580 for a figure byte from 0x80.
    static readonly BuilderFigureAdds BuilderAdds = new() { Walk = 0x1500, Work = 0x1580 };

    static readonly StepOffsets Steps = new(Sequences());

    // The woodcutter: the figure base 100 and the second base 50 in the row 0x0b, 200 and none in the row 0x10, 300 and
    // none in the row 0x0f; each figure byte adds its own number and, to the second, 1. The carrier's row 0: 100, none.
    static SettlerFigureTables Tables()
    {
        var bases = new short[256];
        bases[2 * 0x0b] = 100;
        bases[2 * 0x0b + 1] = 50;
        bases[2 * 0x10] = 200;
        bases[2 * 0x10 + 1] = -1;
        bases[2 * 0x0f] = 300;
        bases[2 * 0x0f + 1] = -1;
        var offsets = new short[512];
        for (var b = 0; b < 256; b++)
        {
            offsets[2 * b] = (short)b;
            offsets[2 * b + 1] = 1;
        }

        bases[0] = 100;
        bases[1] = -1;
        var carried = new short[27];
        carried[7] = 0x0a00;
        bases[2 * 0x0a] = 500;
        bases[2 * 0x0a + 1] = -1;

        bases[2 * 0x11] = 600;
        bases[2 * 0x11 + 1] = -1;
        bases[2 * 0x12] = 700;
        bases[2 * 0x12 + 1] = -1;
        bases[2 * 0x13] = 800;
        bases[2 * 0x13 + 1] = -1;
        bases[2 * 0x14] = 900;
        bases[2 * 0x14 + 1] = -1;

        // The row 2 of the waiting carrier's fixed figure word 0x203 (0x44d77e).
        bases[2 * 2] = 400;
        bases[2 * 2 + 1] = -1;

        // The builder's rows (0x43faf4): 0x15 for the walk and 0x16 for a figure byte from 0x80 (made up).
        bases[2 * 0x15] = 1000;
        bases[2 * 0x15 + 1] = -1;
        bases[2 * 0x16] = 1100;
        bases[2 * 0x16 + 1] = -1;

        // The digger's rows (0x43fa87): 0x17 for the walk and the dig and 0x18 for a figure byte from 0x80 (made up).
        bases[2 * 0x17] = 1200;
        bases[2 * 0x17 + 1] = -1;
        bases[2 * 0x18] = 1300;
        bases[2 * 0x18 + 1] = -1;

        // The sawmiller's rows (0x43fc21): 0x19 for the walk, 0x1a for the step that takes the plank out, and 0x1c for a figure byte
        // from 0x80 (made up).
        bases[2 * 0x19] = 1400;
        bases[2 * 0x19 + 1] = -1;
        bases[2 * 0x1a] = 1500;
        bases[2 * 0x1a + 1] = -1;
        bases[2 * 0x1c] = 1600;
        bases[2 * 0x1c + 1] = -1;

        // The miller's rows (0x4400d3): 0x1d for the walk, 0x1e for the step that takes the flour out, and 0x20 for a figure byte from
        // 0x80 (made up).
        bases[2 * 0x1d] = 1700;
        bases[2 * 0x1d + 1] = -1;
        bases[2 * 0x1e] = 1800;
        bases[2 * 0x1e + 1] = -1;
        bases[2 * 0x20] = 2000;
        bases[2 * 0x20 + 1] = -1;

        // The baker's, the butcher's, and the pig farmer's rows (0x440112, 0x43ff90, 0x43ff56): 0x21 for the walk, 0x22 for the step that takes the
        // bread, the meat, or the pig out, and 0x24 for a figure byte from
        // 0x80 (made up).
        bases[2 * 0x21] = 2100;
        bases[2 * 0x21 + 1] = -1;
        bases[2 * 0x22] = 2200;
        bases[2 * 0x22 + 1] = -1;
        bases[2 * 0x24] = 2400;
        bases[2 * 0x24 + 1] = -1;

        return TablesOf(
            bases,
            offsets,
            Sequences(),
            carried,
            Adds,
            HutAdds,
            Idle(),
            BuilderAdds,
            digger: DiggerAdds,
            sawmiller: SawmillerAdds,
            miller: MillerAdds,
            baker: BakerAdds,
            butcher: ButcherAdds,
            pigFarmer: PigFarmerAdds
        );
    }

    // The adds of the sawmiller's handler (0x43fc21), made up: 0x1900 for a figure byte below 0x80, 0x1a00 for the step that takes the
    // plank out, and 0x1b80 from 0x80 on.
    static readonly SawmillerFigureAdds SawmillerAdds = new()
    {
        Walk = 0x1900,
        Carrying = 0x1a00,
        Saw = 0x1b80,
    };

    // The adds of the miller's handler (0x4400d3), made up: 0x1d00 for a figure byte below 0x80, 0x1e00 for the step that takes the flour
    // out, and 0x1f80 from 0x80 on.
    static readonly FoodHutFigureAdds MillerAdds = new()
    {
        Walk = 0x1d00,
        Carrying = 0x1e00,
        Work = 0x1f80,
    };

    // The adds of the baker's handler (0x440112), made up: 0x2100 for a figure byte below 0x80, 0x2200 for the step that takes the bread
    // out, and 0x2380 from 0x80 on.
    static readonly FoodHutFigureAdds BakerAdds = new()
    {
        Walk = 0x2100,
        Carrying = 0x2200,
        Work = 0x2380,
    };

    // The adds of the butcher's handler (0x43ff90), made up: 0x2100 for a figure byte below 0x80, 0x2200 for the step that takes the meat
    // out, and 0x2380 from 0x80 on.
    static readonly FoodHutFigureAdds ButcherAdds = new()
    {
        Walk = 0x2100,
        Carrying = 0x2200,
        Work = 0x2380,
    };

    // The adds of the pig farmer's handler (0x43ff56), made up: 0x2100 for a figure byte below 0x80, 0x2200 for the step that takes the pig
    // out, and 0x2380 from 0x80 on.
    static readonly FoodHutFigureAdds PigFarmerAdds = new()
    {
        Walk = 0x2100,
        Carrying = 0x2200,
        Work = 0x2380,
    };

    // The adds of the digger's handler (0x43fa87), made up: 0x1700 for a figure byte below 0x80 and 0x1780 from there on.
    static readonly FigureAdds DiggerAdds = new()
    {
        Walk = 0x1700,
        Carrying = 0x1700,
        Cutting = 0x1780,
    };

    // The waiting carriers' tables: the phase of the column c is 8 * c, the figure word of the index i is i (row 0), and the
    // road directions 0x09 (east and west) move the figure 3 right and 1 up.
    static (short[] Phases, short[] Figures, sbyte[] Offsets) Idle()
    {
        var offsets = new sbyte[128];
        offsets[2 * 0x09] = 3;
        offsets[2 * 0x09 + 1] = -1;

        return (
            [.. Enumerable.Range(0, 16).Select(c => (short)(8 * c))],
            [.. Enumerable.Range(0, 128).Select(i => (short)i)],
            offsets
        );
    }

    // A blank map whose node of the waiting carriers (4,3) has the roads given, and the lake mark where asked.
    static Map MapWithRoad(RoadSet roads, bool lakeMark = false)
    {
        var map = Blank();
        map.At(4, 3).Roads = roads;
        map.At(4, 3).LakeMark = lakeMark;

        return map;
    }

    static Map Blank() => new(MapSize.Offered[0]);

    // The terms of the settlers at the clock, and the picks the tables make for them.
    static List<SettlerDraw> Terms(IEnumerable<Settler> settlers, ushort clock = 0, Map? map = null) =>
        SettlerTerms.Of(Steps, map ?? Blank(), settlers, clock);

    static List<SettlerPick> Picks(SettlerFigureTables tables, IEnumerable<Settler> settlers, ushort clock = 0, Map? map = null) =>
        SettlerTerms.Picks(tables, Terms(settlers, clock, map));

    static int[] Entries(SettlerFigureTables tables, IEnumerable<Settler> settlers, ushort clock = 0, Map? map = null) =>
        SettlerTerms.Entries(tables, Terms(settlers, clock, map));

    // A job draws its figure through its handler (0x43f744 plus 8 for each job); a job the engine holds no handler for, such as the
    // castle's own settler or a settler without a job, draws nothing, even where it walks.
    [TestMethod]
    [DataRow(4)]
    [DataRow(21)]
    public void ASettlerOfAJobWithoutAFigureHandlerIsNotDrawn(int job)
    {
        var walker = Walker(7, 0);
        var other = new Settler(Job.Of(job), 0, 4, 3, 0, walker.Activity)
        {
            Frame = walker.Frame,
            Timer = walker.Timer,
            Stamp = 0,
        };
        var tables = Tables();

        Assert.IsNotEmpty(Picks(tables, [walker]), "a woodcutter in the same walk is drawn");
        Assert.IsEmpty(Picks(tables, [other]));
    }

    [TestMethod]
    public void AWalkingSettlerIsDrawnAsItsFigureItsColourOverlayAndItsSecondFigure()
    {
        var picks = Picks(Tables(), [Walker(7, 0)]);

        // Figure byte 3 at the end of the step: entry 1850 + 100 + 3, the colour overlay 2500 + 100 + 3, the second 3150 + 50 + 1.
        CollectionAssert.AreEqual(new[] { 1953, 2603, 3201 }, picks.Select(p => p.Entry).ToArray());
        Assert.AreEqual(-1, picks[0].Colour);
        Assert.AreEqual(0x40, picks[1].Colour, "the player's colour: 0x40 plus 4 for each player");
    }

    [TestMethod]
    public void TheSequenceAdvancesOneTripleForEachEightClockUnitsOfTimeLeft()
    {
        var tables = Tables();

        var entries = new[] { 0, 8, 16 }.Select(timer => Entries(tables, [Walker(7, timer)])[0]).ToArray();

        CollectionAssert.AreEqual(new[] { 1953, 1954, 1955 }, entries);
    }

    [TestMethod]
    public void TheTimeLeftRunsDownWithTheClockBetweenTheSettlersTurns()
    {
        var drawn = Entries(Tables(), [Walker(7, 16)], 8)[0];

        Assert.AreEqual(1954, drawn, "16 units left at the settler's turn, 8 gone since: the second triple");
    }

    // Confirmed (the draw runs of the carried goods): the carrier that hands a good over (state 0xe) is drawn as one on its
    // road is, the good's row added to the figure byte (0x43f8ae serves both states).
    [TestMethod]
    public void ACarrierHandingAGoodOverIsDrawnWithTheGoodInItsHands()
    {
        var entries = Entries(Tables(), [Carrier(mode: 7, state: 0xe)]);

        CollectionAssert.AreEqual(new[] { 2353, 3003 }, entries);
    }

    [TestMethod]
    public void TheFisherIsDrawnInTheRowsOfHisHandler()
    {
        var tables = Tables();

        var walk = Entries(tables, [Hut(7)]);
        var back = Entries(tables, [Hut(7, WayBack(true))]);
        var high = Entries(tables, [Hut(8, Cast(false))]);
        var highFacing = Entries(tables, [Hut(8, Cast(true))]);

        // Figure byte 3 in the row 0x11 (base 600), in the row 0x12 (700) on the way back; byte 0x81 makes the word 0x1301 in the
        // row 0x13 (800), or 0x1401 in the row 0x14 (900), with the offset 1.
        CollectionAssert.AreEqual(new[] { 2453, 3103 }, walk);
        CollectionAssert.AreEqual(new[] { 2553, 3203 }, back);
        CollectionAssert.AreEqual(new[] { 2651, 3301 }, high);
        CollectionAssert.AreEqual(new[] { 2751, 3401 }, highFacing);
    }

    [TestMethod]
    public void ACarrierOnItsRoadIsDrawnAsAWalkingSettlerIs()
    {
        Assert.AreEqual(1954, Entries(Tables(), [Walker(7, 16, state: 3)], 8)[0]);
    }

    // A carrier that waits off its node (states 0x42 to 0x45) leaves a mark on the node (0x43bc52), and the tile pass draws it
    // there (0x44d77e): the figure word of the phase of its node plus the clock, moved by the offsets of the node's road
    // directions.
    [TestMethod]
    [DataRow(0x42)]
    [DataRow(0x43)]
    [DataRow(0x44)]
    [DataRow(0x45)]
    public void AWaitingCarrierIsDrawnByTheTilePassOnItsNode(int state)
    {
        var map = MapWithRoad(RoadSet.Of(0).With(3));

        // The phase of the column 4 is 32; with the clock 40 the index is (32 + 40) >> 3 = 9, the figure word 9.
        var picks = Picks(Tables(), [Carrier(mode: 7, state: state)], 40, map);

        Assert.HasCount(2, picks, "the figure and its overlay: the carrier's row 0 has no second figure");
        Assert.AreEqual(1850 + 100 + 9, picks[0].Entry);
        Assert.AreEqual(2500 + 100 + 9, picks[1].Entry);
        Assert.AreEqual((3, -1), (picks[0].Dx, picks[0].Dy), "the offsets of the node's road directions");
    }

    // The phase is the node's column's (stand-in for the address of the node's record): the column 13 has the phase 104, and
    // with the clock 40 the index is (104 + 40) >> 3 = 18.
    [TestMethod]
    public void AWaitingCarriersPhaseIsItsColumns()
    {
        var map = Blank();
        map.At(13, 3).Roads = RoadSet.Of(0).With(3);
        var carrier = Carrier(mode: 7, state: 0x42);
        carrier.X = 13;

        Assert.AreEqual(1850 + 100 + 18, Entries(Tables(), [carrier], 40, map)[0]);
    }

    // A node with the lake mark draws the fixed figure word 0x203 with no offsets (0x44d77e).
    [TestMethod]
    public void AWaitingCarrierOnANodeWithBitSixDrawsTheFixedFigure()
    {
        var map = MapWithRoad(RoadSet.Of(0).With(3), lakeMark: true);

        var pick = Picks(Tables(), [Carrier(mode: 7, state: 0x42)], 40, map)[0];

        Assert.AreEqual(1850 + 400 + 3, pick.Entry);
        Assert.AreEqual((0, 0), (pick.Dx, pick.Dy));
    }

    // 0x43fb6b: the figure byte plus 0x0b00; plus 0x1000 where byte 13 is 0x80, byte 14 is 1, and the state is 0x10 (the way
    // back with the log); plus 0x0e80 for a figure byte from 0x80 (the felling).
    [TestMethod]
    public void AWoodcutterOnTheWayBackWithTheLogIsDrawnInTheRowOfTheLog()
    {
        var home = Walker(7, 0);
        home.Activity = WayBack(true);

        // Figure byte 3 in the row 0x10: 1850 + 200 + 3, the overlay 2500 + 200 + 3, no second figure.
        CollectionAssert.AreEqual(new[] { 2053, 2703 }, Entries(Tables(), [home]));
    }

    [TestMethod]
    public void AWoodcutterWhoseTreeIsGoneWalksBackInTheRowOfTheWalk()
    {
        var home = Walker(7, 0);
        home.Activity = WayBack(false);

        CollectionAssert.AreEqual(new[] { 1953, 2603, 3201 }, Entries(Tables(), [home]));
    }

    [TestMethod]
    public void AWoodcutterWithTheLogOutsideTheFreeWalkIsDrawnInTheRowOfTheWalk()
    {
        var home = Walker(7, 0, state: 4);

        CollectionAssert.AreEqual(new[] { 1953, 2603, 3201 }, Entries(Tables(), [home]));
    }

    [TestMethod]
    public void ABuilderWalkingAndAtWorkIsDrawnInTheRowsOfHisHandler()
    {
        var tables = Tables();
        Settler Builder(int frame, Activity activity) =>
            new(new Builder(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var walk = Entries(tables, [Builder(7, ActivityOf(2))]);
        var work = Entries(
            tables,
            [
                Builder(
                    8,
                    new Builds
                    {
                        Site = 1,
                        Blowing = true,
                        Materials = 0,
                        SecondFigure = false,
                        Blows = 8,
                    }
                ),
            ]
        );

        // Figure byte 3 in the row 0x15 (base 1000, the walk's constant 0x1500), figure byte 0x81 at work: the word 0x1580 + 0x81 = 0x1601,
        // the row 0x16 (base 1100) and the offset 1; no second figure.
        CollectionAssert.AreEqual(new[] { 2853, 3503 }, walk);
        CollectionAssert.AreEqual(new[] { 2951, 3601 }, work);
    }

    [TestMethod]
    public void ADiggerWalkingAndAtWorkIsDrawnInTheRowsOfHisHandler()
    {
        var tables = Tables();
        Settler Digger(int frame, Activity activity) =>
            new(new Digger(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var walk = Entries(tables, [Digger(7, ActivityOf(2))]);
        var work = Entries(
            tables,
            [
                Digger(
                    8,
                    new Digs
                    {
                        Pass = 15,
                        Target = 10,
                        Node = 6,
                        Phase = 1,
                    }
                ),
            ]
        );

        // Figure byte 3 in the row 0x17 (base 1200, the constant 0x1700), figure byte 0x81 in the work state: the word 0x1780 + 0x81 =
        // 0x1801, the row 0x18 (base 1300) and the offset 1; no second figure.
        CollectionAssert.AreEqual(new[] { 3053, 3703 }, walk);
        CollectionAssert.AreEqual(new[] { 3151, 3801 }, work);
    }

    [TestMethod]
    public void ASawmillerWalkingSawingAndCarryingAPlankOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsForALog()
    {
        var tables = Tables();
        Settler Sawmiller(int frame, Activity activity) =>
            new(new Sawmiller(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var walk = Entries(tables, [Sawmiller(7, ActivityOf(2))]);
        var saws = Entries(tables, [Sawmiller(8, new Saws { Sawing = true })]);
        var carries = Entries(
            tables,
            [Sawmiller(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Plank } })]
        );

        // Figure byte 3 in the row 0x19 (base 1400) for the walk and in the row 0x1a (base 1500) for the step with the plank, figure byte
        // 0x81 in the work state: the word 0x1b80 + 0x81 = 0x1c01, the row 0x1c (base 1600) and the offset 1; waiting inside draws nothing
        // (checked on the terms in src/MapView.Tests).
        CollectionAssert.AreEqual(new[] { 3253, 3903 }, walk);
        CollectionAssert.AreEqual(new[] { 3353, 4003 }, carries);
        CollectionAssert.AreEqual(new[] { 3451, 4101 }, saws);
    }

    [TestMethod]
    public void AMillerWalkingGrindingAndCarryingFlourOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsOrPauses()
    {
        var tables = Tables();
        Settler Miller(int frame, Activity activity) =>
            new(new Miller(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var walk = Entries(tables, [Miller(7, ActivityOf(2))]);
        var grinds = Entries(tables, [Miller(8, new Grinds { Phase = 1 })]);
        var carries = Entries(
            tables,
            [Miller(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Flour } })]
        );

        // Figure byte 3 in the row 0x1d (base 1700) for the walk and in the row 0x1e (base 1800) for the step with the flour, figure byte
        // 0x81 in the work state: the word 0x1f80 + 0x81 = 0x2001, the row 0x20 (base 2000) and the offset 1. Waiting for grain, or
        // pausing with the node free, he is not drawn (checked on the terms in src/MapView.Tests).
        CollectionAssert.AreEqual(new[] { 3553, 4203 }, walk);
        CollectionAssert.AreEqual(new[] { 3653, 4303 }, carries);
        CollectionAssert.AreEqual(new[] { 3851, 4501 }, grinds);
        Assert.AreEqual(3851, Entries(tables, [Miller(8, new Grinds { Phase = 3 })])[0]);
    }

    [TestMethod]
    public void ABakerWalkingBakingAndCarryingBreadOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsOrPauses()
    {
        var tables = Tables();
        Settler Baker(int frame, Activity activity) =>
            new(new Baker(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var walk = Entries(tables, [Baker(7, ActivityOf(2))]);
        var bakes = Entries(tables, [Baker(8, new Bakes { Phase = 1 })]);
        var carries = Entries(
            tables,
            [Baker(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Bread } })]
        );

        // Figure byte 3 in the row 0x21 (base 2100) for the walk and in the row 0x22 (base 2200) for the step with the bread, figure byte
        // 0x81 in the work state: the word 0x2380 + 0x81 = 0x2401, the row 0x24 (base 2400) and the offset 1. Waiting for flour, or
        // pausing with the node free, he is not drawn (checked on the terms in src/MapView.Tests).
        CollectionAssert.AreEqual(new[] { 3953, 4603 }, walk);
        CollectionAssert.AreEqual(new[] { 4053, 4703 }, carries);
        CollectionAssert.AreEqual(new[] { 4251, 4901 }, bakes);
    }

    [TestMethod]
    public void AButcherWalkingWorkingAndCarryingMeatOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsForAPig()
    {
        var tables = Tables();
        Settler Butcher(int frame, Activity activity) =>
            new(new Butcher(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var walk = Entries(tables, [Butcher(7, ActivityOf(2))]);
        var works = Entries(tables, [Butcher(8, new Butchers { Working = true })]);
        var carries = Entries(
            tables,
            [Butcher(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Meat } })]
        );

        // The same figure bytes as the miller's, in the butcher's rows: byte 3 in the row 0x21 (base 2100) for the walk and in the row 0x22
        // (base 2200) for the step with the meat, byte 0x81 in the work state: the word 0x2380 + 0x81 = 0x2401, the row 0x24 (base 2400).
        CollectionAssert.AreEqual(new[] { 3953, 4603 }, walk);
        CollectionAssert.AreEqual(new[] { 4053, 4703 }, carries);
        CollectionAssert.AreEqual(new[] { 4251, 4901 }, works);
    }

    [TestMethod]
    public void APigFarmerWalkingWorkingAndCarryingAPigOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsOrPauses()
    {
        var tables = Tables();
        Settler PigFarmer(int frame, Activity activity) =>
            new(new PigFarmer(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var walk = Entries(tables, [PigFarmer(7, ActivityOf(2))]);
        var works = Entries(tables, [PigFarmer(8, new TendsPigs { Phase = 1 })]);
        var carries = Entries(
            tables,
            [PigFarmer(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Pig } })]
        );

        // Figure byte 3 in the row 0x21 (base 2100) for the walk and in the row 0x22 (base 2200) for the step with the pig, figure byte
        // 0x81 in the work state: the word 0x2380 + 0x81 = 0x2401, the row 0x24 (base 2400) and the offset 1. He is drawn in each
        // stretch that works (1, 3, and 5); the stretches that wait or pause are checked on the terms in src/MapView.Tests.
        CollectionAssert.AreEqual(new[] { 3953, 4603 }, walk);
        CollectionAssert.AreEqual(new[] { 4053, 4703 }, carries);
        CollectionAssert.AreEqual(new[] { 4251, 4901 }, works);
        Assert.AreEqual(4251, Entries(tables, [PigFarmer(8, new TendsPigs { Phase = 3 })])[0]);
        Assert.AreEqual(4251, Entries(tables, [PigFarmer(8, new TendsPigs { Phase = 5 })])[0]);
    }

    [TestMethod]
    public void AWoodcutterWhoseSequenceHoldsAFigureByteFrom0x80IsDrawnInTheFellingRow()
    {
        var entries = Entries(Tables(), [Walker(8, 0, state: 0x11)]);

        // Figure byte 0x81: the word 0x0e80 + 0x81 = 0x0f01, so the row 0x0f and the offsets of the byte 1:
        // 1850 + 300 + 1, the overlay 2500 + 300 + 1, no second figure.
        CollectionAssert.AreEqual(new[] { 2151, 2801 }, entries);
    }

    [TestMethod]
    public void ASettlerOfAnotherJobDrawsNothing()
    {
        var jobless = new Settler(new Jobless(), 0, 4, 3, 0, ActivityOf(2)) { Frame = 7 };

        Assert.IsEmpty(Picks(Tables(), [jobless]));
    }

    [TestMethod]
    public void AFrameWithNoSequenceDrawsNothing()
    {
        // A term of a frame the sequences hold nothing for: the tables pick none, whatever the part.
        var tables = Tables();
        var terms = new[] { SettlerPart.Figure, SettlerPart.Overlay, SettlerPart.SecondFigure }.Select(part =>
            new SettlerDraw
            {
                Row = 0,
                X = 0,
                Y = 0,
                Part = part,
                Job = new Woodcutter(),
                Activity = ActivityOf(2),
                Heading = new Heading { Value = 0 },
                Frame = 9,
                Step = 0,
                Player = 0,
                Carried = Good.None,
            }
        );

        Assert.IsEmpty(SettlerTerms.Picks(tables, terms));
    }

    // The carried good's figure word is the walking figure byte plus the table's word for the good (0x43f8ae to 0x43f92a):
    // the table starts at 0x43f4b7 and its word holds the row in the high byte. **Inferred:** that the carrier's job (0)
    // with a good in byte 11 in state 3 is the only one drawn so.
    [TestMethod]
    public void ACarrierCarryingALogIsDrawnWithTheFigureByteOfTheLogAdded()
    {
        var entries = Entries(Tables(), [Carrier(mode: 7)]);

        // The word 0x0a00 adds the row 10 to the figure byte 3: entry 1850 + 500 + 3, and the colour overlay 2500 + 500 + 3;
        // the row has no second figure here.
        CollectionAssert.AreEqual(new[] { 2353, 3003 }, entries);
    }

    [TestMethod]
    public void ACarrierWithNoGoodIsDrawnAsBefore()
    {
        CollectionAssert.AreEqual(new[] { 1953, 2603 }, Entries(Tables(), [Carrier(mode: 0)]));
    }

    // The original draws the overlay of the figure 1850 + i from 2500 + i (0x406610), not from the figure's entry plus 2500.
    [TestMethod]
    public void TheColourOverlayOfTheFigureOneEightFiftyPlusIIsTwentyFiveHundredPlusI()
    {
        var bases = new short[256];
        bases[2 * 0x0b] = 40;
        bases[2 * 0x0b + 1] = -1;
        var offsets = new short[512];
        offsets[2 * 3] = 7;

        var tables = TablesOf(bases, offsets, Sequences(), null, Adds);
        var picks = Picks(tables, [Walker(7, 0)]);

        CollectionAssert.AreEqual(new[] { (1897, -1), (2547, 0x40) }, picks.Select(p => (p.Entry, p.Colour)).ToArray());
    }

    // The second figure moves by the header bytes 0 and 1 of the figure 1850 + i, read as signed bytes (0x4066d2, 0x4066e4);
    // the figure and the overlay stay where they are. The pick names the figure whose header shifts the second figure.
    [TestMethod]
    public void TheSecondFigureIsShiftedByTheFiguresHeaderBytesAndANegativeByteMovesItBack()
    {
        var picks = Picks(Tables(), [Walker(7, 0)]);
        var shifts = new Dictionary<int, (int X, int Y)> { [1953] = (3, -2) };

        Assert.AreEqual(1953, picks[2].ShiftedBy, "the figure's entry");
        Assert.AreEqual((0, 0), SettlerFigureShift.Of(picks[0], shifts));
        Assert.AreEqual((0, 0), SettlerFigureShift.Of(picks[1], shifts));
        Assert.AreEqual((3, -2), SettlerFigureShift.Of(picks[2], shifts));
    }

    [TestMethod]
    public void AFigureWithNoHeaderLeavesTheSecondFigureUnshifted()
    {
        var picks = Picks(Tables(), [Walker(7, 0)]);

        Assert.AreEqual((0, 0), SettlerFigureShift.Of(picks[2], new Dictionary<int, (int X, int Y)>()));
        Assert.AreEqual((0, 0), SettlerFigureShift.Of(picks[2], null));
    }

    [TestMethod]
    public void AShiftsMapThatLacksTheFiguresEntryLeavesTheSecondFigureUnshifted()
    {
        var picks = Picks(Tables(), [Walker(7, 0)]);

        Assert.AreEqual(
            (0, 0),
            SettlerFigureShift.Of(picks[2], new Dictionary<int, (int X, int Y)> { [1954] = (3, -2) })
        );
        Assert.AreEqual(
            (3, -2),
            SettlerFigureShift.Of(picks[2], new Dictionary<int, (int X, int Y)> { [1953] = (3, -2) })
        );
    }

    // 0x406610 draws the figure, then the overlay over it, then the second figure over both; the row order of the settlers
    // keeps that order within each settler.
    [TestMethod]
    public void EachSettlerDrawsItsFigureThenItsOverlayThenItsSecondFigureWithSeveralSettlersOnARow()
    {
        var settlers = Enumerable.Range(0, 40).Select(_ => Walker(7, 0)).ToArray();

        var entries = Entries(Tables(), settlers);

        CollectionAssert.AreEqual(
            Enumerable.Range(0, 40).SelectMany(_ => new[] { 1953, 2603, 3201 }).ToArray(),
            entries
        );
    }

    [TestMethod]
    public void AJobWithNoSecondFigureDrawsItsFigureAndOverlay()
    {
        var picks = Picks(Tables(), [Carrier(mode: 0)]);

        CollectionAssert.AreEqual(new[] { 1953, 2603 }, picks.Select(p => p.Entry).ToArray());
        Assert.IsTrue(picks.All(p => p.ShiftedBy == -1));
    }

    [TestMethod]
    public void AFigureByteOutsideTheTablesDrawsNothing()
    {
        var bases = new short[256];
        bases[2 * 0x0b + 1] = -1;

        var tables = TablesOf(bases, new short[4], Sequences(), null, Adds);

        Assert.IsEmpty(Picks(tables, [Walker(7, 0)]));
    }

    // A term with no image in the tables draws nothing, and the pick does not throw.
    [TestMethod]
    public void ATermTheTablesHaveNoPickForDrawsNothingAndDoesNotThrow()
    {
        var tables = TablesOf(new short[256], new short[4], Sequences(), null, Adds);
        var term = Terms([Walker(7, 0)])[0];

        Assert.IsNull(tables.Pick(term));
    }

    // The jobs the content set must draw (SettlerCases.JobsWithAFigureHandler, which the open settlers source's checks in
    // src/Launch.Tests read) are exactly the jobs the tables hold a figure handler for.
    [TestMethod]
    public void TheJobsTheTablesHoldAFigureHandlerForAreTheJobsTheContentSetIsCheckedAgainst()
    {
        var tables = TablesOf([], [], []);

        var handled = Enumerable.Range(0, 32).Select(Job.Of).Where(tables.HasHandler).ToArray();

        CollectionAssert.AreEquivalent(JobsWithAFigureHandler.ToArray(), handled);
    }
}
