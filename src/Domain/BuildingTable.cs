using System.Collections;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The buildings in the order of their slots in the original's building table: the castle first, then each placed building.
/// A building that burned down leaves a free slot that the next building takes; the count falls past the free slots at the
/// table's end (<c>0x42cf9d</c>).
/// </summary>
public sealed class BuildingTable : IReadOnlyList<Building>
{
    private readonly List<Building> _buildings = [];

    /// <summary>The number of slots, free ones before the last building included.</summary>
    public int Count => _buildings.Count;

    /// <summary>The slot the next building takes: the first free one, else the end of the table.</summary>
    public int NextSlot
    {
        get
        {
            var free = _buildings.FindIndex(existing => existing.IsFree);

            return free >= 0 ? free : _buildings.Count;
        }
    }

    /// <summary>The building in the slot.</summary>
    public Building this[int slot] => _buildings[slot];

    /// <summary>Puts a building into the next slot and returns the slot.</summary>
    public int Add(Building building)
    {
        var slot = NextSlot;
        if (slot == _buildings.Count)
        {
            _buildings.Add(building);
        }
        else
        {
            _buildings[slot] = building;
        }

        return slot;
    }

    /// <summary>The slot of the building.</summary>
    public int SlotOf(Building building) => _buildings.IndexOf(building);

    /// <summary>Frees the slot and lets the count fall past the free slots at the table's end.</summary>
    public void Free(int slot)
    {
        _buildings[slot] = Building.Free();
        while (_buildings.Count > 0 && _buildings[^1].IsFree)
        {
            _buildings.RemoveAt(_buildings.Count - 1);
        }
    }

    /// <inheritdoc />
    public IEnumerator<Building> GetEnumerator() => _buildings.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
