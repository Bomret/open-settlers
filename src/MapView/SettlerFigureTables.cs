using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The tables the tile pass draws a waiting carrier with (<c>0x44d77e</c>): 16 phase words (<c>0x44d87d</c>), 128 figure words
/// (<c>0x44d89d</c>), and the x and y offsets, as signed bytes, for each value of a node's six road direction bits
/// (<c>0x674923</c>).
/// </summary>
public sealed record IdleCarrierTables(short[] Phases, short[] Figures, sbyte[] Offsets);

/// <summary>
/// What the figure tables pick for one term of a settler: the sprite entry, the palette index its fill takes (-1 for a
/// sprite drawn as it is), the entry of the figure whose header shifts it (-1 for none; only the second figure is shifted),
/// and the offset the node's road directions move a waiting carrier by (0 for any other).
/// </summary>
public readonly record struct SettlerPick(int Entry, int Colour, int ShiftedBy, int Dx, int Dy);

/// <summary>
/// Which sprites a settler is drawn with, by the exe's figure tables (<c>docs/rules/settlers.md</c>, "Drawing the walk"): the
/// History Edition source holds them (ADR 0014). The settler's frame and its time left pick a triple of the sequences of entry 2
/// of the graphics archive; its figure byte is the first of the triple. The job's handler (<c>0x43f744</c> plus 8 for each job)
/// adds a constant to the figure byte to make the figure word; the high byte of the word picks a pair of words of the figure
/// bases and the low byte a pair of the figure offsets, which give the entries: the figure, its overlay in the player's
/// colour, and a second figure (<c>0x44fff3</c>). The figure 1850 + i takes its overlay from 2500 + i, filled with the
/// player's colour. <b>Confirmed</b> by the draw runs of <c>tests/OpenSettlers.Tests/settler-draws.txt</c> (the emulator verb
/// <c>draw</c> runs <c>0x42f4f1</c>, <c>0x43f537</c>, <c>0x44ffe7</c>, and <c>0x406610</c>), which the engine matches pass for pass
/// for the carrier (with each good), the woodcutter, and the settler of the type 1 hut. <b>Inferred:</b> every state the
/// recordings hold no settler of.
/// </summary>
public sealed class SettlerFigureTables
{
    // The sprite entry of the figures, of their colour overlays, and of the second figures (0x406610): the figure 1850 + i
    // has the overlay 2500 + i.

    /// <summary>The sprite entry of the first figure.</summary>
    public const int FigureBase = 1850;

    /// <summary>The sprite entry of the first colour overlay; the figures lie below it.</summary>
    public const int OverlayBase = 2500;

    private const int SecondBase = 3150;

    // The palette index of the first player's colour, and the step between players (0x406610).
    private const int ColourBase = 0x40;
    private const int ColourStep = 4;

    // The node's lake mark picks the fixed figure word, and its road directions the offsets (0x44d77e).
    private const int FixedFigureWord = 0x203;

    // The woodcutter's handler (0x43fb6b) tests the figure byte for bit 7, and the settler's activity for the way back with the
    // log.
    private const int HighFigure = 0x80;

    private readonly short[] _bases;
    private readonly short[] _offsets;
    private readonly byte[] _data;
    private readonly short[] _carried;
    private readonly FigureAdds _woodcutter;
    private readonly FigureAdds _stonecutter;
    private readonly HutFigureAdds _typeOneHut;
    private readonly BuilderFigureAdds _builder;
    private readonly ForesterFigureAdds _forester;
    private readonly IdleCarrierTables? _idle;

