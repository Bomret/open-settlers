using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// The search a gatherer's worker runs from his building (INV-0017): the range of spiral entries he draws from and the target test a
/// node of that range must pass. His job rules declare it once (<see cref="JobRules.Search"/>); his search loop draws and tests through
/// it, and a view that shows the search reads the same declaration (<see cref="Game.SearchOf"/>).
/// </summary>
public sealed record JobSearch
{
    /// <summary>The first spiral entry of the range.</summary>
    public required int First { get; init; }

    /// <summary>The number of entries of the range, a power of two: the draw's mask keeps as many values.</summary>
    public required int Count { get; init; }

    /// <summary>
    /// The target test: whether the node at the column and row of the map is one the worker goes to. It reads the map alone, draws
    /// nothing from the game's random generator, and writes nothing. The column and row are wrapped at the map's edges.
    /// </summary>
    public required Func<Map, int, int, bool> Accepts { get; init; }

    /// <summary>The last spiral entry of the range.</summary>
    public int Last => First + Count - 1;

    /// <summary>The mask the loop applies to a draw before it maps the draw onto the range (the draw arithmetic's <c>&amp; mask</c>).</summary>
    public int Mask => (Count - 1) << 2;

    /// <summary>The spiral entry a draw picks: the draw masked, the first entry added four times over, and the sum shifted right by two.</summary>
    public int Entry(ushort draw) => ((draw & Mask) + (First << 2)) >> 2;
}
