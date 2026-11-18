using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

public sealed partial class Layout
{
    // One entry of the file that a game takes as a command: its name in the file (flags[1]) and the command; a refusal
    // reports the reason Refusal gives for the command's kind. A building, settler, or good entry is no command a player gives:
    // its Place puts it in the game directly (Game.PlaceFinishedBuilding, WalkBehavior.StandWorkerInHut,
    // CarrierBehavior.PlaceCarrierOnRoad, GoodsTransport.PlaceGood) and returns why it refused, or null. A command's Check runs
    // before the game takes it and returns why the game's content set refuses a name the entry gives, or null.
    sealed record Step
    {
        public required string Name { get; init; }

        public required Command Command { get; init; }

        public Func<Game, string?>? Place { get; init; } = null;

        public Func<Game, string?>? Check { get; init; } = null;
    }

    /// <summary>
    /// Builds the castle, flags, roads, and buildings of the layout in the game, one entry at a time in the order the
    /// file lists them (key by key, entry by entry), as <see cref="Game.Enqueue"/> takes them at the game's tick: the
    /// effect of each entry is known before the next is queued. Returns null when every entry was built, or the
    /// problem <c>file: entry: reason</c> of the first entry the game refused, which stops the build. A refusal leaves
    /// the earlier entries in the game, so the caller shows nothing of it and starts no play on that game.
    /// </summary>
    public string? Apply(Game game)
    {
        game.Causes.InLayout = true;
        try
        {
            return Build(game);
        }
        finally
        {
            game.Causes.InLayout = false;
        }
    }

    // The entries one at a time; the game records their events with the cause layout (Causes.InLayout).
    string? Build(Game game)
    {
        foreach (var step in _steps)
        {
            if (step.Place is not null)
            {
                if (step.Place(game) is { } reason)
                {
                    return $"{_file}: {step.Name}: {reason}";
                }

                continue;
            }

            if (step.Check?.Invoke(game) is { } refused)
            {
                return $"{_file}: {step.Name}: {refused}";
            }

            var revision = game.Revision;
            game.Enqueue(step.Command with { Tick = game.TickCount });
            game.ApplyCommands();
            if (game.Revision == revision)
            {
                return $"{_file}: {step.Name}: {Refusal(step.Command.Kind)}";
            }
        }

        return null;
    }

    // Why the original refuses each kind of command (the documentation of the command kind).
    static string Refusal(int kind) =>
        kind switch
        {
            CommandKind.PlaceCastle => "the node is no castle site, the original refuses it there",
            CommandKind.PlaceFlag =>
                "the original's flag button does not allow a flag on the node: the castle must stand, and the node "
                    + "must hold no flag, no building, and no object that blocks it",
            CommandKind.BuildRoad =>
                "a step is not allowed where the original's road drawing allows it, or the last step reaches no "
                    + "flag; list the flag at the end before the road",
            _ => BuildingRefusal,
        };

    const string BuildingRefusal =
        "the node is no site for a building of the type's size with its flag south-east of it, and the castle must stand";

    sealed partial class Reader
    {
        // The castle, flags, roads, buildings, settlers, and goods entries in the order the file holds them. Null when an entry is bad
        // or repeats an earlier one of its kind.
        List<Step>? ReadSteps(JsonElement root, MapSize? size)
        {
            var steps = new List<Step>();
            var good = true;
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "castle":
                        good &= ReadEntry("castle", property.Value, CommandKind.PlaceCastle, steps, size);
                        break;
                    case "flags":
                        good &= ReadList(property, CommandKind.PlaceFlag, steps);
                        break;
                    case "roads":
                        good &= ReadList(property, CommandKind.BuildRoad, steps);
                        break;
                    case "buildings":
                        good &= ReadEntries(property, ReadBuilding, steps);
                        break;
                    case "settlers":
                        good &= ReadEntries(property, ReadSettler, steps);
                        break;
                    case "goods":
                        good &= ReadEntries(property, ReadGood, steps);
                        break;
                }
            }

