using System;
using System.Collections.Generic;

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

    readonly int _count = 1;

    /// <summary>The number of entries of the range, a power of two: the draw's mask keeps as many values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The count is not a positive power of two, at construction or in a copy made with <c>with</c>.
    /// </exception>
    public required int Count
    {
        get => _count;
        init =>
            _count =
                value > 0 && (value & (value - 1)) == 0
                    ? value
                    : throw new ArgumentOutOfRangeException(
                        nameof(Count),
                        value,
                        "The count must be a positive power of two."
                    );
    }

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

    /// <summary>
    /// The work area around the node at (<paramref name="x"/>, <paramref name="y"/>): the node of each spiral entry of the range, in
    /// the order of the entries, wrapped at <paramref name="map"/>'s edges as the worker's search wraps them.
    /// </summary>
    public IEnumerable<(int X, int Y)> Area(Map map, int x, int y)
    {
        for (var entry = First; entry <= Last; entry++)
        {
            var (dx, dy) = Spiral.Offsets[entry];

            yield return ((x + dx) & (map.Size.Width - 1), (y + dy) & (map.Size.Height - 1));
        }
    }
}
