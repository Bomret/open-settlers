using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>The building a flag is tied to: the one that stands at it, not one that burns (0x4617fd takes the pointer from the flag's item).</summary>
    public Building? OfFlag(int flag) =>
        _buildings.FirstOrDefault(building => !building.IsFree && !building.IsDemolishing && building.Flag == flag);

    /// <summary>
    /// A good destined for the flag no longer comes (0x462ca4, called by 0x462c8d): the building at the flag counts one good less
    /// on the way in the fill byte of the kind, unless it is a finished castle or storehouse, which counts none. Returns the
    /// building whose fill fell, or null where the kind is none, no building stands at the flag, or the building is a storage.
    /// </summary>
    public Building? CountOneLess(int flag, FillCounter kind)
    {
        if (
            kind == FillCounter.None
            || OfFlag(flag) is not { } building
            || (
                !building.Constructing
                && (building.Type == BuildingType.Castle || building.Type == BuildingType.Storehouse)
            )
        )
        {
            return null;
        }

        building.SetFill(kind, building.FillOf(kind) - 1);

        return building;
    }

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
