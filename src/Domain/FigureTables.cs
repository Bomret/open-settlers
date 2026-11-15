using System;
using System.Collections.Immutable;
using System.Numerics;

namespace OpenSettlers.Domain;

/// <summary>
/// The tables the settlers' figures are drawn from: the figure bases and offsets, the word a carried good adds, the waiting
/// carriers' phases, figure words, and offsets, and the frame rows. They come from the program's data (INV-0001) and are read-only:
/// the constructor copies what it is given, and no member hands out the stored data for change. A table not given reads as zeros.
/// </summary>
public sealed class FigureTables
{
    /// <summary>
    /// Creates figure tables from their contents. The waiting carriers' phases and figure words are indexed with a mask of their
    /// length, so a length that is no power of two is refused.
    /// </summary>
    public FigureTables(
        short[]? figureBases = null,
        short[]? figureOffsets = null,
        short[]? carriedFigures = null,
        short[]? idlePhases = null,
        short[]? idleFigures = null,
        sbyte[]? idleOffsets = null,
        sbyte[]? frameRows = null
    )
    {
        Bases = [.. figureBases ?? new short[2 * RuleTables.FigureBaseRowCount]];
        Offsets = [.. figureOffsets ?? new short[512]];
        Carried = [.. carriedFigures ?? new short[RuleTables.CarriedFigureCount]];
        IdlePhases = [.. idlePhases ?? new short[RuleTables.IdlePhaseCount]];
        IdleFigures = [.. idleFigures ?? new short[RuleTables.IdleFigureCount]];
        IdleOffsets = [.. idleOffsets ?? new sbyte[RuleTables.IdleOffsetCount]];
        FrameRows = [.. frameRows ?? new sbyte[RuleTables.FrameRowCount]];
        if (!BitOperations.IsPow2(IdlePhases.Length) || !BitOperations.IsPow2(IdleFigures.Length))
        {
            throw new ArgumentException("The waiting carriers' phase and figure tables hold a power of two words.");
        }
    }

    /// <summary>For each high byte of the figure word 2 words: the figure base and the second figure base (negative: none).</summary>
    public ImmutableArray<short> Bases { get; }

    /// <summary>For each low byte of the figure word 2 words: what it adds to the figure and to the second figure.</summary>
    public ImmutableArray<short> Offsets { get; }

    /// <summary>For each good a word that a carrier carrying it adds to its figure byte: the row in the high byte.</summary>
    public ImmutableArray<short> Carried { get; }

    /// <summary>The waiting carriers' phases.</summary>
    public ImmutableArray<short> IdlePhases { get; }

    /// <summary>The waiting carriers' figure words.</summary>
    public ImmutableArray<short> IdleFigures { get; }

    /// <summary>The waiting carriers' offsets by road directions, x and y for each.</summary>
    public ImmutableArray<sbyte> IdleOffsets { get; }

    /// <summary>For each frame of a settler what the record builder adds to the row of its record's list.</summary>
    public ImmutableArray<sbyte> FrameRows { get; }

    /// <summary>What the record builder adds to the row for the frame <paramref name="frame"/>; 0 for a frame past the rows.</summary>
    public int FrameRow(int frame) => frame < FrameRows.Length ? FrameRows[frame] : 0;

    /// <summary>The word a carrier carrying the good number <paramref name="good"/> adds; 0 for a number past the table.</summary>
    public short CarriedWord(int good) => good < Carried.Length ? Carried[good] : (short)0;
}
