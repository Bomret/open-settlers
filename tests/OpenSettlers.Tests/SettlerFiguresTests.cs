using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The selection of a walking settler's sprites on made-up tables and a made-up walk sequence (no install). The shape of
// the sequences (entry 2 of the graphics archive), the tables, and the use of the timer follow docs/rules/settlers.md;
// the values are made up here, and the rule's own details are marked inferred there.
[TestClass]
public sealed class SettlerFiguresTests
{
    // Entry 2: a dword, then 200 big-endian offsets from the first byte after that dword. The sequence of frame 7 holds
    // three triples (figure byte, dx, dy): (3, 1, 2), (4, -1, 0), (5, 0, 0); the sequence of frame 8 holds (0x81, 0, 0), a
    // figure byte from 0x80 as the cut's frames hold.
    private static byte[] Sequences()
    {
        var data = new byte[4 + 200 * 4 + 12];
        var at = 4 + 200 * 4;
        for (var i = 0; i < 12; i++)
        {
            data[at + i] = new byte[] { 3, 1, 2, 4, 0xff, 0, 5, 0, 0, 0x81, 0, 0 }[i];
        }

        var offset = at - 4;
        data[4 + 7 * 4 + 2] = (byte)(offset >> 8);
        data[4 + 7 * 4 + 3] = (byte)offset;
        data[4 + 8 * 4 + 2] = (byte)((offset + 9) >> 8);
        data[4 + 8 * 4 + 3] = (byte)(offset + 9);

        return data;
    }

    // The adds of the woodcutter's handler (0x43fb6b): the high byte of the figure word is 0x0b for the walk, 0x10 for the
    // way back with the log, and 0x0f for a figure byte from 0x80.
    private static readonly FigureAdds Adds = new(0x0b00, 0x1000, 0x0e80);

    // The adds of the handler of the settler of the type 1 hut (0x43fed8), with made-up rows: 0x11 for the walk, 0x12 for the
    // way back with a load, 0x13 and 0x14 for a figure byte from 0x80.
    private static readonly HutFigureAdds HutAdds = new(0x1100, 0x1200, 0x1280, 0x1380);

    // The woodcutter: the figure base 100 and the second base 50 in the row 0x0b, 200 and none in the row 0x10, 300 and
    // none in the row 0x0f; each figure byte adds its own number and, to the second, 1. The carrier's row 0: 100, none.
    private static SettlerFigures Figures(sbyte[]? frameRows = null)
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

