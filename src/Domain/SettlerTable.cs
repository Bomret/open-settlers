using System.Collections;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The settlers in the order of their slots in the original's settler table: the castle's first 20, then each created one.
/// The slot is a settler's number for its whole life.
/// </summary>
public sealed class SettlerTable : IReadOnlyList<Settler>
{
    readonly List<Settler> _settlers = [];

    /// <summary>The number of settlers.</summary>
    public int Count => _settlers.Count;

    /// <summary>
    /// The size of the original's settler table on the map, <see cref="SizeOf"/>; <see cref="int.MaxValue"/> for a table built
    /// without one.
    /// </summary>
    public int Capacity { get; init; } = int.MaxValue;

    /// <summary>
    /// Whether the table takes no more settlers. The original's slot 0 is a dummy that <c>MapGen_InitParams</c> (<c>0x421530</c>)
    /// takes from the table's free count, so a game holds one settler less than <see cref="Capacity"/>; this table has no dummy.
    /// </summary>
    public bool IsFull => Count >= Capacity - 1;

    /// <summary>The size of the settler table on a map of the size: (width / 32) * (height / 32) * 500 (<c>Map_SetGeometry</c>, <c>0x42195c</c>).</summary>
    public static int SizeOf(MapSize size) => size.Width / 32 * (size.Height / 32) * 500;

    /// <summary>The settler in the slot.</summary>
    public Settler this[int slot] => _settlers[slot];

    /// <summary>Puts a settler into the next slot and returns the slot.</summary>
    public int Add(Settler settler)
    {
        _settlers.Add(settler);

        return _settlers.Count - 1;
    }

    /// <summary>The slot of the settler.</summary>
    public int SlotOf(Settler settler) => _settlers.IndexOf(settler);

    /// <inheritdoc />
    public IEnumerator<Settler> GetEnumerator() => _settlers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
