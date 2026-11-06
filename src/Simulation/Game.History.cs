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

    // The cause of a change a player's command makes: the command, or the layout while a layout builds its entries.
    private Cause CommandCause(ThingId commanded) => InLayout ? Cause.Layout : Cause.Placed(commanded);

    // Set while Layout.Apply builds its entries: the commands it applies are the layout's entries, not the player's.
    internal bool InLayout { get; set; }

    private void Record(string type, ThingId thing, ThingState state, Cause cause, string? other) =>
        _events.Add(new ThingEvent(TickCount, type, thing, state, cause, other));

    // Records the event of the building in the slot: its state now, naming the other thing by its ID now.
    private void RecordBuilding(int slot, string type, Cause cause, string? other = null)
    {
        var b = _buildings[slot];
        Record(
            type,
            BuildingId(slot),
            new ThingState(
                [
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
                ]
            ),
            cause,
            other
        );
    }

    // Records the event of the flag in the slot: its node, status byte, road bytes, and the goods on it.
    private void RecordFlag(int slot, string type, Cause cause, string? other = null)
    {
        var fields = new List<(string, int)> { ("x", _flags[slot].X), ("y", _flags[slot].Y), ("status", _flagStatus[slot]) };
        fields.AddRange(_roadBytes[slot].Select((value, direction) => ($"road{direction}", (int)value)));
        fields.AddRange(_flagGoods[slot].Select((value, place) => ($"good{place}", (int)value)));
        Record(type, FlagId(slot), new ThingState(fields), cause, other);
    }

    // Records the event of the settler in the slot: its state now, with the command cause it carries (CarryCause) in place of the
    // rule area's cause. The event that leaves the settler idle in storage or waiting at a flag is the last one the command
    // cause reaches.
    private void RecordSettler(int slot, string type, Cause cause, string? other = null)
    {
        var s = _settlers[slot];
        var id = SettlerId(slot);
        var carried = _carried.TryGetValue(id, out var command);
        Record(
            type,
            id,
            new ThingState(
                [
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
                ]
            ),
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
