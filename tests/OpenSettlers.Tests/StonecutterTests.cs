using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of stone-walks.txt are the output of the emulator verb goods (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes over the item tables and the map's object pass on a game started as the original's game start leaves its tables, with a
// finished stonecutter's building (type 4) at a flag, and the carriers that carry the stones to the castle (the whole run is
// checked). A scenario starts with a "#" line that names its verb command; the lines after it give, for a pass, the 16 bytes of
// the castle's stonecutter (slot 12, s12=) where his node, state, or part of the cut changed, the 18 bytes of the building (b=)
// where they changed, the object bytes that changed (o=x,y:old>new;...), the flag's byte 4 and goods bytes (g=) where they
// changed, and the generator's words (rng=). The engine runs the same commands and must hold the same values at each listed
// pass, and no object byte may change at any other pass.
//
// **Inferred input:** the verb writes the marks 0x42 on the building's node and 0x10 on its flag's node by hand, as the
// woodcutter's scenarios have them (WoodcutterTests).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class StonecutterTests
{
    private const string Fixture = "stone-walks.txt";
    private const int WorkerSlot = 12;
    private const int HutSlot = 1;

    private sealed record Scenario(string Name, string Command, List<Dictionary<string, string>> Lines);

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    private static List<Scenario> Load()
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(Fixture).Where(line => !line.StartsWith("//", StringComparison.Ordinal)))
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

    private static Game Start(string command, out Map map, out int hutFlag, out int passes)
    {
        // goods:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES
        var parts = command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = GameFactory.Create(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
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

        game.ApplyCommands();
        Assert.AreEqual(BuildingType.Stonecutter.Code, hut[2], "the fixture's building is a stonecutter's");
        Assert.IsTrue(
            Huts.Place(game, hut[0] - 1, hut[1] - 1, BuildingType.Stonecutter),
            "the fixture's building has its site"
        );
        hutFlag = flags.FindIndex(flag => flag[0] == hut[0] && flag[1] == hut[1]) + 1;

        return game;
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheStonecutterWorksAsTheOriginalsPassesRunIt(string name)
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

            if (line.TryGetValue("s12", out var worker))
            {
                Assert.AreEqual(
                    WoodcutterTests.InEngineNumbers(worker, 24, game.Flags.Count),
                    WorkerWalkTests.Hex(game.Settlers[WorkerSlot], width),
                    $"{name}: the stonecutter after pass {pass}"
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

            if (line.TryGetValue("g", out var goods))
            {
                var places = game.Flags[hutFlag].Places;
                var bytes = string.Concat(places.Select(place => FlagEncoder.GoodByte(place).ToString("X2")));
                Assert.AreEqual(
                    goods.Split('/')[1],
                    bytes,
                    $"{name}: the goods on the building's flag after pass {pass}"
                );
                Assert.AreEqual(
                    Convert.ToInt32(goods.Split('/')[0], 16) >> 7,
                    game.Flags[hutFlag].NeedsGoodsTurn ? 1 : 0,
                    $"{name}: the flag's goods bit after pass {pass}"
                );
            }

            if (line.TryGetValue("rng", out var rng))
            {
                Assert.AreEqual(rng, Words(game.Draws.Generator), $"{name}: the random generator after pass {pass}");
            }
        }
    }
}
