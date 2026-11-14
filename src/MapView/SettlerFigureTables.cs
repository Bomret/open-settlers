using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The tables the tile pass draws a waiting carrier with (<c>0x44d77e</c>): 16 phase words (<c>0x44d87d</c>), 128 figure words
/// (<c>0x44d89d</c>), and the x and y offsets, as signed bytes, for each value of a node's six road direction bits
/// (<c>0x674923</c>).
/// </summary>
public sealed record IdleCarrierTables
{
    /// <summary>The 16 phase words (<c>0x44d87d</c>).</summary>
    public required short[] Phases { get; init; }

    /// <summary>The 128 figure words (<c>0x44d89d</c>).</summary>
    public required short[] Figures { get; init; }

    /// <summary>The x and y offsets, as signed bytes, for each value of a node's six road direction bits (<c>0x674923</c>).</summary>
    public required sbyte[] Offsets { get; init; }
}

/// <summary>
/// What the figure tables pick for one term of a settler: the sprite entry, the palette index its fill takes (-1 for a
/// sprite drawn as it is), the entry of the figure whose header shifts it (-1 for none; only the second figure is shifted),
/// and the offset the node's road directions move a waiting carrier by (0 for any other).
/// </summary>
public readonly record struct SettlerPick
{
    /// <summary>The sprite entry.</summary>
    public required int Entry { get; init; }

    /// <summary>The palette index the sprite's fill takes; -1 for a sprite drawn as it is.</summary>
    public required int Colour { get; init; }

    /// <summary>The entry of the figure whose header shifts the sprite; -1 for none.</summary>
    public required int ShiftedBy { get; init; }

    /// <summary>The horizontal offset the node's road directions move a waiting carrier by; 0 for any other settler.</summary>
    public required int Dx { get; init; }

    /// <summary>The vertical offset the node's road directions move a waiting carrier by; 0 for any other settler.</summary>
    public required int Dy { get; init; }
}

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

    const int SecondBase = 3150;

    // The palette index of the first player's colour, and the step between players (0x406610).
    const int ColourBase = 0x40;
    const int ColourStep = 4;

    // The node's lake mark picks the fixed figure word, and its road directions the offsets (0x44d77e).
    const int FixedFigureWord = 0x203;

    readonly short[] _bases;
    readonly short[] _offsets;
    readonly byte[] _data;
    readonly Dictionary<Job, HistoryEditionLook> _looks;
    readonly IdleCarrierTables? _idle;

    // The looks the History Edition source holds read the job data of the tables (the registry is generated from the look modules).
    // The figure bases, offsets, and idle tables are given apart from the tables for a test that draws with part of them.
    internal SettlerFigureTables(
        RuleTables tables,
        short[] bases,
        short[] offsets,
        byte[] sequences,
        IdleCarrierTables? idle
    )
    {
        // The handler table at 0x43f744 holds 8 bytes for each job; the engine holds the looks of the jobs it draws.
        _looks = HistoryEditionLookRegistry.Build(tables).ToDictionary(look => look.Job);
        _idle = idle;
        _bases = bases;
        _offsets = offsets;
        _data = sequences;
    }

    /// <summary>The figure tables of the exe's <paramref name="tables"/> over the walk <paramref name="sequences"/>.</summary>
    public static SettlerFigureTables From(RuleTables tables, byte[] sequences) =>
        new(
            tables,
            tables.FigureBases,
            tables.FigureOffsets,
            sequences,
            new IdleCarrierTables
            {
                Phases = tables.IdlePhases,
                Figures = tables.IdleFigures,
                Offsets = tables.IdleOffsets,
            }
        );

    /// <summary>Whether the tables hold the figure handler of <paramref name="job"/>: a job without one has no drawn figure.</summary>
    internal bool HasHandler(Job job) => _looks.ContainsKey(job);

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
            if (draw.Job is not Carrier || _idle is null || draw.Node is not { } node)
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

            if (!_looks.TryGetValue(draw.Job, out var look))
            {
                return null;
            }

            var figure = _data[at];
            word = figure + look.Add(draw, figure);
        }

        return PickOf(word, draw.Part, ColourOf(draw.Player), dx, dy);
    }

    // The part of a figure word: the figure, its colour overlay filled with the colour, or the second figure where the base has
    // one, with the figure entry whose header shifts it (-1 for none: only the second figure is shifted). The high byte of the
    // word picks the pair of bases, the low byte the pair of offsets (0x44fff3). None where either lies outside the tables.
    SettlerPick? PickOf(int word, SettlerPart part, int colour, int dx, int dy)
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
                return new SettlerPick
                {
                    Entry = FigureBase + first,
                    Colour = -1,
                    ShiftedBy = -1,
                    Dx = dx,
                    Dy = dy,
                };
            case SettlerPart.Overlay:
                return new SettlerPick
                {
                    Entry = OverlayBase + first,
                    Colour = colour,
                    ShiftedBy = -1,
                    Dx = dx,
                    Dy = dy,
                };
            default:
                return _bases[2 * row + 1] >= 0
                    ? new SettlerPick
                    {
                        Entry = SecondBase + _bases[2 * row + 1] + _offsets[2 * figure + 1],
                        Colour = -1,
                        ShiftedBy = FigureBase + first,
                        Dx = dx,
                        Dy = dy,
                    }
                    : null;
        }
    }
}
