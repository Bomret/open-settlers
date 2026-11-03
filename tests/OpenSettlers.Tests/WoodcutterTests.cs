using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of wood-walks.txt are the output of the emulator verb wood (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes over the item tables and the map's object pass (0x428728, 0x428d10, 0x46414d, 0x42cb2d, 0x42f3d9)
// on a game started as the road verb starts one, with a finished woodcutter's hut (type 2) at a flag. Each scenario
// starts with a "#" line that names its verb command; the lines after it give, for a pass, the 16 bytes of the castle's
// woodcutter (slot 10, s10=) where it entered a new state or node (and at the checkpoints), the 18 bytes of the hut
// (b=) where they changed, the object bytes that changed (o=x,y:old>new;...), the flag's byte 4 and goods bytes (g=)
// where they changed, and the generator's words (rng=) where they moved. The engine runs the same commands and must
// hold the same values at each listed pass, and no object byte may change at any other pass.
[TestClass]
public sealed class WoodcutterTests
{
    private const string Fixture = "wood-walks.txt";
    private const int WorkerSlot = 10;
    private const int HutSlot = 1;

    private sealed record Scenario(string Name, string Command, List<Dictionary<string, string>> Lines);

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    private static List<Scenario> Load()
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(Fixture))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var parts = line[2..].Split(' ', 2);
                scenarios.Add(new Scenario(parts[0], parts[1], []));
                continue;
            }

            scenarios[^1]
                .Lines.Add(line.Split(' ').Select(field => field.Split('=')).ToDictionary(p => p[0], p => p[1]));
        }

        return scenarios;
    }

    private static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    private static string Words(RandomGenerator generator) =>
        string.Join(',', new[] { generator.W0, generator.W1, generator.W2 }.Select(word => word.ToString("X4")));

    // The object bytes of a map that differ from a copy, as the verb lists them: "x,y:old>new" joined by ";".
    private static string Changes(byte[] before, Map map)
    {
        var now = map.Layer(MapLayer.Object).ToArray();
        var width = map.Size.Width;

        return string.Join(
            ';',
            Enumerable
                .Range(0, now.Length)
                .Where(index => now[index] != before[index])
                .Select(index => $"{index % width},{index / width}:{before[index]:X2}>{now[index]:X2}")
        );
    }

    private static Game Start(string command, out Map map, out int hutFlag, out int passes)
    {
        // wood:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES
        var parts = command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        map = MapGenerator.Generate(new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = new Game(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
        var castle = Points(parts[4]).Single();
        var flags = Points(parts[5]);
        var hut = parts[7].Split(',').Select(int.Parse).ToArray();
        passes = int.Parse(parts[8], CultureInfo.InvariantCulture);

        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, castle[0], castle[1]));
        foreach (var flag in flags)
        {
            game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, flag[0], flag[1]));
        }

        foreach (var road in parts[6].Split(';').Select(text => text.Split(',')))
        {
            game.Enqueue(
                new Command(
                    0,
                    0,
                    CommandKind.BuildRoad,
                    int.Parse(road[0]),
                    int.Parse(road[1]),
                    [.. road[2].Select(c => c - '0')]
                )
            );
        }

        game.Enqueue(new Command(0, 0, CommandKind.PlaceBuilding, hut[0] - 1, hut[1] - 1));
        hutFlag = flags.FindIndex(flag => flag[0] == hut[0] && flag[1] == hut[1]) + 1;

        return game;
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheWoodcutterWorksAsTheOriginalsPassesRunIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        var game = Start(scenario.Command, out var map, out var hutFlag, out var passes);
        var width = map.Size.Width;
        var last = scenario.Lines.Max(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));

        for (var pass = 0; pass <= Math.Min(last, passes - 1); pass++)
        {
            var before = map.Layer(MapLayer.Object).ToArray();
            game.Tick();
            if (pass == 0)
            {
                // The commands of the first tick put the castle, the flags, and the hut on the map; the verb lists changes from there.
                before = map.Layer(MapLayer.Object).ToArray();
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

            if (line.TryGetValue("s10", out var worker))
            {
                Assert.AreEqual(
                    worker,
                    WorkerWalkTests.Hex(game.Settlers[WorkerSlot], width),
                    $"{name}: the worker after pass {pass}"
                );
            }

            if (line.TryGetValue("b", out var hut))
            {
                Assert.AreEqual(
                    hut,
                    WorkerWalkTests.Hex(game.Buildings[HutSlot], width),
                    $"{name}: the hut after pass {pass}"
                );
            }

            if (line.TryGetValue("g", out var goods))
            {
                var places = game.FlagGoods(hutFlag);
                var bytes = string.Concat(places.Select(good => good.ToString("X2")));
                Assert.AreEqual(goods.Split('/')[1], bytes, $"{name}: the goods on the hut's flag after pass {pass}");
                Assert.AreEqual(
                    Convert.ToInt32(goods.Split('/')[0], 16) >> 7,
                    places.Any(good => good != 0) ? 1 : 0,
                    $"{name}: the flag's goods bit after pass {pass}"
                );
            }

            if (line.TryGetValue("rng", out var rng))
            {
                Assert.AreEqual(rng, Words(game.Random), $"{name}: the random generator after pass {pass}");
            }
        }
    }
}
