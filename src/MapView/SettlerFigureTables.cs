using System.Collections.Generic;
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

    private readonly short[] _bases;
    private readonly short[] _offsets;
    private readonly byte[] _data;
    private readonly Dictionary<Job, IFigureHandler> _handlers;
    private readonly IdleCarrierTables? _idle;

    /// <param name="bases">Two words for each high byte of the figure word: the figure base and the second figure base (negative for none).</param>
    /// <param name="offsets">Two words for each low byte of the figure word: what it adds to the figure and to the second figure.</param>
    /// <param name="sequences">The bytes of entry 2 of the graphics archive.</param>
    /// <param name="carried">For each good the word a carrier carrying it adds to its figure byte.</param>
    /// <param name="woodcutter">What the woodcutter's handler adds to the figure byte; all 0 draws him in the carrier's row.</param>
    /// <param name="typeOneHut">What the handler of the settler of the type 1 hut adds to the figure byte (<c>0x43fed8</c>).</param>
    /// <param name="idle">The tables of the waiting carriers; none draws no waiting carrier.</param>
    /// <param name="stonecutter">What the stonecutter's handler adds to the figure byte; all 0 draws him in the carrier's row.</param>
    /// <param name="digger">What the digger's handler adds to the figure byte (<c>0x43fa87</c>); all 0 draws him in the carrier's row.</param>
    /// <param name="sawmiller">What the sawmiller's handler adds to the figure byte (<c>0x43fc21</c>) and how far it moves the saw's second figure; all 0 draws him in the carrier's row.</param>
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
        ForesterFigureAdds forester = default,
        FigureAdds digger = default,
        SawmillerFigureAdds sawmiller = default
    )
    {
        // The handler table at 0x43f744 holds 8 bytes for each job; the engine holds the handlers of the jobs it draws.
        _handlers = new Dictionary<Job, IFigureHandler>
        {
            [Job.Carrier] = new CarrierFigures(carried ?? []),
            [Job.Woodcutter] = new WoodcutterFigures(woodcutter),
            [Job.Stonecutter] = new StonecutterFigures(stonecutter),
            [Job.TypeOneHut] = new TypeOneHutFigures(typeOneHut),
            [Job.Builder] = new WorkFigures(builder.Work, builder.Walk),
            [Job.Digger] = new WorkFigures(digger.Cutting, digger.Walk),
            [Job.Forester] = new WorkFigures(forester.Work, forester.Walk),
            [Job.Sawmiller] = new SawmillerFigures(sawmiller),
        };
        _idle = idle;
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
            tables.Get<FigureAdds>(Job.Woodcutter),
            tables.Get<HutFigureAdds>(Job.TypeOneHut),
            new IdleCarrierTables(tables.IdlePhases, tables.IdleFigures, tables.IdleOffsets),
            tables.Get<BuilderFigureAdds>(Job.Builder),
            tables.Get<FigureAdds>(Job.Stonecutter),
            tables.Get<ForesterFigureAdds>(Job.Forester),
            tables.Get<FigureAdds>(Job.Digger),
            tables.Get<SawmillerFigureAdds>(Job.Sawmiller)
        );

    /// <summary>Whether the tables hold the figure handler of <paramref name="job"/>: a job without one has no drawn figure.</summary>
    public bool HasHandler(Job job) => _handlers.ContainsKey(job);

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

            if (!_handlers.TryGetValue(draw.Job, out var handler))
            {
                return null;
            }

            var figure = _data[at];
            word = figure + handler.Add(draw, figure);
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
}
