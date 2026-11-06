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

    // The woodcutter: the figure base 100 and the second base 50 in the row 0x0b, 200 and none in the row 0x10, 300 and
    // none in the row 0x0f; each figure byte adds its own number and, to the second, 1. The carrier's row 0: 100, none.
    private static SettlerFigures Figures()
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

        return new SettlerFigures(bases, offsets, Sequences(), carried, Adds);
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

    // Inferred: a settler that claims its node (0x1c) or searches for the nearest flag (0x19) stands with the frame 0x52 the
    // claim sets and is drawn as a waiting settler is; no draw run confirms it.
    [TestMethod]
    [DataRow(0x19)]
    [DataRow(0x1c)]
    public void ASettlerOnItsWayHomeIsDrawnAsAWalkingSettlerIs(int state)
    {
        Assert.HasCount(3, Figures().Draws(Map(), [Walker(7, 0, state: state)], 0));
    }

    // Inferred: a carrier whose road was taken down or joined (0x44, 0x45) still stands next to the flag and is drawn as the carrier
    // that waits (0x42); no draw run confirms it.
    [TestMethod]
    [DataRow(0x44)]
    [DataRow(0x45)]
    public void ADismissedCarrierIsDrawnAsTheCarrierThatWaitsIs(int state)
    {
        var waiting = Figures().Draws(Map(), [Carrier(mode: 7, state: 0x42)], 0);
        var dismissed = Figures().Draws(Map(), [Carrier(mode: 7, state: state)], 0);

        Assert.HasCount(waiting.Count, dismissed);
        Assert.AreEqual(waiting[0].Entry, dismissed[0].Entry);
    }

    [TestMethod]
    public void ACarrierOnItsRoadIsDrawnAsAWalkingSettlerIs()
    {
        var figures = Figures();

        Assert.HasCount(3, figures.Draws(Map(), [Walker(7, 0, state: 3)], 0));
        Assert.AreEqual(1954, figures.Draws(Map(), [Walker(7, 16, state: 3)], 8)[0].Entry);
    }

    // Inferred: that the original draws a carrier that waits next to a flag (state 0x42) with the timer it holds; no draw
    // run confirms it (open lead in docs/milestones/m3-first-loop.md).
    [TestMethod]
    public void AWaitingCarrierIsDrawnWithTheTimerItHoldsAndTheClockDoesNotRunItDown()
    {
        var figures = Figures();

        var drawn = figures.Draws(Map(), [Walker(7, 8, state: 0x42)], 800);

        Assert.HasCount(3, drawn);
        Assert.AreEqual(1954, drawn[0].Entry, "the second triple: 8 units left, however much clock has passed");
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
    public void AWaitingCarrierDoesNotCarryItsDirectionAsAGood()
    {
        var draws = Figures().Draws(Map(), [Carrier(mode: 7, state: 0x42)], 0);

        Assert.AreEqual(1953, draws[0].Entry, "in state 0x42 byte 11 is the road direction");
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
