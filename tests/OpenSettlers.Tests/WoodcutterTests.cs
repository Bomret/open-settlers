using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of wood-walks.txt are the output of the emulator verb goods (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes over the item tables and the map's object pass (0x428728, 0x428d10, 0x46414d, 0x42cb2d, 0x42f3d9)
// on a game started as the original's game start leaves its tables, with a finished woodcutter's hut (type 2) at a flag, and
// the carriers that carry the logs to the castle (the whole run is checked, with the carriers' walks in the woodcutter's
// way). Each scenario
// starts with a "#" line that names its verb command; the lines after it give, for a pass, the 16 bytes of the castle's
// woodcutter (slot 10, s10=) where it entered a new state or node (and at the checkpoints), the 18 bytes of the hut
// (b=) where they changed, the object bytes that changed (o=x,y:old>new;...), the flag's byte 4 and goods bytes (g=)
// where they changed, the generator's words (rng=) where they moved, and the 16 bytes of a castle knight (s6= to s8=) at
// the pass that promoted it. The engine runs the same commands and must hold the same values at each listed pass, and no
// object byte may change at any other pass.
//
// **Inferred input:** the verb writes the marks 0x42 on the hut's node and 0x10 on its flag's node by hand, to match the
// engine's stand-in for the castle's placement marks (docs/rules/settlers.md): no emulator run of the original's own placement
// backs them. The expected walk of the woodcutter depends on them (it avoids a node marked 0x40). Open lead in
// docs/milestones/m3-first-loop.md.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class WoodcutterTests
{
    const string Fixture = "wood-walks.txt";
    const int WorkerSlot = 10;
    const int HutSlot = 1;

    sealed record Scenario(string Name, string Command, List<Dictionary<string, string>> Lines);

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    static List<Scenario> Load()
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

    // The verb goods numbers the flags one higher than the engine (the dummy of the original's game start, docs/rules/goods.md).
    // The word of the bytes at the hex offset holds a flag in the settler's states that walk to a flag (2, 5, 6, 0xf)
    // and in the hut's item; there it is the engine's slot plus one, and 0 where there is none. A word beyond the
    // flags (the engine has flagCount) is no flag, and neither is the word of a settler in another state.
    internal static string InEngineNumbers(string bytes, int offset, int flagCount)
    {
        var isFlagWord = offset == 12 || Convert.ToInt32(bytes[20..22], 16) is 2 or 5 or 6 or 0xf;
        var word = Convert.ToInt32(string.Concat(bytes.AsSpan(offset + 2, 2), bytes.AsSpan(offset, 2)), 16);

        return isFlagWord && word >= 1 && word <= flagCount
            ? string.Concat(
                bytes.AsSpan(0, offset),
                (word - 1).ToString("X2", CultureInfo.InvariantCulture),
                bytes.AsSpan(offset + 2)
            )
            : bytes;
    }

    static Game Start(string command, out Map map, out int hutFlag, out int passes)
    {
        // wood:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES
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
        Assert.IsTrue(Huts.Place(game, hut[0] - 1, hut[1] - 1), "the fixture's hut has its site");
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
            var before = Layers.Of(map, MapLayer.Object);
            game.Tick();
            if (pass == 0)
            {
                // The commands of the first tick put the castle, the flags, and the hut on the map; the verb lists changes from there.
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

            if (line.TryGetValue("s10", out var worker))
            {
                // The word at 12 holds the hut's flag while the worker walks to it or from it.
                Assert.AreEqual(
                    InEngineNumbers(worker, 24, game.Flags.Count),
                    WorkerWalkTests.Hex(game.Settlers[WorkerSlot], width),
                    $"{name}: the worker after pass {pass}"
                );
            }

            if (line.TryGetValue("b", out var hut))
            {
                Assert.AreEqual(
                    InEngineNumbers(hut, 12, game.Flags.Count),
                    WorkerWalkTests.Hex(game.Buildings[HutSlot], width),
                    $"{name}: the hut after pass {pass}"
                );
            }

            if (line.TryGetValue("g", out var goods))
            {
                var places = game.Flags[hutFlag].Places;
                var bytes = string.Concat(places.Select(place => FlagEncoder.GoodByte(place).ToString("X2")));
                Assert.AreEqual(goods.Split('/')[1], bytes, $"{name}: the goods on the hut's flag after pass {pass}");
                Assert.AreEqual(
                    Convert.ToInt32(goods.Split('/')[0], 16) >> 7,
                    game.Flags[hutFlag].NeedsGoodsTurn ? 1 : 0,
                    $"{name}: the flag's goods bit after pass {pass}"
                );
            }

            // A knight of the castle (slots 6 to 8) where it was promoted: the bytes after its job went up by one.
            foreach (var knight in new[] { 6, 7, 8 })
            {
                if (line.TryGetValue($"s{knight}", out var bytes))
                {
                    Assert.AreEqual(
                        bytes,
                        WorkerWalkTests.Hex(game.Settlers[knight], width),
                        $"{name}: the knight of slot {knight} after pass {pass}"
                    );
                }
            }

            if (line.TryGetValue("rng", out var rng))
            {
                Assert.AreEqual(rng, Words(game.Draws.Generator), $"{name}: the random generator after pass {pass}");
            }
        }
    }
}
