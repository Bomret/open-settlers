using System.Collections;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The settlers in the order of their slots in the original's settler table: the castle's first 20, then each created one.
/// The slot is a settler's number for its whole life.
/// </summary>
public sealed class SettlerTable : IReadOnlyList<Settler>
{
    private readonly List<Settler> _settlers = [];

    /// <summary>The number of settlers.</summary>
    public int Count => _settlers.Count;

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
