using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// A thing (<c>CONTEXT.md</c>) as a <c>list</c> step shows it: its kind, its node, the raw values the engine holds for
/// it in the engine's form, and its name where the name table has one.
/// </summary>
/// <param name="Kind">The kind, such as <c>object</c>; a list orders by it first.</param>
/// <param name="X">The node's column.</param>
/// <param name="Y">The node's row.</param>
/// <param name="Values">The raw values as a field name and its text, in the order a line shows them.</param>
/// <param name="Name">The name from the name table; null where it has none.</param>
public sealed record Thing(string Kind, int X, int Y, IReadOnlyList<(string Field, string Value)> Values, string? Name)
{
    /// <summary>The kind of a map object.</summary>
    public const string ObjectKind = "object";

    /// <summary>The map object as a thing: its code in hex, and its name where <paramref name="names"/> has one.</summary>
    public static Thing Of(PlacedObject mapObject, NameTable? names) =>
        new(
            ObjectKind,
            mapObject.X,
            mapObject.Y,
            [("code", $"0x{mapObject.Code:x2}")],
            names?.ObjectName(mapObject.Code)
        );

    /// <summary>The kind of a settler.</summary>
    public const string SettlerKind = "settler";

    /// <summary>The kind of a building.</summary>
    public const string BuildingKind = "building";

    /// <summary>The kind of a flag.</summary>
    public const string FlagKind = "flag";

    /// <summary>The kind of a road node.</summary>
    public const string RoadKind = "road";

    static string Hex(int value) => $"0x{value:x2}";

    /// <summary>The settler as a thing: every value the engine holds for it, and its job and state names where there are some.</summary>
    public static Thing Of(Settler settler)
    {
        // The readout shows the original's own encoding of the settler beside the names (INV-0006).
        var raw = SettlerEncoder.Fields(settler);

        return new(
            SettlerKind,
            settler.X,
            settler.Y,
            [
                ("job", settler.Job.ToString()),
                ("state", Hex(raw.State)),
                ("player", settler.Player.ToString(CultureInfo.InvariantCulture)),
                ("mode", Hex(raw.Mode)),
                ("target", raw.Target.ToString(CultureInfo.InvariantCulture)),
                ("next", Hex(raw.Next)),
                ("heading", raw.Heading.ToString(CultureInfo.InvariantCulture)),
                ("frame", settler.Frame.ToString(CultureInfo.InvariantCulture)),
                ("timer", settler.Timer.ToString(CultureInfo.InvariantCulture)),
                ("stamp", raw.Stamp.ToString(CultureInfo.InvariantCulture)),
            ],
            SettlerName(settler)
        );
    }

    static string? SettlerName(Settler settler)
    {
        var name = string.Join(
            " ",
            new[] { NameTable.JobName(settler.Job.Code), NameTable.StateName(settler.Activity.Code) }.OfType<string>()
        );

        return name.Length > 0 ? name : null;
    }

    /// <summary>The building as a thing: its raw type, never the walk's type index, and its raw status.</summary>
    public static Thing Of(Building building) =>
        new(
            BuildingKind,
            building.X,
            building.Y,
            [
                ("type", Hex(building.Type.Code)),
                ("constructing", building.Constructing ? "true" : "false"),
                ("status", Hex(BuildingEncoder.Status(building))),
                ("flag", building.Flag.ToString(CultureInfo.InvariantCulture)),
                ("worker", BuildingEncoder.Worker(building).ToString(CultureInfo.InvariantCulture)),
            ],
            NameTable.BuildingName(building.Type.Code)
        );

    /// <summary>
    /// The things a <c>list</c> step shows: every thing of <paramref name="game"/> in the game view once a game runs,
    /// otherwise the map objects of <paramref name="map"/>; empty for no map.
    /// </summary>
    public static IReadOnlyList<Thing> Listed(bool gameView, Game? game, Map? map, NameTable? names) =>
        gameView && game is not null ? Of(game, names) : [.. (map?.Objects ?? []).Select(o => Of(o, names))];

    /// <summary>
    /// Every thing of the game: the map objects off the flags' and buildings' nodes, the settlers, the buildings and
    /// flags (a free slot shows none), and each road node once with the directions its roads leave in.
    /// </summary>
    public static IReadOnlyList<Thing> Of(Game game, NameTable? names)
    {
        var buildings = game
            .Buildings.Select((building, slot) => (building, slot))
            .Where(e => !e.building.IsFree)
            .ToList();
        var flags = game.Flags.Select((flag, slot) => (flag, slot)).Where(entry => !entry.flag.IsFree).ToList();
        var taken = buildings
            .Select(e => (e.building.X, e.building.Y))
            .Concat(flags.Select(entry => (entry.flag.X, entry.flag.Y)))
            .ToHashSet();

        return
        [
            .. game.Map.Objects.Where(o => !taken.Contains((o.X, o.Y))).Select(o => Of(o, names)),
            .. game.Settlers.Select((settler, slot) => Of(game, settler, slot)),
            .. buildings.Select(e => Of(game, e.building, e.slot)),
            .. flags.Select(entry => new Thing(
                FlagKind,
                entry.flag.X,
                entry.flag.Y,
                [
                    ("slot", entry.slot.ToString(CultureInfo.InvariantCulture)),
                    ("id", game.History.FlagId(entry.slot).ToString()),
                ],
                null
            )),
            .. RoadNodes(game)
                .Select(node => new Thing(
                    RoadKind,
                    node.Key.X,
                    node.Key.Y,
                    [("dirs", string.Join(",", node.Value))],
                    null
                )),
        ];
    }