            return good ? steps : null;
        }

        bool ReadList(JsonProperty property, int kind, List<Step> steps)
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                Fail(property.Name, $"must be a list of {Shape(kind)}");

                return false;
            }

            var good = true;
            var index = 0;
            foreach (var entry in property.Value.EnumerateArray())
            {
                good &= ReadEntry($"{property.Name}[{index++}]", entry, kind, steps);
            }

            return good;
        }

        bool ReadEntries(JsonProperty property, Func<string, JsonElement, List<Step>, bool> read, List<Step> steps)
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                Fail(property.Name, "must be a list");

                return false;
            }

            var good = true;
            var index = 0;
            foreach (var entry in property.Value.EnumerateArray())
            {
                good &= read($"{property.Name}[{index++}]", entry, steps);
            }

            return good;
        }

        const string BuildingShape =
            "{\"type\": a stable name, \"x\", \"y\"} (the node of the building, finished with its flag south-east of it)";

        // The building types a finished-building entry may name (docs/rules/layouts.md): the types the placement builds that the
        // catalog holds, by stable name in ordinal order.
        static IEnumerable<string> PlaceableNames(ContentCatalog catalog) =>
            BuildingPlacement
                .Types.Where(type => catalog.EntryOf(type) is not null)
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal);

        // A finished-building entry: the building stands finished at tick 0 as the original's mission start leaves a building
        // (0x41f2d2 places it through 0x449a63 and finishes it).
        bool ReadBuilding(string name, JsonElement entry, List<Step> steps)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Fail(name, $"must be {BuildingShape}");

                return false;
            }

            var unknown = entry
                .EnumerateObject()
                .Select(p => p.Name)
                .FirstOrDefault(n => n is not ("type" or "x" or "y"));
            if (unknown is not null)
            {
                Fail(name, $"unknown key {unknown}, a building is {BuildingShape}");

                return false;
            }

            if (!entry.TryGetProperty("type", out var typeEntry))
            {
                Fail(name, $"a building needs a \"type\": a building is {BuildingShape}");

                return false;
            }

            // The game's catalog is not known while the file is read: a name the catalog lacks, or a build-panel name, is refused when
            // the layout is built, with the names the game's content set knows.
            var type = new BuildingType
            {
                Name = typeEntry.ValueKind == JsonValueKind.String ? typeEntry.GetString() ?? "" : "",
            };
            var given = typeEntry.GetRawText();

            if (!Whole(entry, "x", out var x) || !Whole(entry, "y", out var y))
            {
                Fail(name, "x and y must each be a whole number");

                return false;
            }

            var command = new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = x,
                Y = y,
                Building = type,
            };
            var repeated = steps.FirstOrDefault(step => step.Command == command);
            if (repeated is not null)
            {
                Fail(name, $"repeats {repeated.Name}");

                return false;
            }

            // The mission start creates a building's settlers inside it: the huts of the jobs whose rules let their settler stand in the
            // hut (the woodcutter's, the stonecutter's, and the forester's) stand with their workers.
            steps.Add(
                new Step
                {
                    Name = name,
                    Command = command,
                    Place = game =>
                        PlaceableNames(game.Tables.Catalog).ToList() is var placeable && !placeable.Contains(type.Name)
                            ? $"the building type {given} is no stable name of a building type of this content set, use {string.Join(", ", placeable)}"
                        : !game.PlaceFinishedBuilding(type, x, y) ? BuildingRefusal
                        : game.Walks.StandsWorker(type) ? game.Walks.StandWorkerInHut(x, y)
                        : null,
                }
            );

            return true;
        }

        // The entry states a settler starts in (docs/rules/layouts.md): each job with its states.
        const string SettlerShape =
            "{\"job\": \"carrier\", \"state\": \"waiting-on-road\", \"x\", \"y\", \"direction\"} (a flag of the road and "
            + "the direction the road leaves it)";

        bool ReadSettler(string name, JsonElement entry, List<Step> steps)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Fail(name, $"must be {SettlerShape}");

                return false;
            }

            string? Text(string key) =>
                entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            var job = Text("job");
            var state = Text("state");
            if (job != "carrier" || state != "waiting-on-road")
            {
                Fail(name, $"no entry state: a settler is {SettlerShape}");

                return false;
            }

            var unknown = entry
                .EnumerateObject()
                .Select(p => p.Name)
                .FirstOrDefault(n => n is not ("job" or "state" or "x" or "y") && n != "direction");
            if (unknown is not null)
            {
                Fail(name, $"unknown key {unknown}, a settler is {SettlerShape}");

                return false;
            }

            var direction = 0;
            if (
                !Whole(entry, "x", out var x)
                || !Whole(entry, "y", out var y)
                || !Whole(entry, "direction", out direction)
                || direction is < 0 or > 5
            )
            {
                Fail(name, "x, y, and a direction from 0 to 5 must be whole numbers");

                return false;
            }

            steps.Add(
                new Step
                {
                    Name = name,
                    Command = default,
                    Place = game => game.Carriers.PlaceCarrierOnRoad(x, y, direction),
                }
            );

            return true;
        }

        bool ReadGood(string name, JsonElement entry, List<Step> steps)
        {
            const string Shape = "{\"x\", \"y\", \"place\": 0 to 7, \"good\": a stable name}";
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Fail(name, $"must be {Shape}");

                return false;
            }

            var unknown = entry
                .EnumerateObject()
                .Select(p => p.Name)
                .FirstOrDefault(n => n is not ("x" or "y" or "place" or "good"));
            if (unknown is not null)
            {
                Fail(name, $"unknown key {unknown}, a good is {Shape}");

                return false;
            }

            if (
                !Whole(entry, "x", out var x)
                || !Whole(entry, "y", out var y)
                || !Whole(entry, "place", out var place)
                || place is < 0 or > 7
                || !entry.TryGetProperty("good", out var named)
            )
            {
                Fail(name, $"must be {Shape}, x, y, and place whole numbers");

                return false;
            }

            // The game's catalog is not known while the file is read: a number or a name the catalog lacks is refused when the layout is
            // built, with the names the game's content set knows.
            var good = new Good { Name = named.ValueKind == JsonValueKind.String ? named.GetString() ?? "" : "" };
            var given = named.GetRawText();
            steps.Add(
                new Step
                {
                    Name = name,
                    Command = default,
                    Place = game =>
                        good.IsNone || game.Tables.Catalog.EntryOf(good) is null
                            ? $"the good {given} is no stable name of a good of this content set, use {string.Join(", ", game.Tables.Catalog.Goods.Select(entry => entry.Name))}"
                            : game.Goods.PlaceGood(x, y, place, good),
                }
            );

            return true;
        }

        static string Shape(int kind) =>
            kind == CommandKind.BuildRoad ? "{\"x\", \"y\", \"directions\"}" : "{\"x\", \"y\"}";

        // The size of the map bounds the settlers a castle names (null where the size is bad, and for every other entry).
        bool ReadEntry(string name, JsonElement entry, int kind, List<Step> steps, MapSize? size = null)
        {
            var road = kind == CommandKind.BuildRoad;
            var castle = kind == CommandKind.PlaceCastle;
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Fail(name, $"must be {Shape(kind)}");

                return false;
            }

            var unknown = entry
                .EnumerateObject()
                .Select(p => p.Name)
                .FirstOrDefault(n =>
                    n is not ("x" or "y") && !(road && n == "directions") && !(castle && n is "stock" or "settlers")
                );
            if (unknown is not null)
            {
                Fail(name, $"unknown key {unknown}, an entry is {Shape(kind)}");

                return false;
            }

            if (!Whole(entry, "x", out var x) || !Whole(entry, "y", out var y))
            {
                Fail(name, "x and y must each be a whole number");

                return false;
            }

            int[]? directions = null;
            if (road && !ReadDirections(name, entry, out directions))
            {
                return false;
            }

            Dictionary<Good, int>? stock = null;
            if (castle && entry.TryGetProperty("stock", out var stockEntry) && !ReadStock(name, stockEntry, out stock))
            {
                return false;
            }

            Job[]? settlers = null;
            if (
                castle
                && entry.TryGetProperty("settlers", out var settlersEntry)
                && !ReadSettlers(name, settlersEntry, size, out settlers)
            )
            {
                return false;
            }

            var command = new Command
            {
                Tick = 0,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
                Stock = stock,
                Settlers = settlers,
            };
            var repeated = steps.FirstOrDefault(step => step.Place is null && step.Command == command);
            if (repeated is not null)
            {
                Fail(name, $"repeats {repeated.Name}");

                return false;
            }

            steps.Add(
                new Step
                {
                    Name = name,
                    Command = command,
                    Check = stock is null ? null : game => StockRefusal(game, stock),
                }
            );

            return true;
        }

        // The game's catalog is not known while the file is read: a good of the castle's stock that the catalog lacks is refused when
        // the layout is built, with the names the game's content set knows.
        static string? StockRefusal(Game game, IReadOnlyDictionary<Good, int> stock) =>
            stock
                .Keys.Where(good => game.Tables.Catalog.EntryOf(good) is null)
                .Select(good => good.Name)
                .FirstOrDefault()
                is { } unknown
                ? $"stock: the good {unknown} is no stable name of a good of this content set, use {string.Join(", ", game.Tables.Catalog.Goods.Select(entry => entry.Name))}"
                : null;

        // The castle's stock key: an object of goods by stable name and counts, each a whole number from 0 to 50000 (the most a count
        // of CastleStock holds). Every good it does not name counts 0. An engine stand-in: the original has no such start.
        bool ReadStock(string name, JsonElement entry, out Dictionary<Good, int>? stock)
        {
            stock = null;
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Fail(name, "stock must be an object of good names and counts, such as {\"grain\": 1}");

                return false;
            }

            var counts = new Dictionary<Good, int>();
            var good = true;
            foreach (var property in entry.EnumerateObject())
            {
                if (counts.ContainsKey(new Good { Name = property.Name }))
                {
                    Fail(name, $"stock: {property.Name} is given twice");
                    good = false;
                }
                else if (
                    property.Value.ValueKind != JsonValueKind.Number
                    || !property.Value.TryGetInt32(out var count)
                    || count is < 0 or > 50000
                )
                {
                    Fail(name, $"stock: {property.Name}: the count must be a whole number from 0 to 50000");
                    good = false;
                }
                else
                {
                    counts[new Good { Name = property.Name }] = count;
                }
            }

            stock = good ? counts : null;

            return good;
        }

        // The jobs with a stable name (Job.Ported) but the castle settler, by name: the jobs a castle's named settlers take.
        static readonly Dictionary<string, Job> JobsByName = Job
            .Ported.Where(job => job.Name != CastleSettlerName)
            .ToDictionary(job => job.Name);

        const string CastleSettlerName = "castle-settler";

        static readonly string JobNameList =
            string.Join(", ", JobsByName.Keys.SkipLast(1)) + ", or " + JobsByName.Keys.Last();

        // The castle's settlers key: an object of job names and counts, each count a whole number from 0, whose settlers wait in the
        // castle after its castle settler, each job's together in the order the file names them. With the castle settler they fit in
        // the settler table of the map's size less the one place the game keeps free (SettlerTable.IsFull). An engine stand-in.
        bool ReadSettlers(string name, JsonElement entry, MapSize? size, out Job[]? settlers)
        {
            settlers = null;
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Fail(name, "settlers must be an object of job names and counts, such as {\"jobless\": 1}");

                return false;
            }

            var named = new List<Job>();
            var seen = new HashSet<string>();
            var good = true;
            foreach (var property in entry.EnumerateObject())
            {
                if (property.Name == CastleSettlerName)
                {
                    Fail(
                        name,
                        $"settlers: {CastleSettlerName} is the castle's own settler, whom the castle always holds and no layout names"
                    );
                    good = false;
                }
                else if (!JobsByName.TryGetValue(property.Name, out var job))
                {
                    Fail(name, $"settlers: {property.Name} is no job's name: a castle names {JobNameList}");
                    good = false;
                }
                else if (!seen.Add(property.Name))
                {
                    Fail(name, $"settlers: {property.Name} is given twice");
                    good = false;
                }
                else if (
                    property.Value.ValueKind != JsonValueKind.Number
                    || !property.Value.TryGetInt32(out var count)
                    || count < 0
                )
                {
                    Fail(name, $"settlers: {property.Name}: the count must be a whole number from 0");
                    good = false;
                }
                else if (size is null)
                {
                    // A bad or missing size fails the load already; with no table to bound the count, no settlers are made.
                    good = false;
                }
                else if (named.Count + (long)count + 1 > SettlerTable.SizeOf(size) - 1)
                {
                    Fail(
                        name,
                        $"settlers: {named.Count + (long)count} settlers and the castle settler are more than the "
                            + $"{SettlerTable.SizeOf(size) - 1} the settler table holds"
                    );
                    good = false;
                }
                else
                {
                    named.AddRange(Enumerable.Repeat(job, count));
                }
            }

            settlers = good ? [.. named] : null;

            return good;
        }

        // The directions of a road: one or more whole numbers from 0 to 5. A bad list is a problem naming the entry.
        bool ReadDirections(string name, JsonElement entry, out int[]? directions)
        {
            directions = null;
            if (
                !entry.TryGetProperty("directions", out var list)
                || list.ValueKind != JsonValueKind.Array
                || list.GetArrayLength() == 0
                || list.EnumerateArray()
                    .Any(d => d.ValueKind != JsonValueKind.Number || !d.TryGetInt32(out var v) || v is < 0 or > 5)
            )
            {
                Fail(name, "directions must be a list of one or more whole numbers from 0 to 5");

                return false;
            }

            directions = [.. list.EnumerateArray().Select(d => d.GetInt32())];

            return true;
        }
    }
}
