using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The commands waiting for their tick. The commands due at one tick come out in a total order, so that the order of
/// <see cref="Add"/> calls does not change the game (INV-0002).
/// </summary>
public sealed class CommandQueue
{
    private readonly SortedSet<Command> _queue = new(Comparer<Command>.Create(Compare));

    /// <summary>The number of commands waiting.</summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Queues a command for its tick. Throws <see cref="ArgumentOutOfRangeException"/> when that tick is before
    /// <paramref name="tickCount"/>, the next tick to run. A command equal to one already queued is the same command and
    /// queues once; the queue keeps its own copy of the directions.
    /// </summary>
    public void Add(Command command, long tickCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(command.Tick, tickCount);
        _queue.Add(command with { Directions = command.Directions?.ToArray() });
    }

    /// <summary>Takes the first command due at <paramref name="tick"/> out of the queue; false when none is.</summary>
    public bool TryTakeDue(long tick, out Command command)
    {
        if (_queue.Count > 0 && _queue.Min.Tick == tick)
        {
            command = _queue.Min;
            _queue.Remove(command);

            return true;
        }

        command = default;

        return false;
    }

    private static int Compare(Command a, Command b)
    {
        var order = (a.Tick, a.Player, a.Kind, a.X, a.Y).CompareTo((b.Tick, b.Player, b.Kind, b.X, b.Y));

        // The directions of a road, compared one by one, the shorter first where one is the start of the other.
        var (da, db) = (a.Directions ?? [], b.Directions ?? []);
        for (var i = 0; order == 0 && i < Math.Min(da.Count, db.Count); i++)
        {
            order = da[i].CompareTo(db[i]);
        }

        return order != 0 ? order : da.Count.CompareTo(db.Count);
    }
}