    // The settler with the ID of its slot, and the flag its state makes the target (state 0x42 waits next to it; state 2 walks to it
    // or along the road that leaves it, but not the walk home, which targets a storage). No other state is known to hold a flag
    // slot in the target.
    static Thing Of(Game game, Settler settler, int slot)
    {
        var thing = Of(settler);
        var flag = settler.Activity switch
        {
            WaitsAtFlag waiting => FlagLink(game, waiting.Flag),
            Walks { Errand: not ToStorage } walking => FlagLink(game, walking.Errand.Flag),
            _ => null,
        };

        return thing with
        {
            Values =
            [
                .. thing.Values,
                ("id", game.History.SettlerId(slot).ToString()),
                .. flag is { } id ? [("flag-id", id)] : Array.Empty<(string, string)>(),
            ],
        };
    }

    // The building with the ID of its slot, and its flag and worker as IDs. A building that burns holds its countdown in the
    // worker word, so it has no worker link; no worker is slot 0.
    static Thing Of(Game game, Building building, int slot)
    {
        var thing = Of(building);
        var worker = !building.IsDemolishing && building.Worker > 0 && building.Worker < game.Settlers.Count;
        var flag = FlagLink(game, building.Flag);

        return thing with
        {
            Values =
            [
                .. thing.Values,
                ("id", game.History.BuildingId(slot).ToString()),
                .. flag is { } flagId ? [("flag-id", flagId)] : Array.Empty<(string, string)>(),
                .. worker
                    ? [("worker-id", game.History.SettlerId(building.Worker).ToString())]
                    : Array.Empty<(string, string)>(),
            ],
        };
    }

    // The ID of the flag in the slot, or null where no flag stands there.
    static string? FlagLink(Game game, int slot) =>
        slot >= 0 && slot < game.Flags.Count && !game.Flags[slot].IsFree ? game.History.FlagId(slot).ToString() : null;

    // The directions each road node's roads leave in: a road's steps run from its starting flag, each node holding the
    // way on and the way back; the ending flag holds the way back only.
    static Dictionary<(int X, int Y), SortedSet<int>> RoadNodes(Game game)
    {
        var nodes = new Dictionary<(int X, int Y), SortedSet<int>>();
        void Add((int X, int Y) node, int direction)
        {
            if (!nodes.TryGetValue(node, out var directions))
            {
                nodes[node] = directions = [];
            }

            directions.Add(direction);
        }

        var size = game.Map.Size;
        foreach (var road in game.Network.Roads)
        {
            var flag = game.Flags[road.FromSlot];
            var node = (flag.X, flag.Y);
            foreach (var direction in road.Directions)
            {
                Add(node, direction);
                var (dx, dy) = RoadBuilding.Steps[direction];
                node = ((node.X + dx + size.Width) % size.Width, (node.Y + dy + size.Height) % size.Height);
                Add(node, (direction + 3) % 6);
            }
        }

        return nodes;
    }

    /// <summary>The line of the thing: <c>&lt;kind&gt; &lt;column&gt;,&lt;row&gt; &lt;field&gt;=&lt;raw&gt; ... &lt;name&gt;</c>, no newline.</summary>
    public string Line()
    {
        var text = new StringBuilder();
        text.Append(Kind)
            .Append(' ')
            .Append(X.ToString(CultureInfo.InvariantCulture))
            .Append(',')
            .Append(Y.ToString(CultureInfo.InvariantCulture));
        foreach (var (field, value) in Values)
        {
            text.Append(' ').Append(field).Append('=').Append(value);
        }

        if (Name is not null)
        {
            text.Append(' ').Append(Name);
        }

        return text.ToString();
    }

    /// <summary>
    /// The text of a <c>.things.txt</c> file: one line for each thing, each ending in a newline, by kind, then row, then
    /// column, and by line text for things on one node; empty for no things.
    /// </summary>
    public static string File(IEnumerable<Thing> things) =>
        string.Concat(
            things
                .OrderBy(t => t.Kind, StringComparer.Ordinal)
                .ThenBy(t => t.Y)
                .ThenBy(t => t.X)
                .ThenBy(t => t.Line(), StringComparer.Ordinal)
                .Select(t => t.Line() + "\n")
        );
}
