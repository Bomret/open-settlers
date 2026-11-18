using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;
using static OpenSettlers.Adapters.Testing.WorkerBytes;

namespace OpenSettlers.Tests;

// The lines of worker-walks.txt are the output of the emulator verb worker (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes over the item tables (0x428728, 0x42cb2d, 0x42f3d9) on a game started as the road verb starts one,
// with a finished woodcutter's hut at a flag. Each scenario starts with a "#" line that names its verb command; the
// lines after it give, for the passes where the bytes changed, the 16 bytes of the castle's woodcutter (slot 10 of the
// settler table, s10=) and the 18 bytes of the hut (building slot 1, b=), as hexadecimal. A scenario's lines end with
// the pass at which the worker's first turn in state 4 (stepping through the hut's door) left its bytes; what the
// original does next belongs to the woodcutter's work. The engine runs the same commands and must hold the same bytes
// after each pass, which tests the route search, the retry scan, the request, the step times and the door speeds.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class WorkerWalkTests
{
    const string Fixture = "worker-walks.txt";
    const int WorkerSlot = 10;
    const int HutSlot = 1;

    sealed record Scenario
    {
        public required string Name { get; init; }

        public required string Command { get; init; }

        public required List<(int Pass, string? Worker, string? Hut)> Lines { get; init; }
    }

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    static List<Scenario> Load()
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(Fixture))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var parts = line[2..].Split(' ', 2);
                scenarios.Add(
                    new Scenario
                    {
                        Name = parts[0],
                        Command = parts[1],
                        Lines = [],
                    }
                );
                continue;
            }

            var fields = line.Split(' ')
                .Select(field => field.Split('='))
                .ToDictionary(pair => pair[0], pair => pair[1]);
            var pass = fields["pass"] == "start" ? -1 : int.Parse(fields["pass"], CultureInfo.InvariantCulture);
            scenarios[^1].Lines.Add((pass, fields.GetValueOrDefault("s10"), fields.GetValueOrDefault("b")));
        }

        return scenarios;
    }

    static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheWorkerWalksAsTheOriginalsPassesMoveIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        // worker:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES[:WARM[:LATEROADS:AT]]
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(
            TestProgram.Tables(),
            new MapSeed
            {
                W0 = seed[0],
                W1 = seed[1],
                W2 = seed[2],
            },
            size!
        );
        var game = GameFactory.Create(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
        var castle = Points(parts[4]).Single();
        var hut = parts[7].Split(',').Select(int.Parse).ToArray();
        var late = parts.Length > 11 ? parts[10] : "";
        var lateAt = parts.Length > 11 ? int.Parse(parts[11], CultureInfo.InvariantCulture) : -1;
        var passes = int.Parse(parts[8], CultureInfo.InvariantCulture);

        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = castle[0],
                Y = castle[1],
            }
        );
        foreach (var flag in Points(parts[5]))
        {
            game.Enqueue(
                new Command
                {
                    Tick = 0,
                    Player = 0,
                    Kind = CommandKind.PlaceFlag,
                    X = flag[0],
                    Y = flag[1],
                }
            );
        }

        foreach (var road in parts[6].Split(';').Select(text => text.Split(',')))
        {
            game.Enqueue(
                new Command
                {
                    Tick = 0,
                    Player = 0,
                    Kind = CommandKind.BuildRoad,
                    X = int.Parse(road[0]),
                    Y = int.Parse(road[1]),
                    Directions = [.. road[2].Select(c => c - '0')],
                }
            );
        }

        game.ApplyCommands();
        Assert.IsTrue(Huts.Place(game, hut[0] - 1, hut[1] - 1), "the fixture's hut has its site");
        if (late.Length > 0)
        {
            foreach (var road in late.Split(';').Select(text => text.Split(',')))
            {
                game.Enqueue(
                    new Command
                    {
                        Tick = lateAt,
                        Player = 0,
                        Kind = CommandKind.BuildRoad,
                        X = int.Parse(road[0]),
                        Y = int.Parse(road[1]),
                        Directions = [.. road[2].Select(c => c - '0')],
                    }
                );
            }
        }

        var last = scenario.Lines.Where(line => line.Pass >= 0).Max(line => line.Pass);
        // The start line holds the bytes after the commands and before the first pass.
        var worker = scenario.Lines.Single(line => line.Pass == -1).Worker;
        var building = scenario.Lines.Single(line => line.Pass == -1).Hut;
        for (var pass = 0; pass <= Math.Min(last, passes - 1); pass++)
        {
            game.Tick();
            foreach (var line in scenario.Lines.Where(line => line.Pass == pass))
            {
                worker = line.Worker ?? worker;
                building = line.Hut ?? building;
            }

            Assert.AreEqual(
                worker,
                Hex(game.Settlers[WorkerSlot], map.Size.Width),
                $"{name}: the worker after pass {pass}"
            );
            Assert.AreEqual(
                building,
                Hex(game.Buildings[HutSlot], map.Size.Width),
                $"{name}: the hut after pass {pass}"
            );
        }
    }
}
