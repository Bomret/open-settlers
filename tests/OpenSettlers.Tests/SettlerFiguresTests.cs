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
    // three triples (figure byte, dx, dy): (3, 1, 2), (4, -1, 0), (5, 0, 0).
    private static byte[] Sequences()
    {
        var data = new byte[4 + 200 * 4 + 9];
        var at = 4 + 200 * 4;
        for (var i = 0; i < 9; i++)
        {
            data[at + i] = new byte[] { 3, 1, 2, 4, 0xff, 0, 5, 0, 0 }[i];
        }

        var offset = at - 4;
        data[4 + 7 * 4 + 2] = (byte)(offset >> 8);
        data[4 + 7 * 4 + 3] = (byte)offset;

        return data;
    }

    // Job 11: the figure base 100 and the second base 50; each figure byte adds its own number and, to the second, 1.
    private static SettlerFigures Figures()
    {
        var bases = new short[32 * 8];
        bases[11 * 8] = 100;
        bases[11 * 8 + 1] = 50;
        var offsets = new short[512];
        for (var b = 0; b < 256; b++)
        {
            offsets[2 * b] = (short)b;
            offsets[2 * b + 1] = 1;
        }

        bases[0] = 100;
        bases[1] = -1;
        var carried = new short[27];
        carried[7] = 10;

        return new SettlerFigures(bases, offsets, Sequences(), carried);
    }

    private static Settler Carrier(int mode, int state = 3) =>
        new(0, 0, 4, 3, 0, state, 0)
        {
            Frame = 7,
            Timer = 0,
            Mode = mode,
        };

    private static Map Map() => MapGenerator.Generate(new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);

    private static Settler Walker(int frame, int timer, int state = 2) =>
        new(11, 0, 4, 3, 0, state, 0)
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
    public void TheSpritesOfAJobAreThoseOfEveryTripleOfItsSequences()
    {
        var sprites = Figures().Sprites(11, 0x40).OrderBy(s => s.Entry).ToArray();

        CollectionAssert.AreEqual(
            new[] { (1953, -1), (1954, -1), (1955, -1), (2603, 0x40), (2604, 0x40), (2605, 0x40), (3201, -1) },
            sprites.Distinct().ToArray()
        );
    }

    [TestMethod]
    public void AFrameWithNoSequenceDrawsNothing()
    {
        Assert.IsEmpty(Figures().Draws(Map(), [Walker(9, 0)], 0));
    }

    // The carried good's figure byte is the walking figure byte plus the table's word for the good (0x43f8ae to 0x43f92a).
    // Inferred: that the table's words start at 0x43f4b8 (the program's load names 0x43f4b7), and that the carrier's job
    // (0) with a good in byte 11 in state 3 is the only one drawn so; no emulator run of the draw confirms it.
    [TestMethod]
    public void ACarrierCarryingALogIsDrawnWithTheFigureByteOfTheLogAdded()
    {
        var draws = Figures().Draws(Map(), [Carrier(mode: 7)], 0);

        // Figure byte 3 plus 10: entry 1850 + 100 + 13, and the colour overlay 2500 + 100 + 13; the job has no second figure here.
        CollectionAssert.AreEqual(new[] { 1963, 2613 }, draws.Select(d => d.Entry).ToArray());
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

        CollectionAssert.IsSubsetOf(new[] { 1963, 1964, 1965, 2613 }, entries);
    }

    // The original draws the overlay of the figure 1850 + i from 2500 + i (0x406610), not from the figure's entry plus 2500.
    [TestMethod]
    public void TheColourOverlayOfTheFigureOneEightFiftyPlusIIsTwentyFiveHundredPlusI()
    {
        var bases = new short[32 * 8];
        bases[11 * 8] = 40;
        bases[11 * 8 + 1] = -1;
        var offsets = new short[512];
        offsets[2 * 3] = 7;

        var draws = new SettlerFigures(bases, offsets, Sequences()).Draws(Map(), [Walker(7, 0)], 0);

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
        var bases = new short[32 * 8];
        bases[11 * 8 + 1] = -1;

        Assert.IsEmpty(new SettlerFigures(bases, new short[4], Sequences()).Draws(Map(), [Walker(7, 0)], 0));
    }
}