    /// <param name="bases">Two words for each high byte of the figure word: the figure base and the second figure base (negative for none).</param>
    /// <param name="offsets">Two words for each low byte of the figure word: what it adds to the figure and to the second figure.</param>
    /// <param name="sequences">The bytes of entry 2 of the graphics archive.</param>
    /// <param name="carried">For each good the word a carrier carrying it adds to its figure byte.</param>
    /// <param name="woodcutter">What the woodcutter's handler adds to the figure byte; all 0 draws him in the carrier's row.</param>
    /// <param name="typeOneHut">What the handler of the settler of the type 1 hut adds to the figure byte (<c>0x43fed8</c>).</param>
    /// <param name="idle">The tables of the waiting carriers; none draws no waiting carrier.</param>
    /// <param name="stonecutter">What the stonecutter's handler adds to the figure byte; all 0 draws him in the carrier's row.</param>
    public SettlerFigureTables(
        short[] bases,
        short[] offsets,
        byte[] sequences,
        short[]? carried = null,
        FigureAdds woodcutter = default,
        HutFigureAdds typeOneHut = default,
        IdleCarrierTables? idle = null,
        BuilderFigureAdds builder = default,
        FigureAdds stonecutter = default,
        ForesterFigureAdds forester = default
    )
    {
        _builder = builder;
        _forester = forester;
        _idle = idle;
        _stonecutter = stonecutter;
        _typeOneHut = typeOneHut;
        _woodcutter = woodcutter;
        _carried = carried ?? [];
        _bases = bases;
        _offsets = offsets;
        _data = sequences;
    }

    /// <summary>The figure tables of the exe's <paramref name="tables"/> over the walk <paramref name="sequences"/>.</summary>
    public static SettlerFigureTables From(RuleTables tables, byte[] sequences) =>
        new(
            tables.FigureBases,
            tables.FigureOffsets,
            sequences,
            tables.CarriedFigures,
            tables.WoodcutterFigureAdds,
            tables.TypeOneHutFigureAdds,
            new IdleCarrierTables(tables.IdlePhases, tables.IdleFigures, tables.IdleOffsets),
            tables.BuilderFigureAdds,
            tables.StonecutterFigureAdds,
            tables.ForesterFigureAdds
        );

    /// <summary>The palette index of a player's colour.</summary>
    public static int ColourOf(int player) => ColourBase + ColourStep * player;

    /// <summary>
    /// The sprite the tables pick for a term, or null where there is none: the job has no drawn figure, the term's step
    /// is outside the sequences or the word outside the tables, or the term has no second figure.
    /// </summary>
    public SettlerPick? Pick(SettlerDraw draw)
    {
        int word;
        var dx = 0;
        var dy = 0;
        if (draw.Activity is WaitsOffRoad)
        {
            if (draw.Job != Job.Carrier || _idle is null || draw.Node is not { } node)
            {
                return null;
            }

            // The figure word is 0x203 where bit 6 of the node's flag byte is set; else the word of the table at 0x44d89d at
            // the phase of the node plus the clock, shifted right 3.
            if (node.LakeMark)
            {
                word = FixedFigureWord;
            }
            else
            {
                // **Stand-in:** the original picks the phase by bits 2 to 5 of the address of the node's record, so by the
                // node's column only where the map buffer starts on a multiple of 64; the engine takes the column.
                var phase = _idle.Phases[node.Column & (_idle.Phases.Length - 1)];
                word = (ushort)_idle.Figures[((ushort)(phase + draw.Step) >> 3) & (_idle.Figures.Length - 1)];
                // The offsets table has an entry for each set of road directions (its six bits as a number).
                var direction = node.Roads.Bits;
                dx = _idle.Offsets[2 * direction];
                dy = _idle.Offsets[2 * direction + 1];
            }
        }
        else
        {
            if (StepOffsets.TripleAt(_data, draw.Frame, draw.Step) is not { } at)
            {
                return null;
            }

            word = FigureWord(draw, _data[at]);
        }

        return PickOf(word, draw.Part, ColourOf(draw.Player), dx, dy);
    }

    // The part of a figure word: the figure, its colour overlay filled with the colour, or the second figure where the base has
    // one, with the figure entry whose header shifts it (-1 for none: only the second figure is shifted). The high byte of the
    // word picks the pair of bases, the low byte the pair of offsets (0x44fff3). None where either lies outside the tables.
    private SettlerPick? PickOf(int word, SettlerPart part, int colour, int dx, int dy)
    {
        var row = (word >> 8) & 0xff;
        var figure = word & 0xff;
        if (2 * row + 1 >= _bases.Length || 2 * figure + 1 >= _offsets.Length)
        {
            return null;
        }

        var first = _bases[2 * row] + _offsets[2 * figure];

        switch (part)
        {
            case SettlerPart.Figure:
                return new SettlerPick(FigureBase + first, -1, -1, dx, dy);
            case SettlerPart.Overlay:
                return new SettlerPick(OverlayBase + first, colour, -1, dx, dy);
            default:
                return _bases[2 * row + 1] >= 0
                    ? new SettlerPick(
                        SecondBase + _bases[2 * row + 1] + _offsets[2 * figure + 1],
                        -1,
                        FigureBase + first,
                        dx,
                        dy
                    )
                    : null;
        }
    }

