using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// A game: a <see cref="Map"/> and a queue of <see cref="Command"/>s, advanced one tick (ADR 0005) at a time.
/// <see cref="Tick"/> applies the commands due at the tick in a total order (player, kind, column, row), so the
/// order of <see cref="Enqueue"/> calls does not change the game (INV-0002). The only rule so far is the castle:
/// <see cref="CommandKind.PlaceCastle"/> puts it where the original allows (<c>docs/rules/castle.md</c>), and every
/// other command is refused. A refused command changes nothing, not even the digest. The game draws no randomness.
/// </summary>
public sealed class Game
{
    /// <summary>Ticks in a second at normal speed: 4 timer calls of 20 milliseconds make one tick.</summary>
    public const double TicksPerSecond = 12.5;

    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    private readonly SortedSet<Command> _queue = new(Comparer<Command>.Create(Compare));
    private readonly ProgramTables _tables;
    private readonly int _stockLevel;
    private ulong _applied = FnvOffset;

    /// <summary>
    /// A game on <paramref name="map"/> with the rule tables of <paramref name="tables"/> (read from the install's
    /// program), at tick 0 with no command queued and no castle. The castle will hold the stock of
    /// <paramref name="stockLevel"/> (<see cref="StartStock.At"/>). The game changes the map it is given:
    /// placing the castle writes the castle, its flag, the heights, and the territory into the map's layers.
    /// </summary>
    public Game(ProgramTables tables, Map map, int stockLevel = 0)
    {
        _tables = tables;
        Map = map;
        _stockLevel = stockLevel;
    }

    /// <summary>The map the game is played on.</summary>
    public Map Map { get; }

    /// <summary>The castle of the one player, or null before it is placed.</summary>
    public Castle? Castle { get; private set; }

    /// <summary>The goods in the castle, <see cref="StartStock.Goods"/> counts; empty before the castle is placed.</summary>
    public IReadOnlyList<int> Stock { get; private set; } = [];

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
            if (Apply(command))
            {
                _applied = Mix(
                    Mix(Mix(Mix(Mix(_applied, command.Tick), command.Player), command.Kind), command.X),
                    command.Y
                );
            }
        }

        TickCount++;
    }

    /// <summary>
    /// The FNV-1a 64-bit fingerprint of the whole game state: the tick, the commands applied, the castle and its stock,
    /// and every map layer.
    /// </summary>
    public ulong Digest()
    {
        var hash = Mix(Mix(FnvOffset, TickCount), (long)_applied);
        hash = Mix(hash, Castle is { } castle ? castle.X * 65536L + castle.Y : -1);
        foreach (var count in Stock)
        {
            hash = Mix(hash, count);
        }

        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            hash = Mix(hash, (long)Map.Digest(layer));
        }

        return hash;
    }

    // Applies a command if its rule allows it and tells whether it did.
    private bool Apply(Command command)
    {
        if (command is not { Kind: CommandKind.PlaceCastle, Player: 0 } || Castle is not null)
        {
            return false;
        }

        var size = Map.Size;
        if (command.X < 0 || command.X >= size.Width || command.Y < 0 || command.Y >= size.Height)
        {
            return false;
        }

        var site = BuildSite.Evaluate(_tables, Map, command.X, command.Y, 0, hasCastle: false);
        if (!site.IsCastleSite)
        {
            return false;
        }

        Castle = new Castle(command.X, command.Y, site.Height);
        Stock = StartStock.At(_tables, _stockLevel);
        CastlePlacement.Apply(Map, command.X, command.Y, site.Height, 0);

        return true;
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
