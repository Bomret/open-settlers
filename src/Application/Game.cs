using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// A game: a <see cref="Map"/> and a queue of <see cref="Command"/>s, advanced one tick (ADR 0005) at a time, the commands due
/// at a tick applied in a total order so that the order of <see cref="Enqueue"/> calls does not change the game (INV-0002).
/// The game only applies commands, runs the tick in the original's order, and builds the digest: each kind of command has its
/// handler, the tick is the main pass, the digest is the port's, and everything else a caller reaches is a read-only part.
/// The behaviours that change the game are internal, reached only by the assemblies that build and drive it.
/// </summary>
/// <remarks>
/// A game of <paramref name="parts"/>, at tick 0 with no command queued and no castle. Throws
/// <see cref="ArgumentException"/> where two handlers apply the same kind.
/// </remarks>
/// <param name="parts">The parts the game is built from.</param>
public sealed class Game(GameParts parts)
{
    /// <summary>Ticks in a second at normal speed: 4 timer calls of 20 milliseconds make one tick.</summary>
    public const double TicksPerSecond = 12.5;

    readonly GameParts _parts = parts;
    readonly List<Command> _applied = [];

    /// <summary>The handler of each kind of command.</summary>
    internal IReadOnlyDictionary<int, ICommandHandler> Handlers { get; } =
        parts.Handlers.ToDictionary(handler => handler.Kind);

    /// <summary>The commands applied so far, in the order they were applied.</summary>
    internal IReadOnlyList<Command> Applied => _applied;

    /// <summary>
    /// The number of commands applied so far. A view that draws the flags, roads, and buildings redraws when it changes: a
    /// taken-down flag leaves the lists as long as they were.
    /// </summary>
    public int Revision => _applied.Count;

    /// <summary>The number of ticks run so far; the next <see cref="Tick"/> is the tick with this number.</summary>
    public long TickCount { get; private set; }

    /// <summary>The map the game is played on.</summary>
    public Map Map => _parts.Map;

    /// <summary>The rule tables.</summary>
    public RuleTables Tables => _parts.Tables;

    /// <summary>The castle of the one player (null before it is placed) and the goods in it.</summary>
    public CastleStock CastleStock => _parts.CastleStock;

    /// <summary>
    /// The flags in the order of their slots in the flag table: the castle's flag first, then each placed flag. Empty before
    /// the castle is placed.
    /// </summary>
    public FlagTable Flags => _parts.Flags;

    /// <summary>The finished roads in the order they were built, and the questions the rules ask of them.</summary>
    public RoadNetwork Network => _parts.Network;

    /// <summary>The buildings in the order of their slots: the castle first, then each placed building.</summary>
    public IReadOnlyList<Building> Buildings => _parts.Buildings;

    /// <summary>The settlers in the order of their slots: the castle's first 20, then each created one.</summary>
    public IReadOnlyList<Settler> Settlers => _parts.Settlers;

    /// <summary>
    /// The history of the game: the thing IDs, the causes, and the events the rules recorded (ADR 0011). It is outside
    /// <see cref="Digest"/>; no rule reads it.
    /// </summary>
    public GameHistory History => _parts.History;

    /// <summary>The game clock, in clock units (16 bits, 8 for each tick): the settlers' timers and stamps count in it.</summary>
    public GameClock Clock => _parts.Clock;

    /// <summary>The settler supply, or null in a game that does not run it.</summary>
    internal SettlerSupply? Supply => _parts.Supply;

    /// <summary>The settler supply's values, or null in a game that does not run it.</summary>
    public SupplyState? SupplyState =>
        _parts.Supply is { } supply
            ? new SupplyState(
                supply.Period,
                supply.Countdown,
                supply.LastClock,
                supply.KnightAccumulator,
                supply.KnightsOwed,
                supply.Running,
                supply.Cap,
                supply.MayMake
            )
            : null;

    /// <summary>The commands applied so far, in the order they were applied.</summary>
    public IReadOnlyList<Command> AppliedCommands => _applied;

    /// <summary>The index of the flag the carriers' flag pass scans next.</summary>
    public int CarrierFlagCursor => _parts.Carriers.FlagCursor;

    /// <summary>The state of the game's random generator.</summary>
    public RandomGenerator RandomState => _parts.Draws.Generator;

    /// <summary>The state the huts' workers share for the map's object pass.</summary>
    public (ushort Stamp, ushort Timer, int X, int Y, ushort Decay) HutPassState => _parts.Hut.ObjectPassState;

    /// <summary>The phase the main pass is in.</summary>
    public int PassPhase => _parts.Pass.Phase;

    /// <summary>The index of the building the main pass scans next.</summary>
    public int PassScanCursor => _parts.Pass.ScanCursor;

    /// <summary>The number of the castle's settlers that are leaving it.</summary>
    public int CastleLeavingCount => _parts.Roster.LeavingCount;

    /// <summary>The slots of the settlers that wait in the castle, in the order they wait.</summary>
    public IReadOnlyList<ushort> CastleWaitingList => _parts.Roster.WaitingList;

    /// <summary>The occupied places of the shared ground, each as the index of the node and the slot of its settler.</summary>
    public IReadOnlyList<(int Index, int Slot)> OccupiedMovementSlots => [.. _parts.Movement.Occupied];

    /// <summary>The causes the rules give their events.</summary>
    internal Causes Causes => _parts.Causes;

    /// <summary>The game's random generator: it starts as the map's generation left it and draws only in the passes.</summary>
    internal IRandomDraws Draws => _parts.Draws;

