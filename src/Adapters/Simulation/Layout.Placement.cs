using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OpenSettlers.Simulation;

public sealed partial class Layout
{
    // One entry of the file that a game takes as a command: its name in the file (flags[1]) and the command; a refusal
    // reports the reason Refusal gives for the command's kind. A building, settler, or good entry is no command a player gives:
    // its Place puts it in the game directly (Game.PlaceFinishedBuilding, WalkBehavior.StandWorkerInHut,
    // CarrierBehavior.PlaceCarrierOnRoad, GoodsTransport.PlaceGood) and returns why it refused, or null.
    sealed record Step
    {
        public required string Name { get; init; }

        public required Command Command { get; init; }

        public Func<Game, string?>? Place { get; init; } = null;
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
        List<Step>? ReadSteps(JsonElement root)
        {
            var steps = new List<Step>();
            var good = true;
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "castle":
                        good &= ReadEntry("castle", property.Value, CommandKind.PlaceCastle, steps);
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

        // The building types a finished-building entry names (docs/rules/layouts.md), by the names of the build panel.
        static readonly Dictionary<string, BuildingType> BuildingNames = new()
        {
            ["woodcutter"] = BuildingType.Woodcutter,
            ["stonecutter"] = BuildingType.Stonecutter,
            ["forester"] = BuildingType.Forester,
            ["sawmill"] = BuildingType.Sawmill,
        };

        const string BuildingShape =
            "{\"type\": \"woodcutter\", \"stonecutter\", \"forester\", or \"sawmill\", \"x\", \"y\"} (the node of the building, "
            + "finished with its flag south-east of it)";

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

            if (!entry.TryGetProperty("type", out var typeEntry) || typeEntry.ValueKind != JsonValueKind.String)
            {
                Fail(name, $"a building needs a \"type\": a building is {BuildingShape}");

                return false;
            }

            var typeName = typeEntry.GetString();
            if (typeName is null || !BuildingNames.TryGetValue(typeName, out var type))
            {
                Fail(name, $"no building type {typeName}: M4 builds woodcutter, stonecutter, forester, or sawmill");

                return false;
            }

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
                        !game.PlaceFinishedBuilding(type, x, y) ? BuildingRefusal
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
            const string Shape = "{\"x\", \"y\", \"place\": 0 to 7, \"good\": 1 to 25}";
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
                || !Whole(entry, "good", out var good)
                || good is < 1 or >= StartStock.Goods
            )
            {
                Fail(name, $"must be {Shape}, whole numbers");

                return false;
            }

            steps.Add(
                new Step
                {
                    Name = name,
                    Command = default,
                    Place = game => game.Goods.PlaceGood(x, y, place, good),
                }
            );

            return true;
        }

        static string Shape(int kind) =>
            kind == CommandKind.BuildRoad ? "{\"x\", \"y\", \"directions\"}" : "{\"x\", \"y\"}";

        bool ReadEntry(string name, JsonElement entry, int kind, List<Step> steps)
        {
            var road = kind == CommandKind.BuildRoad;
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Fail(name, $"must be {Shape(kind)}");

                return false;
            }

            var unknown = entry
                .EnumerateObject()
                .Select(p => p.Name)
                .FirstOrDefault(n => n is not ("x" or "y") && !(road && n == "directions"));
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

            var command = new Command
            {
                Tick = 0,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
            };
            var repeated = steps.FirstOrDefault(step => step.Place is null && step.Command == command);
            if (repeated is not null)
            {
                Fail(name, $"repeats {repeated.Name}");

                return false;
            }

            steps.Add(new Step { Name = name, Command = command });

            return true;
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
