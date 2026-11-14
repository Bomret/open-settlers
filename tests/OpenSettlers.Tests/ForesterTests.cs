using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of forester-walks.txt are the output of the emulator verb goods (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes over the item tables and the map's object pass on a game started as the original's game start leaves its tables, with a
// finished forester's building (type 9) at a flag, whose worker is a settler without a job the castle sends out (the castle holds no
// forester). A scenario starts with a "#" line that names its verb command; the lines after it give, for a pass, the 16 bytes of the
// forester (slot 4, s4=) where his state changed (and at a checkpoint), the 18 bytes of the building (b=) where they changed, the
// object bytes that changed (o=x,y:old>new;...), and the generator's words (rng=). The engine runs the same commands and must hold the
// same values at each listed pass, and no object byte may change at any other pass. The saplings the verb's SET field puts on the map
// grow into trees at the passes the object pass gives them.
//
// **Inferred input:** the verb writes the marks 0x42 on the building's node and 0x10 on its flag's node by hand, as the
// woodcutter's scenarios have them (WoodcutterTests).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class ForesterTests
{
    const string Fixture = "forester-walks.txt";
    const int WorkerSlot = 4;
    const int HutSlot = 1;

    sealed record Scenario
    {
        public required string Name { get; init; }

        public required string Command { get; init; }

        public required List<Dictionary<string, string>> Lines { get; init; }
    }

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    static List<Scenario> Load()
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(Fixture).Where(line => !line.StartsWith("//", StringComparison.Ordinal)))
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

            scenarios[^1]
                .Lines.Add(line.Split(' ').Select(field => field.Split('=')).ToDictionary(p => p[0], p => p[1]));
        }

        return scenarios;
    }

    static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    static string Words(RandomGenerator generator) =>
        string.Join(',', new[] { generator.W0, generator.W1, generator.W2 }.Select(word => word.ToString("X4")));

    // The object bytes of a map that differ from a copy, as the verb lists them: "x,y:old>new" joined by ";".
    static string Changes(byte[] before, Map map)
    {
        var now = Layers.Of(map, MapLayer.Object);
        var width = map.Size.Width;

        return string.Join(
            ';',
            Enumerable
                .Range(0, now.Length)
                .Where(index => now[index] != before[index])
                .Select(index => $"{index % width},{index / width}:{before[index]:X2}>{now[index]:X2}")
        );
    }

    static Game Start(string command, out Map map, out int passes, out List<(int Pass, int X, int Y, int Code)> sets)
    {
        // goods:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES[:WARM[:LATEROADS:AT[:PUT;PUT...[:SET;SET...]]]]
        var parts = command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        map = MapGenerator.Generate(
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
        var flags = Points(parts[5]);
        var hut = parts[7].Split(',').Select(int.Parse).ToArray();
        passes = int.Parse(parts[8], CultureInfo.InvariantCulture);

        // Each set writes an object code (decimal) into a node's object byte before the pass.
        sets =
        [
            .. parts.Length > 13
                ? parts[13]
                    .Split(';')
                    .Select(text => text.Split(',').Select(int.Parse).ToArray())
                    .Select(set => (set[0], set[1], set[2], set[3]))
                : [],
        ];

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
        foreach (var flag in flags)
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
        Assert.AreEqual(BuildingType.Forester.Code, hut[2], "the fixture's building is a forester's");
        Assert.IsTrue(
            Huts.Place(game, hut[0] - 1, hut[1] - 1, BuildingType.Forester),
            "the fixture's building has its site"
        );

        return game;
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheForesterPlantsAndTheSaplingsGrowAsTheOriginalsPassesRunThem(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        var game = Start(scenario.Command, out var map, out var passes, out var sets);
        var width = map.Size.Width;
        var last = scenario.Lines.Max(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));

        for (var pass = 0; pass <= Math.Min(last, passes - 1); pass++)
        {
            var before = Layers.Of(map, MapLayer.Object);
            foreach (var set in sets.Where(set => set.Pass == pass))
            {
                map.At(set.X, set.Y).Object = new MapObject { Code = (byte)set.Code };
            }

            game.Tick();
            if (pass == 0)
            {
                // The commands of the first tick put the castle, the flags, and the building on the map; the verb lists changes from there.
                before = Layers.Of(map, MapLayer.Object);
            }

            var line = scenario.Lines.SingleOrDefault(candidate =>
                candidate["pass"] == pass.ToString(CultureInfo.InvariantCulture)
            );
            Assert.AreEqual(
                line?.GetValueOrDefault("o") ?? "",
                Changes(before, map),
                $"{name}: the objects that changed at pass {pass}"
            );
            if (line is null)
            {
                continue;
            }

            if (line.TryGetValue("s4", out var worker))
            {
                Assert.AreEqual(
                    WoodcutterTests.InEngineNumbers(worker, 24, game.Flags.Count),
                    WorkerWalkTests.Hex(game.Settlers[WorkerSlot], width),
                    $"{name}: the forester after pass {pass}"
                );
            }

            if (line.TryGetValue("b", out var hutBytes))
            {
                Assert.AreEqual(
                    WoodcutterTests.InEngineNumbers(hutBytes, 12, game.Flags.Count),
                    WorkerWalkTests.Hex(game.Buildings[HutSlot], width),
                    $"{name}: the building after pass {pass}"
                );
            }

            if (line.TryGetValue("rng", out var rng))
            {
                Assert.AreEqual(rng, Words(game.Draws.Generator), $"{name}: the random generator after pass {pass}");
            }
        }
    }
}