    /// <summary>The ground the settlers share.</summary>
    internal SettlerMovement Movement => _parts.Movement;

    /// <summary>The behaviour of each activity type that the table pass hands a settler to.</summary>
    internal ActivityRegistry Behaviours => _parts.Behaviours;

    /// <summary>The pass of the main loop: the phase, the scan cursor, and whether every pass gives the settlers a turn.</summary>
    internal MainPass Pass => _parts.Pass;

    /// <summary>The castle's settlers and the list of those that wait.</summary>
    internal CastleRoster Roster => _parts.Roster;

    /// <summary>The carriers: the flag table's pass, the carrier requests, and the walk along the roads.</summary>
    internal CarrierBehavior Carriers => _parts.Carriers;

    /// <summary>The goods on the flags: their destinations, directions, and the exchange with the carriers.</summary>
    internal GoodsTransport Goods => _parts.Goods;

    /// <summary>The removal: the click class of a node, the take-downs, and the split of a road.</summary>
    internal Removal Removal => _parts.Removal;

    /// <summary>The walk home: which settlers are on it, and the rules of its claim, search, and arrival.</summary>
    internal WalkHomeBehavior WalkHome => _parts.WalkHome;

    /// <summary>The walk to an errand and the entry into the building at its end.</summary>
    internal WalkBehavior Walks => _parts.Walks;

    /// <summary>The states the workers of the huts share, and the map's object pass.</summary>
    internal HutWork Hut => _parts.Hut;

    /// <summary>
    /// Stands a finished building of <paramref name="type"/> on the node, as a layout's finished-building entry does
    /// (<see cref="BuildingPlacement.PlaceFinished"/>), and tells whether the placement allowed it. No command gives a finished
    /// building. Stand-in: the placement is logged among the commands applied, as the placement command it replaces, with no type,
    /// so the digests the layouts and scenarios of M3 gave stay.
    /// </summary>
    public bool PlaceFinishedBuilding(BuildingType type, int x, int y)
    {
        if (!_parts.Placement.PlaceFinished(type, x, y))
        {
            return false;
        }

        _applied.Add(
            new Command
            {
                Tick = TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = x,
                Y = y,
            }
        );

        return true;
    }

    /// <summary>
    /// Stands the worker of the finished hut of <paramref name="type"/> on the node inside it, as a layout's finished-building
    /// entry does where the type's job lets its settler stand in the hut (<see cref="WalkBehavior.StandsWorker"/>). Returns why it
    /// refused, or null where the worker stands or the type's worker does not stand in a hut.
    /// </summary>
    public string? StandWorkerInFinishedHut(BuildingType type, int x, int y) =>
        _parts.Walks.StandsWorker(type) ? _parts.Walks.StandWorkerInHut(x, y) : null;

    /// <summary>
    /// Places the carrier of the road that leaves the flag on the node in the direction, as a layout's settler entry does
    /// (<see cref="CarrierBehavior.PlaceCarrierOnRoad"/>). Returns why it refused, or null.
    /// </summary>
    public string? PlaceCarrierOnRoad(int x, int y, int direction) =>
        _parts.Carriers.PlaceCarrierOnRoad(x, y, direction);

    /// <summary>
    /// Puts a good on a place of the flag on the node, as a layout's good entry does (<see cref="GoodsTransport.PlaceGood"/>).
    /// Returns why it refused, or null.
    /// </summary>
    public string? PlaceGood(int x, int y, int place, int good) => _parts.Goods.PlaceGood(x, y, place, good);

    /// <summary>
    /// Sets or clears the layout mode: while it is set, the rules give their events the cause layout (<see cref="Causes.InLayout"/>).
    /// The caller that sets it clears it again, also where a step of its setup throws.
    /// </summary>
    public void SetLayoutMode(bool inLayout) => _parts.Causes.InLayout = inLayout;

    /// <summary>
    /// The command that removes what stands on the node, by the class of the click (<see cref="CommandKind"/>), or null where
    /// nothing there can be removed.
    /// </summary>
    public int? RemovalCommandAt(int x, int y) => _parts.Removal.CommandAt(x, y);

    /// <summary>The check of the road the player draws.</summary>
    public RoadPlanner RoadPlanner => _parts.RoadPlanner;

    /// <summary>
    /// Queues a command for its tick. Throws <see cref="ArgumentOutOfRangeException"/> when that tick has run. A command equal
    /// to one already queued is the same command and queues once.
    /// </summary>
    public void Enqueue(Command command) => _parts.Queue.Add(command, TickCount);

    /// <summary>Applies the commands due at this tick, then runs the tick's pass and advances the tick count by one.</summary>
    public void Tick()
    {
        ApplyCommands();
        _parts.Pass.Run();
        TickCount++;
    }

    /// <summary>The FNV-1a 64-bit fingerprint of the game state the ticks have produced (a command still queued is no part of it).</summary>
    public ulong Digest() => _parts.Digest.Of(this);

    /// <summary>
    /// The layout seam: applies the commands due at this tick and nothing else, with no pass and no tick advance. The layout
    /// loader places its things at tick 0 through it. A game that is played runs only <see cref="Tick"/>.
    /// </summary>
    public void ApplyCommands()
    {
        while (_parts.Queue.TryTakeDue(TickCount, out var command))
        {
            if (Apply(command))
            {
                _applied.Add(command);
            }
        }
    }

    // Applies a command if its rule allows it and tells whether it did.
    bool Apply(Command command)
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

        return Handlers.TryGetValue(command.Kind, out var handler) && handler.Handle(command);
    }
}