        return new SettlerFigures(bases, offsets, Sequences(), carried, Adds, frameRows, HutAdds, Idle());
    }

    // The waiting carriers' tables: the phase of the column c is 8 * c, the figure word of the index i is i (row 0), and the
    // road directions 0x09 (east and west) move the figure 3 right and 1 up.
    private static IdleCarrierTables Idle()
    {
        var offsets = new sbyte[128];
        offsets[2 * 0x09] = 3;
        offsets[2 * 0x09 + 1] = -1;

        return new IdleCarrierTables(
            [.. Enumerable.Range(0, 16).Select(c => (short)(8 * c))],
            [.. Enumerable.Range(0, 128).Select(i => (short)i)],
            offsets
        );
    }

    // A map whose node of the waiting carriers (4,3) has the roads given, and the lake mark where asked.
    private static Map MapWithRoad(RoadSet roads, bool lakeMark = false)
    {
        var map = Map();
        map.At(4, 3).Roads = roads;
        map.At(4, 3).LakeMark = lakeMark;

        return map;
    }

    private static Settler Carrier(int mode, int state = 3, int timer = 0) =>
        new(0, 0, 4, 3, 0, state, 0)
        {
            Frame = 7,
            Timer = (ushort)timer,
            Mode = mode,
        };

    private static Map Map() =>
        MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);

    private static Settler Walker(int frame, int timer, int state = 2) =>
        new(Settler.WoodcutterJob, 0, 4, 3, 0, state, 0)
        {
            Frame = frame,
            Timer = (ushort)timer,
            Stamp = 0,
        };

    [TestMethod]
    public void AWalkingSettlerIsDrawnAsItsFigureItsColourOverlayAndItsSecondFigure()
    {
        var draws = Figures().Draws(Map(), [Walker(7, 0)], 0);

        // Figure byte 3 at the end of the step: entry 1850 + 100 + 3, the colour overlay 2500 + 100 + 3, the second 3150 + 50 + 1.
        CollectionAssert.AreEqual(new[] { 1953, 2603, 3201 }, draws.Select(d => d.Entry).ToArray());
        Assert.AreEqual(-1, draws[0].Colour);
        Assert.AreEqual(0x40, draws[1].Colour, "the player's colour: 0x40 plus 4 for each player");
    }

    [TestMethod]
    public void TheSequenceAdvancesOneTripleForEachEightClockUnitsOfTimeLeft()
    {
        var figures = Figures();

        var entries = new[] { 0, 8, 16 }
            .Select(timer => figures.Draws(Map(), [Walker(7, timer)], 0)[0].Entry)
            .ToArray();

        CollectionAssert.AreEqual(new[] { 1953, 1954, 1955 }, entries);
    }

    [TestMethod]
    public void TheOffsetsOfATripleMoveTheFigureAlongTheStep()
    {
        var figures = Figures();

        var first = figures.Draws(Map(), [Walker(7, 0)], 0)[0];
        var second = figures.Draws(Map(), [Walker(7, 8)], 0)[0];

        Assert.AreEqual(-2, second.X - first.X, "the dx of the triples are 1 and -1");
        Assert.AreEqual(-2, second.Y - first.Y);
    }

    [TestMethod]
    public void TheTimeLeftRunsDownWithTheClockBetweenTheSettlersTurns()
    {
        var figures = Figures();

        var drawn = figures.Draws(Map(), [Walker(7, 16)], 8)[0].Entry;

        Assert.AreEqual(1954, drawn, "16 units left at the settler's turn, 8 gone since: the second triple");
    }

    [TestMethod]
    public void OnlyASettlerOnTheRoadIsDrawn()
    {
        var figures = Figures();

        Assert.IsEmpty(figures.Draws(Map(), [Walker(7, 0, state: 1), Walker(7, 0, state: 0x12)], 0));
        Assert.HasCount(3, figures.Draws(Map(), [Walker(7, 0, state: 5)], 0));
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(0x10)]
    [DataRow(0x11)]
    public void AWoodcutterStepsThroughItsDoorWalksFreeAndCutsDrawnAsAWalkingSettlerIs(int state)
    {
        Assert.HasCount(3, Figures().Draws(Map(), [Walker(7, 0, state: state)], 0));
    }

    // Confirmed (draw runs walk-home, dismissed, dismissed-with-log): a settler that searches for the nearest flag (0x19) on its
    // way home is drawn as a walking settler is.
    [TestMethod]
    public void ASettlerSearchingForAFlagOnItsWayHomeIsDrawnAsAWalkingSettlerIs()
    {
        Assert.HasCount(3, Figures().Draws(Map(), [Walker(7, 0, state: 0x19)], 0));
    }

    // Confirmed (the same runs): a settler that claims its node (0x1c) on its way home, and a settler in the states 0x44 and
    // 0x45, have no record of the settler's draw path. A carrier in 0x44 or 0x45 is drawn by the tile pass instead
    // (AWaitingCarrierIsDrawnByTheTilePassOnItsNode).
    [TestMethod]
    [DataRow(0x1c)]
    [DataRow(0x44)]
    [DataRow(0x45)]
    public void ASettlerThatClaimsItsNodeOrWaitsToGoHomeIsNotDrawn(int state)
    {
        Assert.IsEmpty(Figures().Draws(Map(), [Walker(7, 0, state: state)], 0));
    }

    // Confirmed (the draw runs of the carried goods): the carrier that hands a good over (state 0xe) is drawn as one on its
    // road is, the good's row added to the figure byte (0x43f8ae serves both states).
    [TestMethod]
    public void ACarrierHandingAGoodOverIsDrawnWithTheGoodInItsHands()
    {
        var draws = Figures().Draws(Map(), [Carrier(mode: 7, state: 0xe)], 0);

        CollectionAssert.AreEqual(new[] { 2353, 3003 }, draws.Select(d => d.Entry).ToArray());
    }

    // The handler of the settler of the type 1 hut (0x43fed8) adds, for a figure byte below 0x80, 0x2c00 or 0x2f00 on the way back
    // with a load and, from 0x80, 0x2c80 or, where the heading (byte 14) is 1, 0x2d80; the rows here are made up.
    private static Settler Hut(int frame, int state = 2, int heading = 0, int mark = 0) =>
        new(RuleTables.TypeOneHutJob, 0, 4, 3, 0, state, 0)
        {
            Frame = frame,
            Heading = heading,
            Target = mark << 8,
        };

    [TestMethod]
    public void TheSettlerOfTheTypeOneHutIsDrawnInTheRowsOfHisHandler()
    {
        var figures = Figures();
        var map = Map();

        var walk = figures.Draws(map, [Hut(7)], 0).Select(d => d.Entry).ToArray();
        var back = figures.Draws(map, [Hut(7, 0x10, 1, 0x80)], 0).Select(d => d.Entry).ToArray();
        var high = figures.Draws(map, [Hut(8, 0x11, 0)], 0).Select(d => d.Entry).ToArray();
        var highFacing = figures.Draws(map, [Hut(8, 0x11, 1)], 0).Select(d => d.Entry).ToArray();

        // Figure byte 3 in the row 0x11 (base 600), in the row 0x12 (700) on the way back; byte 0x81 makes the word 0x1301 in the
        // row 0x13 (800), or 0x1401 in the row 0x14 (900), with the offset 1.
        CollectionAssert.AreEqual(new[] { 2453, 3103 }, walk);
        CollectionAssert.AreEqual(new[] { 2553, 3203 }, back);
        CollectionAssert.AreEqual(new[] { 2651, 3301 }, high);
        CollectionAssert.AreEqual(new[] { 2751, 3401 }, highFacing);
    }

    [TestMethod]
    public void TheSpritesOfTheSettlerOfTheTypeOneHutAreThoseOfEveryTripleOfItsSequencesInTheRowsOfHisHandler()
    {
        var entries = Figures().Sprites(RuleTables.TypeOneHutJob, 0x40).Select(s => s.Entry).Distinct().ToArray();

        CollectionAssert.IsSubsetOf(new[] { 2453, 2553, 2651, 2751, 3103, 3203, 3301, 3401 }, entries);
    }

    [TestMethod]
    public void ACarrierOnItsRoadIsDrawnAsAWalkingSettlerIs()
    {
        var figures = Figures();

        Assert.HasCount(3, figures.Draws(Map(), [Walker(7, 0, state: 3)], 0));
        Assert.AreEqual(1954, figures.Draws(Map(), [Walker(7, 16, state: 3)], 8)[0].Entry);
    }

    // Confirmed by the draw run (settler-draws.txt): the settler's draw path has no record for a settler in state 0x42 (the
    // carrier's handler returns at once). A settler of another job in that state draws nothing.
    [TestMethod]
    public void ASettlerOfAnotherJobInTheWaitingStateIsNotDrawn()
    {
        Assert.IsEmpty(Figures().Draws(Map(), [Walker(7, 8, state: 0x42)], 800));
    }

    // A carrier that waits off its node (states 0x42 to 0x45) leaves a mark on the node (0x43bc52), and the tile pass draws it
    // there (0x44d77e): the figure word of the phase of its node plus the clock, moved by the offsets of the node's road
    // directions, at the node lifted by 4 times its height, with no lift of 2.
    [TestMethod]
    [DataRow(0x42)]
    [DataRow(0x43)]
    [DataRow(0x44)]
    [DataRow(0x45)]
    public void AWaitingCarrierIsDrawnByTheTilePassOnItsNode(int state)
    {
        var map = MapWithRoad(RoadSet.Of(0).With(3));
        var height = map.At(4, 3).Height;
        var (nodeX, nodeY) = Camera.NodePosition(4, 3);

        // The phase of the column 4 is 32; with the clock 40 the index is (32 + 40) >> 3 = 9, the figure word 9.
        var draws = Figures().Draws(map, [Carrier(mode: 7, state: state)], 40);

        Assert.HasCount(2, draws, "the figure and its overlay: the carrier's row 0 has no second figure");
        Assert.AreEqual(1850 + 100 + 9, draws[0].Entry);
        Assert.AreEqual(2500 + 100 + 9, draws[1].Entry);
        Assert.AreEqual((nodeX + 3, nodeY - 4 * height - 1), (draws[0].X, draws[0].Y));
    }

    // The phase is the node's column's (stand-in for the address of the node's record): the column 13 has the phase 104, and
    // with the clock 40 the index is (104 + 40) >> 3 = 18.
    [TestMethod]
    public void AWaitingCarriersPhaseIsItsColumns()
    {
        var map = Map();
        map.At(13, 3).Roads = RoadSet.Of(0).With(3);
        var carrier = Carrier(mode: 7, state: 0x42);
        carrier.X = 13;

        Assert.AreEqual(1850 + 100 + 18, Figures().Draws(map, [carrier], 40)[0].Entry);
    }

    // A node with the lake mark draws the fixed figure word 0x203 with no offsets (0x44d77e).
    [TestMethod]
    public void AWaitingCarrierOnANodeWithBitSixDrawsTheFixedFigure()
    {
        var map = MapWithRoad(RoadSet.Of(0).With(3), lakeMark: true);
        var height = map.At(4, 3).Height;
        var (nodeX, nodeY) = Camera.NodePosition(4, 3);

        var draw = Figures().Draws(map, [Carrier(mode: 7, state: 0x42)], 40)[0];

        Assert.AreEqual(1850 + 400 + 3, draw.Entry);
        Assert.AreEqual((nodeX, nodeY - 4 * height), (draw.X, draw.Y));
    }

    // 0x44d3ae draws a row's settler records, then the tile pass's records of that row.
    [TestMethod]
    public void AWaitingCarrierIsDrawnAfterTheWalkingSettlersOfItsRow()
    {
        var walker = Walker(7, 0);
        walker.X = 6;

        var draws = Figures().Draws(MapWithRoad(RoadSet.Of(0).With(3)), [Carrier(mode: 7, state: 0x42), walker], 40);

        Assert.AreEqual(1850 + 100 + 9, draws[^2].Entry);
    }

    // The row of a record is the settler's row plus the byte of its frame's row (0x43f537); a list holds its records in the order
    // of the original's scan of the nodes, which is the settlers' row, then column.
    [TestMethod]
    public void ARecordJoinsTheRowItsFramesByteMovesItTo()
    {
        var rows = new sbyte[16];
        rows[8] = -2;
        // The settler of the frame 8 (the felling) stands in row 4, the one of the frame 7 in row 3; the frame's byte
        // puts the first in row 2, so it is drawn first.
        var felling = Walker(8, 0, state: 0x11);
        felling.Y = 4;

        var draws = Figures(rows).Draws(Map(), [Walker(7, 0), felling], 0);

        Assert.AreEqual(2151, draws[0].Entry, "the felling: 1850 + 300 + 1");
        Assert.AreEqual(2, draws[0].Row);
        Assert.AreEqual(3, draws[^1].Row);
    }

    [TestMethod]
    public void TheRecordsOfOneRowAreInTheOrderOfTheNodesFromTheWest()
    {
        var east = Walker(7, 0);
        east.X = 6;
        var west = Walker(7, 0);
        west.X = 2;

        var draws = Figures().Draws(Map(), [east, west], 0);

        Assert.IsLessThan(draws[3].X, draws[0].X, "the settler at column 2 first");
    }

    // 0x43f537: the figure is 4 times the node's height and 2 more pixels above the node, plus the triple's offsets.
    [TestMethod]
    public void ASettlerIsLiftedByTheHeightOfItsNodeAndTwoPixels()
    {
        var map = Map();
        var settler = Walker(7, 0);
        var height = map.At(settler.X, settler.Y).Height;
        var (nodeX, nodeY) = Camera.NodePosition(settler.X, settler.Y);

        var draw = Figures().Draws(map, [settler], 0)[0];

        Assert.AreEqual(nodeX + 1, draw.X, "the triple's dx is 1");
        Assert.AreEqual(nodeY + 2 - 4 * height - 2, draw.Y, "the triple's dy is 2");
    }

    [TestMethod]
    public void TheSpritesOfTheWoodcutterAreThoseOfEveryTripleOfItsSequencesInTheRowsOfHisHandler()
    {
        var sprites = Figures().Sprites(Settler.WoodcutterJob, 0x40).OrderBy(s => s.Entry).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                (1953, -1),
                (1954, -1),
                (1955, -1),
                (2053, -1),
                (2054, -1),
                (2055, -1),
                (2151, -1),
                (2603, 0x40),
                (2604, 0x40),
                (2605, 0x40),
                (2703, 0x40),
                (2704, 0x40),
                (2705, 0x40),
                (2801, 0x40),
                (3201, -1),
            },
            sprites.Distinct().ToArray()
        );
    }

    // 0x43fb6b: the figure byte plus 0x0b00; plus 0x1000 where byte 13 is 0x80, byte 14 is 1, and the state is 0x10 (the way
    // back with the log); plus 0x0e80 for a figure byte from 0x80 (the felling).
    [TestMethod]
    public void AWoodcutterOnTheWayBackWithTheLogIsDrawnInTheRowOfTheLog()
    {
        var home = Walker(7, 0, state: 0x10);
        home.Target = 0x8000;
        home.Heading = 1;

        var draws = Figures().Draws(Map(), [home], 0);

        // Figure byte 3 in the row 0x10: 1850 + 200 + 3, the overlay 2500 + 200 + 3, no second figure.
        CollectionAssert.AreEqual(new[] { 2053, 2703 }, draws.Select(d => d.Entry).ToArray());
    }

    [TestMethod]
    public void AWoodcutterWhoseTreeIsGoneWalksBackInTheRowOfTheWalk()
    {
        var home = Walker(7, 0, state: 0x10);
        home.Target = 0x8000;
        home.Heading = 0;

        CollectionAssert.AreEqual(
            new[] { 1953, 2603, 3201 },
            Figures().Draws(Map(), [home], 0).Select(d => d.Entry).ToArray()
        );
    }

    [TestMethod]
    public void AWoodcutterWithTheLogOutsideTheFreeWalkIsDrawnInTheRowOfTheWalk()
    {
        var home = Walker(7, 0, state: 4);
        home.Target = 0x8000;
        home.Heading = 1;

        CollectionAssert.AreEqual(
            new[] { 1953, 2603, 3201 },
            Figures().Draws(Map(), [home], 0).Select(d => d.Entry).ToArray()
        );
    }

    [TestMethod]
    public void AWoodcutterWhoseSequenceHoldsAFigureByteFrom0x80IsDrawnInTheFellingRow()
    {
        var draws = Figures().Draws(Map(), [Walker(8, 0, state: 0x11)], 0);

        // Figure byte 0x81: the word 0x0e80 + 0x81 = 0x0f01, so the row 0x0f and the offsets of the byte 1:
        // 1850 + 300 + 1, the overlay 2500 + 300 + 1, no second figure.
        CollectionAssert.AreEqual(new[] { 2151, 2801 }, draws.Select(d => d.Entry).ToArray());
    }

    [TestMethod]
    public void ASettlerOfAnotherJobDrawsNothing()
    {
        var jobless = new Settler(21, 0, 4, 3, 0, 2, 0) { Frame = 7 };

        Assert.IsEmpty(Figures().Draws(Map(), [jobless], 0));
    }

    [TestMethod]
    public void AFrameWithNoSequenceDrawsNothing()
    {
        Assert.IsEmpty(Figures().Draws(Map(), [Walker(9, 0)], 0));
    }

    // The carried good's figure word is the walking figure byte plus the table's word for the good (0x43f8ae to 0x43f92a):
    // the table starts at 0x43f4b7 and its word holds the row in the high byte. **Inferred:** that the carrier's job (0)
    // with a good in byte 11 in state 3 is the only one drawn so.
    [TestMethod]
    public void ACarrierCarryingALogIsDrawnWithTheFigureByteOfTheLogAdded()
    {
        var draws = Figures().Draws(Map(), [Carrier(mode: 7)], 0);

        // The word 0x0a00 adds the row 10 to the figure byte 3: entry 1850 + 500 + 3, and the colour overlay 2500 + 500 + 3;
        // the row has no second figure here.
        CollectionAssert.AreEqual(new[] { 2353, 3003 }, draws.Select(d => d.Entry).ToArray());
    }

    [TestMethod]
    public void ACarrierWithNoGoodIsDrawnAsBefore()
    {
        var draws = Figures().Draws(Map(), [Carrier(mode: 0)], 0);

        CollectionAssert.AreEqual(new[] { 1953, 2603 }, draws.Select(d => d.Entry).ToArray());
    }

    [TestMethod]
    public void TheSpritesOfTheCarrierIncludeThoseItCarriesALog()
    {
        var entries = Figures().Sprites(0, 0x40, carriedGood: 7).Select(s => s.Entry).Distinct().ToArray();

        CollectionAssert.IsSubsetOf(new[] { 2353, 2354, 2355, 3003 }, entries);
        CollectionAssert.Contains(entries, 2353);
    }

    // What the carrier is drawn with is among what Sprites loads: both apply the one rule, for every frame and timer.
    [TestMethod]
    public void EveryEntryACarrierCarryingALogIsDrawnWithIsAmongTheSpritesLoadedForIt()
    {
        var figures = Figures();
        var loaded = figures.Sprites(Settler.CarrierJob, 0x40, carriedGood: 7).ToHashSet();
        var drawn = new[] { 0, 8, 16 }
            .SelectMany(timer => figures.Draws(Map(), [Carrier(7, timer: timer)], 0).Select(d => (d.Entry, d.Colour)))
            .ToHashSet();

        Assert.IsNotEmpty(drawn);
        Assert.IsTrue(drawn.IsSubsetOf(loaded), string.Join(", ", drawn.Except(loaded)));
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

        var draws = new SettlerFigures(bases, offsets, Sequences(), null, Adds).Draws(Map(), [Walker(7, 0)], 0);

        CollectionAssert.AreEqual(new[] { (1897, -1), (2547, 0x40) }, draws.Select(d => (d.Entry, d.Colour)).ToArray());
    }

    // The second figure moves by the header bytes 0 and 1 of the figure 1850 + i, read as signed bytes (0x4066d2, 0x4066e4);
    // the figure and the overlay stay where they are.
    [TestMethod]
    public void TheSecondFigureIsShiftedByTheFiguresHeaderBytesAndANegativeByteMovesItBack()
    {
        var plain = Figures().Draws(Map(), [Walker(7, 0)], 0);
        var shifted = Figures()
            .Draws(Map(), [Walker(7, 0)], 0, new Dictionary<int, (int X, int Y)> { [1953] = (3, -2) });

        CollectionAssert.AreEqual(plain.Take(2).ToArray(), shifted.Take(2).ToArray());
        Assert.AreEqual((plain[2].X + 3, plain[2].Y - 2), (shifted[2].X, shifted[2].Y));
    }

    [TestMethod]
    public void AFigureWithNoHeaderLeavesTheSecondFigureUnshifted()
    {
        var plain = Figures().Draws(Map(), [Walker(7, 0)], 0);
        var shifted = Figures().Draws(Map(), [Walker(7, 0)], 0, new Dictionary<int, (int X, int Y)>());

        CollectionAssert.AreEqual(plain, shifted);
    }

    [TestMethod]
    public void AShiftsMapThatLacksTheFiguresEntryLeavesTheSecondFigureUnshifted()
    {
        var plain = Figures().Draws(Map(), [Walker(7, 0)], 0);
        var others = Figures()
            .Draws(Map(), [Walker(7, 0)], 0, new Dictionary<int, (int X, int Y)> { [1954] = (3, -2) });
        var own = Figures().Draws(Map(), [Walker(7, 0)], 0, new Dictionary<int, (int X, int Y)> { [1953] = (3, -2) });

        CollectionAssert.AreEqual(plain, others);
        CollectionAssert.AreNotEqual(plain, own);
    }

    // 0x406610 draws the figure, then the overlay over it, then the second figure over both; the row order of the settlers
    // keeps that order within each settler.
    [TestMethod]
    public void EachSettlerDrawsItsFigureThenItsOverlayThenItsSecondFigureWithSeveralSettlersOnARow()
    {
        var settlers = Enumerable.Range(0, 40).Select(_ => Walker(7, 0)).ToArray();

        var entries = Figures().Draws(Map(), settlers, 0).Select(d => d.Entry).ToArray();

        CollectionAssert.AreEqual(
            Enumerable.Range(0, 40).SelectMany(_ => new[] { 1953, 2603, 3201 }).ToArray(),
            entries
        );
    }

    [TestMethod]
    public void AJobWithNoSecondFigureDrawsItsFigureAndOverlay()
    {
        var draws = Figures()
            .Draws(Map(), [Carrier(mode: 0)], 0, new Dictionary<int, (int X, int Y)> { [1953] = (3, 3) });

        CollectionAssert.AreEqual(new[] { 1953, 2603 }, draws.Select(d => d.Entry).ToArray());
    }

    [TestMethod]
    public void AFigureByteOutsideTheTablesDrawsNothing()
    {
        var bases = new short[256];
        bases[2 * 0x0b + 1] = -1;

        Assert.IsEmpty(
            new SettlerFigures(bases, new short[4], Sequences(), null, Adds).Draws(Map(), [Walker(7, 0)], 0)
        );
    }
}
