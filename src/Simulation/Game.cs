using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// A game: a <see cref="Map"/> and a queue of <see cref="Command"/>s, advanced one tick (ADR 0005) at a time.
/// <see cref="Tick"/> applies the commands due at the tick in a total order (player, kind, column, row), so the
/// order of <see cref="Enqueue"/> calls does not change the game (INV-0002). The rules so far are the castle and the
/// flag: <see cref="CommandKind.PlaceCastle"/> and <see cref="CommandKind.PlaceFlag"/> put them where the original
/// allows (<c>docs/rules/castle.md</c>, <c>docs/rules/flags.md</c>), and <see cref="CommandKind.BuildRoad"/> joins two
/// flags (<c>docs/rules/roads.md</c>); every other command is refused. A refused command changes nothing, not even the
/// digest. The game draws no randomness.
/// </summary>
public sealed partial class Game
{
    /// <summary>Ticks in a second at normal speed: 4 timer calls of 20 milliseconds make one tick.</summary>
    public const double TicksPerSecond = 12.5;

    private const int MaxFlags = 256;
    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    private readonly SortedSet<Command> _queue = new(Comparer<Command>.Create(Compare));
    private readonly ProgramTables _tables;
    private readonly int _stockLevel;
    private readonly List<Flag> _flags = [];
    private readonly List<Road> _roads = [];
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

    /// <summary>
    /// The flags in the order of their slots in the flag table: the castle's flag first, then each placed flag. Empty
    /// before the castle is placed.
    /// </summary>
    public IReadOnlyList<Flag> Flags => _flags;

    /// <summary>The finished roads in the order they were built.</summary>
    public IReadOnlyList<Road> Roads => _roads;

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
                foreach (var direction in command.Directions ?? [])
                {
                    _applied = Mix(_applied, direction);
                }
            }
        }

        Pass();
        TickCount++;
    }

    /// <summary>
    /// The FNV-1a 64-bit fingerprint of the whole game state: the tick, the commands applied, the castle and its stock,
    /// the flags, the total of resource 1, and every map layer.
    /// </summary>
    public ulong Digest()
    {
        var hash = Mix(Mix(FnvOffset, TickCount), (long)_applied);
        hash = Mix(hash, Castle is { } castle ? castle.X * 65536L + castle.Y : -1);
        foreach (var count in Stock)
        {
            hash = Mix(hash, count);
        }

        foreach (var flag in _flags)
        {
            hash = Mix(hash, flag.X * 65536L + flag.Y);
        }

        foreach (var road in _roads)
        {
            hash = Mix(
                Mix(Mix(Mix(Mix(hash, road.FromSlot), road.FromDirection), road.ToSlot), road.ToDirection),
                road.Water ? 1 : 0
            );
            foreach (var direction in road.Directions)
            {
                hash = Mix(hash, direction);
            }
        }

        hash = Mix(hash, Map.Resource1Total);
        MixSettlers(ref hash);

        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            hash = Mix(hash, (long)Map.Digest(layer));
        }

        return hash;
    }

    // Applies a command if its rule allows it and tells whether it did.
    private bool Apply(Command command)
    {
        if (command.Player != 0)
        {
            return false;
        }

        var size = Map.Size;
        if (command.X < 0 || command.X >= size.Width || command.Y < 0 || command.Y >= size.Height)
        {
            return false;
        }

        return command.Kind switch
        {
            CommandKind.PlaceCastle => PlaceCastle(command.X, command.Y),
            CommandKind.PlaceFlag => PlaceFlag(command.X, command.Y),
            CommandKind.BuildRoad => BuildRoad(command),
            CommandKind.PlaceBuilding => PlaceBuilding(command.X, command.Y),
            _ => false,
        };
    }

    /// <summary>
    /// Where a road would stand if the player drew it from the flag on the node along the directions, as the original's
    /// road drawing judges it (<see cref="RoadBuilding"/>): refused, still open, or reaching a flag and accepted.
    /// Changes nothing, so an interface can call it after each step it draws.
    /// </summary>
    public RoadStatus CheckRoad(int x, int y, IReadOnlyList<int> directions) =>
        Castle is null ? RoadStatus.Refused : RoadBuilding.Walk(_tables, Map, x, y, directions, keep: false).Status;

    private bool BuildRoad(Command command)
    {
        if (Castle is null || command.Directions is null)
        {
            return false;
        }

        var (status, road) = RoadBuilding.Walk(_tables, Map, command.X, command.Y, command.Directions, keep: true);
        if (status != RoadStatus.Complete)
        {
            return false;
        }

        _roads.Add(road!);

        return true;
    }

    private bool PlaceCastle(int x, int y)
    {
        if (Castle is not null)
        {
            return false;
        }

        var site = BuildSite.Evaluate(_tables, Map, x, y, 0, hasCastle: false);
        if (!site.IsCastleSite)
        {
            return false;
        }

        Castle = new Castle(x, y, site.Height);
        Stock = StartStock.At(_tables, _stockLevel);
        CastlePlacement.Apply(Map, x, y, site.Height, 0);
        // The castle's flag stands south-east of it and takes the first slot of the flag table.
        _flags.Add(new Flag((x + 1) & (Map.Size.Width - 1), (y + 1) & (Map.Size.Height - 1)));
        CreateCastleSettlers(x, y);

        return true;
    }

    private bool PlaceFlag(int x, int y)
    {
        // simplicity: the link keeps the slot in one byte, so a flag table holds 256 flags. The original's limit is
        // set from the machine's memory at start-up and is not recovered (docs/rules/flags.md).
        if (Castle is null || _flags.Count >= MaxFlags)
        {
            return false;
        }

        if (!BuildSite.Evaluate(_tables, Map, x, y, 0, hasCastle: true).AllowsFlag)
        {
            return false;
        }

        FlagPlacement.Apply(Map, x, y, _flags.Count);
        _flags.Add(new Flag(x, y));

        return true;
    }

    private static int Compare(Command a, Command b)
    {
        var order = a.Tick.CompareTo(b.Tick);
        order = order != 0 ? order : a.Player.CompareTo(b.Player);
        order = order != 0 ? order : a.Kind.CompareTo(b.Kind);
        order = order != 0 ? order : a.X.CompareTo(b.X);
        order = order != 0 ? order : a.Y.CompareTo(b.Y);

        // The directions of a road, compared one by one, the shorter first where one is the start of the other.
        var (da, db) = (a.Directions ?? [], b.Directions ?? []);
        for (var i = 0; order == 0 && i < Math.Min(da.Count, db.Count); i++)
        {
            order = da[i].CompareTo(db[i]);
        }

        return order != 0 ? order : da.Count.CompareTo(db.Count);
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
