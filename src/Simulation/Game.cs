using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// A game: a <see cref="Map"/> and a queue of <see cref="Command"/>s, advanced one tick (ADR 0005) at a time.
/// <see cref="Tick"/> applies the commands due at the tick in a total order (player, kind, column, row), so the
/// order of <see cref="Enqueue"/> calls does not change the game (INV-0002). No rule beyond that exists yet: a
/// command applied changes the state only by being recorded in the digest. The game draws no randomness.
/// </summary>
public sealed class Game
{
    /// <summary>Ticks in a second at normal speed: 4 timer calls of 20 milliseconds make one tick.</summary>
    public const double TicksPerSecond = 12.5;

    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    private readonly SortedSet<Command> _queue = new(Comparer<Command>.Create(Compare));
    private ulong _applied = FnvOffset;

    /// <summary>A game on <paramref name="map"/>, at tick 0 with no command queued.</summary>
    public Game(Map map)
    {
        Map = map;
    }

    /// <summary>The map the game is played on.</summary>
    public Map Map { get; }

    /// <summary>The number of ticks run so far; the next <see cref="Tick"/> is the tick with this number.</summary>
    public long TickCount { get; private set; }

    /// <summary>
    /// Queues a command for its tick. Throws <see cref="ArgumentOutOfRangeException"/> when that tick has run. A
    /// command equal to one already queued is the same command and queues once.
    /// </summary>
    public void Enqueue(Command command)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(command.Tick, TickCount);
        _queue.Add(command);
    }

    /// <summary>Applies the commands due at this tick, then advances the tick count by one.</summary>
    public void Tick()
    {
        while (_queue.Count > 0 && _queue.Min.Tick == TickCount)
        {
            var command = _queue.Min;
            _queue.Remove(command);
            _applied = Mix(Mix(Mix(Mix(Mix(_applied, command.Tick), command.Player), command.Kind), command.X), command.Y);
        }

        TickCount++;
    }

    /// <summary>The FNV-1a 64-bit fingerprint of the whole game state: the tick, the commands applied, and every map layer.</summary>
    public ulong Digest()
    {
        var hash = Mix(Mix(FnvOffset, TickCount), (long)_applied);
        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            hash = Mix(hash, (long)Map.Digest(layer));
        }

        return hash;
    }

    private static int Compare(Command a, Command b)
    {
        var order = a.Tick.CompareTo(b.Tick);
        order = order != 0 ? order : a.Player.CompareTo(b.Player);
        order = order != 0 ? order : a.Kind.CompareTo(b.Kind);
        order = order != 0 ? order : a.X.CompareTo(b.X);

        return order != 0 ? order : a.Y.CompareTo(b.Y);
    }

    private static ulong Mix(ulong hash, long value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
        }

        return hash;
    }
}
