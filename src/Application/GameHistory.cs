using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The thing IDs, the causes, and the event store of one game (ADR 0011, INV-0007): the counters and the store sit beside the
/// game state, take no part in <c>Game.Digest</c>, and no rule reads them to decide. The history asks its
/// <see cref="IThingSnapshots"/> for the state of a thing when a rule records an event.
/// </summary>
public sealed class GameHistory
{
    // The state of a settler waiting next to a flag (docs/rules/carriers.md).
    const int WaitingState = 0x42;

    // The state of a settler idle in a storage.
    const int IdleState = 1;

    // How often each slot of the settler, building, and flag lists was filled. A list the free slots at its end trimmed keeps
    // its counts, and nothing lowers one.
    readonly List<int> _settlerFills = [];
    readonly List<int> _buildingFills = [];
    readonly List<int> _flagFills = [];

    // The command cause each settler carries on until its behaviour ends.
    readonly Dictionary<ThingId, Cause> _carried = [];

    readonly IThingSnapshots _snapshots;
    readonly Func<long> _tickCount;

    /// <summary>
    /// A history that takes each thing's state from <paramref name="snapshots"/> and reads the number of ticks the game has
    /// run from <paramref name="tickCount"/>.
    /// </summary>
    public GameHistory(IThingSnapshots snapshots, Func<long> tickCount)
    {
        _snapshots = snapshots;
        _tickCount = tickCount;
    }

    /// <summary>
    /// The event store: every event the rules recorded, in order (ADR 0011). It is outside the game's digest; no rule reads
    /// it or a counter of the IDs.
    /// </summary>
    public EventStore Events { get; } = new();

    /// <summary>The events of a settler, building, or flag in the order recorded.</summary>
    public IReadOnlyList<ThingEvent> EventsOf(ThingId thing) => Events.Of(thing);

    /// <summary>The ID of the settler in the slot of the settler list; the fill count 0 for a slot never filled.</summary>
    public ThingId SettlerId(int slot) =>
        new()
        {
            Kind = ThingId.SettlerKind,
            Slot = slot,
            Fill = FillOf(_settlerFills, slot),
        };

    /// <summary>The ID of the building in the slot of the building list; the fill count 0 for a slot never filled.</summary>
    public ThingId BuildingId(int slot) =>
        new()
        {
            Kind = ThingId.BuildingKind,
            Slot = slot,
            Fill = FillOf(_buildingFills, slot),
        };

    /// <summary>The ID of the flag in the slot of the flag list; the fill count 0 for a slot never filled.</summary>
    public ThingId FlagId(int slot) =>
        new()
        {
            Kind = ThingId.FlagKind,
            Slot = slot,
            Fill = FillOf(_flagFills, slot),
        };

    /// <summary>Counts a fill of the settler slot: its next settler takes the next count.</summary>
    internal void FillSettler(int slot) => Fill(_settlerFills, slot);

    /// <summary>Counts a fill of the building slot: its next building takes the next count.</summary>
    internal void FillBuilding(int slot) => Fill(_buildingFills, slot);

    /// <summary>Counts a fill of the flag slot: its next flag takes the next count.</summary>
    internal void FillFlag(int slot) => Fill(_flagFills, slot);

    /// <summary>
    /// The state of the thing at the end of <paramref name="tick"/>: the state in its last event at or before that tick. A tick
    /// before the thing's first event, and a tick that has not run, give no state and the reason in the second part. The
    /// game's tick count counts as run where a command recorded an event at it: the state then is the state so far, and the
    /// tick's own rules can still change it.
    /// </summary>
    public (ThingState? State, string? Refusal) StateAt(ThingId thing, long tick)
    {
        var last = Math.Max(_tickCount() - 1, Events.LastTick);
        if (tick > last)
        {
            return (null, $"tick {tick} has not run: the game ran up to tick {last}");
        }

        var events = Events.Of(thing);
        if (events.Count == 0)
        {
            return (null, $"{thing} has no event");
        }

        if (tick < events[0].Tick)
        {
            return (null, $"{thing} first appears at tick {events[0].Tick}, after tick {tick}");
        }

        // The events are in time order: scan from the back for the last one at or before the tick.
        for (var i = events.Count - 1; ; i--)
        {
            if (events[i].Tick <= tick)
            {
                return (events[i].State, null);
            }
        }
    }

    /// <summary>Records the event of the building in the slot: its state now, naming the other thing by its ID now.</summary>
    internal void RecordBuilding(int slot, string type, Cause cause, string? other = null) =>
        Record(type, BuildingId(slot), _snapshots.Building(slot), cause, other);

    /// <summary>Records the event of the flag in the slot: its state now.</summary>
    internal void RecordFlag(int slot, string type, Cause cause, string? other = null) =>
        Record(type, FlagId(slot), _snapshots.Flag(slot), cause, other);

    /// <summary>
    /// Starts the settler carrying a command cause on: every event of the settler records it in place of its rule area's cause
    /// (<see cref="RecordSettler"/>) from the change that set a behaviour going, until the behaviour ends.
    /// <see cref="RecordSettler"/> ends the carry by itself: the event that leaves the settler idle in storage (state 1) or
    /// waiting at a flag (state 0x42) is the last one that carries it. A rule can end it earlier with
    /// <see cref="EndCarry"/>, before the event of a new job request. Throws for a cause no command made.
    /// </summary>
    internal void CarryCause(int slot, Cause command)
    {
        if (!command.IsCommand)
        {
            throw new ArgumentException($"a {command.Kind} cause is no command and cannot be carried", nameof(command));
        }

        _carried[SettlerId(slot)] = command;
    }

    /// <summary>Ends the carrying of a command cause: the settler's next event takes the rule area's cause.</summary>
    internal void EndCarry(int slot) => _carried.Remove(SettlerId(slot));

    /// <summary>
    /// Records the event of the settler in the slot: its state now, with the command cause it carries
    /// (<see cref="CarryCause"/>) in place of the rule area's cause. A carried cause ends here when the recorded state is 1
    /// (idle in storage) or 0x42 (waiting at a flag).
    /// </summary>
    internal void RecordSettler(int slot, string type, Cause cause, string? other = null)
    {
        var id = SettlerId(slot);
        var state = _snapshots.Settler(slot);
        var carried = _carried.TryGetValue(id, out var command);
        Record(type, id, state, carried ? command! : cause, other);
        if (carried && state.Get("state") is IdleState or WaitingState)
        {
            _carried.Remove(id);
        }
    }

    void Record(string type, ThingId thing, ThingState state, Cause cause, string? other) =>
        Events.Add(
            new ThingEvent
            {
                Tick = _tickCount(),
                Type = type,
                Thing = thing,
                State = state,
                Cause = cause,
                Other = other,
            }
        );

    static int FillOf(List<int> fills, int slot) => slot >= 0 && slot < fills.Count ? fills[slot] : 0;

    static void Fill(List<int> fills, int slot)
    {
        while (fills.Count <= slot)
        {
            fills.Add(0);
        }

        fills[slot]++;
    }
}
