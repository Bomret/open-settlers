using System.Collections;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The flags of the player in the order of their slots in the original's flag table: the castle's flag first, then each
/// placed flag. A taken-down flag leaves a free slot that the next flag takes; the count falls past the free slots at the
/// table's end (the allocator <c>0x45d7ad</c> and the free <c>0x45d974</c>).
/// </summary>
public sealed class FlagTable : IReadOnlyList<Flag>
{
    /// <summary>
    /// The number of flags the table holds. simplicity: the link keeps the slot in one byte, so a table holds 256 flags.
    /// The original's limit is set from the machine's memory at start-up and is not recovered (<c>docs/rules/flags.md</c>).
    /// </summary>
    public const int Capacity = 256;

    readonly List<Flag> _flags = [];

    /// <summary>The number of slots, free ones before the last flag included.</summary>
    public int Count => _flags.Count;

    /// <summary>Whether no further flag fits: the next slot is past <see cref="Capacity"/>.</summary>
    public bool IsFull => NextSlot >= Capacity;

    /// <summary>The slot the next flag takes: the first free one, else the end of the table.</summary>
    public int NextSlot
    {
        get
        {
            var free = _flags.FindIndex(flag => flag.IsFree);

            return free >= 0 ? free : _flags.Count;
        }
    }

    /// <summary>The flag in the slot.</summary>
    public Flag this[int slot] => _flags[slot];

    /// <summary>The slot of the flag on the node at (<paramref name="x"/>, <paramref name="y"/>), or -1 where none stands there.</summary>
    public int SlotAt(int x, int y) => _flags.FindIndex(flag => flag.X == x && flag.Y == y);

    /// <summary>Puts a new flag on the node into the next slot and returns the slot.</summary>
    public int Add(int x, int y)
    {
        var slot = NextSlot;
        var flag = new Flag(x, y);
        if (slot == _flags.Count)
        {
            _flags.Add(flag);
        }
        else
        {
            _flags[slot] = flag;
        }

        return slot;
    }

    /// <summary>Frees the slot, and drops the free slots at the end of the table.</summary>
    public void Free(int slot)
    {
        _flags[slot].Release();
        while (_flags.Count > 0 && _flags[^1].IsFree)
        {
            _flags.RemoveAt(_flags.Count - 1);
        }
    }

    /// <inheritdoc />
    public IEnumerator<Flag> GetEnumerator() => _flags.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
