using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

// The thing IDs and the event store of the game (ADR 0011, INV-0007): the counters and the store sit beside the game state, take
// no part in Digest, and no rule reads them to decide.
public sealed partial class Game
{
    // How often each slot of the settler, building, and flag lists was filled. A list the free slots at its end trimmed keeps
    // its counts, and nothing lowers one.
    private readonly List<int> _settlerFills = [];
    private readonly List<int> _buildingFills = [];
    private readonly List<int> _flagFills = [];

    private readonly EventStore _events = new();

    // The command cause each settler carries on until its behaviour ends.
    private readonly Dictionary<ThingId, Cause> _carried = [];

    /// <summary>
    /// The event store: every event the rules recorded, in order (ADR 0011). It is outside <see cref="Digest"/>; no rule reads
    /// it or a counter of the IDs.
    /// </summary>
    public EventStore Events => _events;

    /// <summary>The events of a settler, building, or flag in the order recorded.</summary>
    public IReadOnlyList<ThingEvent> EventsOf(ThingId thing) => _events.Of(thing);

    /// <summary>The ID of the settler in the slot of <see cref="Settlers"/>; the fill count 0 for a slot never filled.</summary>
    public ThingId SettlerId(int slot) => new(ThingId.SettlerKind, slot, FillOf(_settlerFills, slot));

    /// <summary>The ID of the building in the slot of <see cref="Buildings"/>; the fill count 0 for a slot never filled.</summary>
    public ThingId BuildingId(int slot) => new(ThingId.BuildingKind, slot, FillOf(_buildingFills, slot));

    /// <summary>The ID of the flag in the slot of <see cref="Flags"/>; the fill count 0 for a slot never filled.</summary>
    public ThingId FlagId(int slot) => new(ThingId.FlagKind, slot, FillOf(_flagFills, slot));

    /// <summary>
    /// The state of the thing at the end of <paramref name="tick"/>: the state in its last event at or before that tick. A tick
    /// before the thing's first event, and a tick that has not run, give no state and the reason in the second part. The tick
    /// <see cref="TickCount"/> counts as run where a command recorded an event at it: the state then is the state so far, and
    /// the tick's own rules can still change it.
    /// </summary>
    public (ThingState? State, string? Refusal) StateAt(ThingId thing, long tick)
    {
        var last = Math.Max(TickCount - 1, _events.LastTick);
        if (tick > last)
        {
            return (null, $"tick {tick} has not run: the game ran up to tick {last}");
        }

        var events = _events.Of(thing);
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

    // The cause of a change a player's command makes: the command, or the layout while a layout builds its entries.
    private Cause CommandCause(ThingId commanded) => InLayout ? Cause.Layout : Cause.Placed(commanded);

    // Set while Layout.Apply builds its entries: the commands it applies are the layout's entries, not the player's.
    internal bool InLayout { get; set; }

    private void Record(string type, ThingId thing, ThingState state, Cause cause, string? other) =>
        _events.Add(new ThingEvent(TickCount, type, thing, state, cause, other));

    // Records the event of the building in the slot: its state now, naming the other thing by its ID now.
    internal void RecordBuilding(int slot, string type, Cause cause, string? other = null)
    {
        var b = _buildings[slot];
        Record(
            type,
            BuildingId(slot),
            new ThingState([
                ("type", b.Type),
                ("constructing", b.Constructing ? 1 : 0),
                ("x", b.X),
                ("y", b.Y),
                ("flag", b.Flag),
                ("status", b.Status),
                ("worker", b.Worker),
                ("stamp", b.Stamp),
                ("fill", b.Fill),
                ("progress", b.Progress),
            ]),
            cause,
            other
        );
    }

    // Records the event of the flag in the slot: its node, status byte, road bytes, and the goods on it.
    internal void RecordFlag(int slot, string type, Cause cause, string? other = null)
    {
        var fields = new List<(string, int)>
        {
            ("x", _flags[slot].X),
            ("y", _flags[slot].Y),
            ("status", _flagStatus[slot]),
        };
        fields.AddRange(_roadBytes[slot].Select((value, direction) => ($"road{direction}", (int)value)));
        fields.AddRange(_flagGoods[slot].Select((value, place) => ($"good{place}", (int)value)));
        Record(type, FlagId(slot), new ThingState(fields), cause, other);
    }

    // Starts the settler carrying a command cause on: every event of the settler records it in place of its rule area's cause
    // (RecordSettler) from the change that set a behaviour going, until the behaviour ends. RecordSettler ends the carry by
    // itself: the event that leaves the settler idle in storage (state 1) or waiting at a flag (state 0x42) is the last one that
    // carries it. A rule can end it earlier with EndCarry, before the event of a new job request. Throws for a cause no command
    // made.
    internal void CarryCause(int slot, Cause command)
    {
        if (!command.IsCommand)
        {
            throw new ArgumentException($"a {command.Kind} cause is no command and cannot be carried", nameof(command));
        }

        _carried[SettlerId(slot)] = command;
    }

    // Ends the carrying of a command cause: the settler's next event takes the rule area's cause.
    internal void EndCarry(int slot) => _carried.Remove(SettlerId(slot));

    // Records the event of the settler in the slot: its state now, with the command cause it carries (CarryCause) in place of the
    // rule area's cause. A carried cause ends here when the recorded state is 1 (idle in storage) or 0x42 (waiting at a flag).
    internal void RecordSettler(int slot, string type, Cause cause, string? other = null)
    {
        var s = _settlers[slot];
        var id = SettlerId(slot);
        var carried = _carried.TryGetValue(id, out var command);
        Record(
            type,
            id,
            new ThingState([
                ("job", s.Job),
                ("state", s.State),
                ("player", s.Player),
                ("mode", s.Mode),
                ("target", s.Target),
                ("next", s.Next),
                ("heading", s.Heading),
                ("frame", s.Frame),
                ("timer", s.Timer),
                ("stamp", s.Stamp),
                ("x", s.X),
                ("y", s.Y),
            ]),
            carried ? command! : cause,
            other
        );
        if (carried && s.State is 1 or WaitingState)
        {
            _carried.Remove(id);
        }
    }

    private static int FillOf(List<int> fills, int slot) => slot >= 0 && slot < fills.Count ? fills[slot] : 0;

    // Counts a fill of the slot: the slot's next thing takes the next count.
    private static void Fill(List<int> fills, int slot)
    {
        while (fills.Count <= slot)
        {
            fills.Add(0);
        }

        fills[slot]++;
    }
}
