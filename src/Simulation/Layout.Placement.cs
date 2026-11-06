using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OpenSettlers.Simulation;

public sealed partial class Layout
{
    // One entry of the file that a game takes as a command: its name in the file (flags[1]), the command, and why the
    // original refuses that command, for the problem a refusal reports. A later entry kind (settlers, goods) adds a
    // reader to Reader.ReadSteps and a step here.
    private sealed record Step(string Name, Command Command, string Refusal);

    /// <summary>
    /// Builds the castle, flags, roads, and buildings of the layout in the game, one entry at a time in the order the
    /// file lists them (key by key, entry by entry), as <see cref="Game.Enqueue"/> takes them at the game's tick: the
    /// effect of each entry is known before the next is queued. Returns null when every entry was built, or the
    /// problem <c>file: entry: reason</c> of the first entry the game refused, which stops the build. A refusal leaves
    /// the earlier entries in the game, so the caller shows nothing of it and starts no play on that game.
    /// </summary>
    public string? Apply(Game game)
    {
        foreach (var step in _steps)
        {
            var revision = game.Revision;
            game.Enqueue(step.Command with { Tick = game.TickCount });
            game.ApplyCommands();
            if (game.Revision == revision)
            {
                return $"{_file}: {step.Name}: {step.Refusal}";
            }
        }

        return null;
    }

    private sealed partial class Reader
    {
        // The castle, flags, roads, and buildings entries in the order the file holds them. Null when an entry is bad
        // or repeats an earlier one of its kind.
        private List<Step>? ReadSteps(JsonElement root)
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
                        good &= ReadList(property, CommandKind.PlaceBuilding, steps);
                        break;
                }
            }

            return good ? steps : null;
        }

        private bool ReadList(JsonProperty property, int kind, List<Step> steps)
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

        private static string Shape(int kind) =>
            kind == CommandKind.BuildRoad ? "{\"x\", \"y\", \"directions\"}" : "{\"x\", \"y\"}";

        private bool ReadEntry(string name, JsonElement entry, int kind, List<Step> steps)
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
            if (
                road
                && (
                    !entry.TryGetProperty("directions", out var list)
                    || list.ValueKind != JsonValueKind.Array
                    || list.GetArrayLength() == 0
                    || list.EnumerateArray()
                        .Any(d => d.ValueKind != JsonValueKind.Number || !d.TryGetInt32(out var v) || v is < 0 or > 5)
                )
            )
            {
                Fail(name, "directions must be a list of one or more whole numbers from 0 to 5");

                return false;
            }

            if (road)
            {
                directions = entry.GetProperty("directions").EnumerateArray().Select(d => d.GetInt32()).ToArray();
            }

            var command = new Command(0, 0, kind, x, y, directions);
            var repeated = steps.FirstOrDefault(step => step.Command == command);
            if (repeated is not null)
            {
                Fail(name, $"repeats {repeated.Name}");

                return false;
            }

            steps.Add(new Step(name, command, Refusal(kind)));

            return true;
        }

        // Why the original refuses each kind (the documentation of the command kind).
        private static string Refusal(int kind) =>
            kind switch
            {
                CommandKind.PlaceCastle => "the node is no castle site, the original refuses it there",
                CommandKind.PlaceFlag =>
                    "the original's flag button does not allow a flag on the node: the castle must stand, and the node "
                        + "must hold no flag, no building, and no object that blocks it",
                CommandKind.BuildRoad =>
                    "a step is not allowed where the original's road drawing allows it, or the last step reaches no "
                        + "flag; list the flag at the end before the road",
                _ => "the node is no site for a hut with the flag south-east of it standing and free",
            };
    }
}
