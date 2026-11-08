using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Writes the state of a settler, building, or flag as the original's named raw values, the form each event of the history
/// holds (ADR 0013, INV-0006): the fields in a fixed order, each a name and its number. It reads the game's lists, which the
/// game shares with it, and changes none.
/// </summary>
public sealed class SnapshotExporter(
    IReadOnlyList<Settler> settlers,
    IReadOnlyList<Building> buildings,
    FlagTable flags
) : IThingSnapshots
{
    /// <summary>The building in the slot: its type, node, flag, status, worker, and fill, as the building table holds them.</summary>
    public ThingState Building(int slot)
    {
        var b = buildings[slot];
        return new ThingState([
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
        ]);
    }

    /// <summary>The flag in the slot: its node, status byte, road bytes, and the goods on it.</summary>
    public ThingState Flag(int slot)
    {
        var flag = flags[slot];
        var fields = new List<(string, int)> { ("x", flag.X), ("y", flag.Y), ("status", FlagEncoder.Status(flag)) };
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            fields.Add(($"road{direction}", FlagEncoder.RoadByte(flag.Side(direction))));
        }

        fields.AddRange(flag.Places.Select((place, index) => ($"good{index}", (int)FlagEncoder.GoodByte(place))));

        return new ThingState(fields);
    }

    /// <summary>The settler in the slot: the fields of its table item.</summary>
    public ThingState Settler(int slot)
    {
        var s = settlers[slot];
        return new ThingState([
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
        ]);
    }
}