    // The figure word of a settler's figure byte (0x43f8ae for the carrier, 0x43fb6b for the woodcutter): the carrier adds
    // the word of the good it carries; the woodcutter adds the constant of the handler, the one of the way back with the
    // log (byte 13 0x80, byte 14 1, state 0x10) for a figure byte below 0x80 on that way.
    private int FigureWord(SettlerDraw draw, int figure)
    {
        if (draw.Job == Job.Carrier)
        {
            return figure + CarriedFigure(draw);
        }

        if (draw.Job == Job.Builder)
        {
            // The builder's handler (0x43faf4) adds the blows' constant for a figure byte from 0x80 and the walk's below it.
            return figure + (figure >= HighFigure ? _builder.Work : _builder.Walk);
        }

        if (draw.Job == Job.TypeOneHut)
        {
            // The handler of the type 1 hut's settler (0x43fed8) adds by the heading for a figure byte from 0x80 and by the
            // way back with a load, as the woodcutter's does, below it.
            if (figure >= HighFigure)
            {
                return figure + (FacesBack(draw) ? _typeOneHut.HighFacing : _typeOneHut.High);
            }

            return figure + (WayBackWithLoad(draw) ? _typeOneHut.Carrying : _typeOneHut.Walk);
        }

        if (draw.Job == Job.Stonecutter)
        {
            return figure + StonecutterAdd(figure, draw);
        }

        if (draw.Job == Job.Forester)
        {
            // The forester's handler (0x43fd88) adds the work's constant for a figure byte from 0x80 and the walk's below it,
            // the waiting and planting frames included.
            return figure + (figure >= HighFigure ? _forester.Work : _forester.Walk);
        }

        return figure + WoodcutterAdd(figure, WayBackWithLoad(draw));
    }

    // What the stonecutter's handler (0x43fcd8) adds to a figure byte: the stroke's constant from 0x80, below it the way back's
    // with the stone, also for the step back off the heap (state 0x17, byte 13 2), or the walk's.
    private int StonecutterAdd(int figure, SettlerDraw draw) =>
        figure >= HighFigure ? _stonecutter.Cutting
        : WayBackWithLoad(draw) || draw.Activity is CutsStone { Phase: 2 } ? _stonecutter.Carrying
        : _stonecutter.Walk;

    // What the woodcutter's handler (0x43fb6b) adds to a figure byte: the felling's constant from 0x80, below it the way
    // back's with a load or the walk's.
    private int WoodcutterAdd(int figure, bool carrying) =>
        figure >= HighFigure ? _woodcutter.Cutting
        : carrying ? _woodcutter.Carrying
        : _woodcutter.Walk;

    // The way back with a load: the free walk back to the hut's flag with the log (0x43fc0b, 0x43ff40).
    private static bool WayBackWithLoad(SettlerDraw draw) =>
        draw.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: true } };

    // The settler's heading is 1 (byte 14, which the handler of the type 1 hut's settler reads at 0x43fed8): the way back with
    // a load, the first cut, or a step back from the node to the south-west.
    private static bool FacesBack(SettlerDraw draw) =>
        draw.Activity switch
        {
            WalksFree { Trip.Purpose: BackToHut { Carrying: true } } => true,
            Cuts cut => cut.Count == 1,
            Walks => draw.Heading == new Heading(1),
            _ => false,
        };

    // The word a carrier on its road or handing a good over adds to its figure byte for the good it carries (byte 11, at
    // 0x43f8ae, for the states 3 and 0xe). A carrier in state 0x42 has no record (byte 11 is the road direction there, no
    // good).
    private int CarriedFigure(SettlerDraw draw) => draw.Activity is WalksRoad or HandsOver ? Carried(draw.Carried) : 0;

    // The word of the table at 0x43f4b7 for a good (2 * good bytes on): the row of the figure bases in its high byte. A
    // carrier with no good (0) adds nothing; so does a good the table does not cover.
    private int Carried(Good good) => good.Code > 0 && good.Code < _carried.Length ? _carried[good.Code] : 0;
}
