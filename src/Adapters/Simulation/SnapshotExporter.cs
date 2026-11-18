using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Writes the state of a settler, building, or flag as the original's named raw values, the form each event of the history
/// holds (ADR 0013, INV-0006): the fields in a fixed order, each a name and its number. It reads the game's lists, which the
/// game shares with it, and changes none.
/// </summary>
/// <param name="settlers">The settler list.</param>
/// <param name="buildings">The building list.</param>
/// <param name="flags">The flag list.</param>
/// <param name="catalog">The content catalog of the game: it gives each good its original code, or the word of the hash of its name.</param>
public sealed class SnapshotExporter(
    SettlerTable settlers,
    BuildingTable buildings,
    FlagTable flags,
    ContentCatalog catalog
) : IThingSnapshots
{
    /// <inheritdoc/>
    public string GoodText(Good good) => GoodWords.Text(catalog, good);

    /// <summary>The building in the slot: its type, node, flag, status, worker, and fill, as the building table holds them.</summary>
    public ThingState Building(int slot)
    {
        var b = buildings[slot];
        List<(string, int)> fields =
        [
            ("type", BuildingWords.Of(catalog, b.Type)),
            ("constructing", b.Constructing ? 1 : 0),
            ("x", b.X),
            ("y", b.Y),
            ("flag", b.Flag),
            ("status", BuildingEncoder.Status(b)),
            ("worker", BuildingEncoder.Worker(b)),
            ("stamp", b.Stamp),
            ("fill", BuildingEncoder.Fill(b)),
            ("progress", b.Progress),
        ];
        if (b.Site is { } site)
        {
            fields.AddRange([("planks", site.Planks), ("stones", site.Stones), ("height", site.TargetHeight)]);
        }

        return new ThingState { Fields = fields };
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

        fields.AddRange(
            flag.Places.Select((place, index) => ($"good{index}", (int)FlagEncoder.GoodWord(place, catalog)))
        );

        return new ThingState { Fields = fields };
    }

    /// <summary>The settler in the slot: the fields of its table item.</summary>
    public ThingState Settler(int slot)
    {
        var s = settlers[slot];
        var raw = SettlerEncoder.Fields(s, catalog);
        return new ThingState
        {
            Fields =
            [
                ("job", s.Job.Code),
                ("state", raw.State),
                ("player", s.Player),
                ("mode", raw.Mode),
                ("target", raw.Target),
                ("next", raw.Next),
                ("heading", raw.Heading),
                ("frame", s.Frame),
                ("timer", s.Timer),
                ("stamp", raw.Stamp),
                ("x", s.X),
                ("y", s.Y),
            ],
        };
    }
}
