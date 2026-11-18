using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of fish-walks.txt are the output of the emulator verb goods (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes over the item tables and the map's object pass on a game started as the original's game start leaves its tables, with a
// finished fisher's building (type 1) at a flag on a map with a lake, and the carriers that carry the fish to the castle (the whole
// run is checked). A scenario starts with a "#" line that names its verb command; the lines after it give, for a pass, the 16 bytes
// of the castle's fisher (slot 15, s15=) where his node, state, timers, or counters changed, the 18 bytes of the building (b=) where
// they changed, the object bytes that changed (o=x,y:old>new;...), the lake details that changed (ld=x,y:old>new;...), the flag's
// byte 4 and goods bytes (g=) where they changed, the castle's count of fish (fish=) where it changed, and the generator's words
// (rng=). The engine runs the same commands and must hold the same values at each listed pass, and no object byte or lake detail
// may change at any other pass.
//
// A scenario's last command field may write a lake detail into a rectangle of nodes before a pass (ld@PASS,X0,Y0,X1,Y1,VALUE), as the
// verb's SET list does; the ld= line of that pass lists the write.
//
// **Inferred input:** the verb writes the marks 0x42 on the building's node and 0x10 on its flag's node by hand, as the
// woodcutter's scenarios have them (WoodcutterTests).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class FisherTests
{
    const string Fixture = "fish-walks.txt";
    const int WorkerSlot = 15;
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

    // The bytes of a map layer that differ from a copy, as the verb lists them: "x,y:old>new" joined by ";".
    static string Changes(byte[] before, Map map, MapLayer layer)
    {
        var now = Layers.Of(map, layer);
        var width = map.Size.Width;

        return string.Join(
            ';',
            Enumerable
                .Range(0, now.Length)
                .Where(index => now[index] != before[index])
                .Select(index => $"{index % width},{index / width}:{before[index]:X2}>{now[index]:X2}")
        );
    }

    static Game Start(
        string command,
        out Map map,
        out int hutFlag,
        out int passes,
        out List<(int Pass, int Place, int Good)> puts,
        out List<int[]> lakes
    )
    {
        // goods:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES[:WARM[:LATEROADS:AT[:PASS,PLACE,GOOD;...[:ld@PASS,X0,Y0,X1,Y1,VALUE]]]]
        // The last field writes a lake detail into every node of a rectangle before a pass (the verb's SET of that form).
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
        // Each put lays a good on a place of the building's flag before the pass.
        puts =
        [
            .. parts.Length > 12
                ? parts[12]
                    .Split(';')
                    .Select(text => text.Split(',').Select(int.Parse).ToArray())
                    .Select(put => (put[0], put[1], put[2]))
                : [],
        ];

        lakes =
        [
            .. parts.Length > 13
                ? parts[13].Split(';').Select(text => text["ld@".Length..].Split(',').Select(int.Parse).ToArray())
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
        Assert.AreEqual(BuildingType.Fisher.Code, hut[2], "the fixture's building is a fisher's");
        Assert.IsTrue(
            Huts.Place(game, hut[0] - 1, hut[1] - 1, BuildingType.Fisher),
            "the fixture's building has its site"
        );
        hutFlag = flags.FindIndex(flag => flag[0] == hut[0] && flag[1] == hut[1]) + 1;

        return game;
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheFisherWorksAsTheOriginalsPassesRunIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        var game = Start(scenario.Command, out var map, out var hutFlag, out var passes, out var puts, out var lakes);
        var width = map.Size.Width;
        var last = scenario.Lines.Max(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));
        var fish = 0;

        for (var pass = 0; pass <= Math.Min(last, passes - 1); pass++)
        {
            var objectsBefore = Layers.Of(map, MapLayer.Object);
            var detailsBefore = Layers.Of(map, MapLayer.Resource);
            foreach (var put in puts.Where(put => put.Pass == pass))
            {
                game.Goods.LayGood(hutFlag, put.Place, put.Good);
            }

            foreach (var lake in lakes.Where(lake => lake[0] == pass))
            {
                for (var y = lake[2]; y <= lake[4]; y++)
                {
                    for (var x = lake[1]; x <= lake[3]; x++)
                    {
                        map.At(x, y).LakeDetail = lake[5];
                    }
                }
            }

            game.Tick();
            if (pass == 0)
            {
                // The commands of the first tick put the castle, the flags, and the building on the map; the verb lists changes from there.
                objectsBefore = Layers.Of(map, MapLayer.Object);
                detailsBefore = Layers.Of(map, MapLayer.Resource);
            }

            var line = scenario.Lines.SingleOrDefault(candidate =>
                candidate["pass"] == pass.ToString(CultureInfo.InvariantCulture)
            );
            Assert.AreEqual(
                line?.GetValueOrDefault("o") ?? "",
                Changes(objectsBefore, map, MapLayer.Object),
                $"{name}: the objects that changed at pass {pass}"
            );
            Assert.AreEqual(
                line?.GetValueOrDefault("ld") ?? "",
                Changes(detailsBefore, map, MapLayer.Resource),
                $"{name}: the lake details that changed at pass {pass}"
            );
            if (line is not null && line.TryGetValue("fish", out var count))
            {
                fish = int.Parse(count, CultureInfo.InvariantCulture);
            }

            Assert.AreEqual(fish, game.CastleStock.Count(Good.Fish), $"{name}: the castle's fish after pass {pass}");
            if (line is null)
            {
                continue;
            }

            if (line.TryGetValue("s15", out var worker))
            {
                Assert.AreEqual(
                    FlagWords.InEngineNumbers(worker, 24, game.Flags.Count),
                    WorkerBytes.Hex(game.Settlers[WorkerSlot], width),
                    $"{name}: the fisher after pass {pass}"
                );
            }

            if (line.TryGetValue("b", out var hutBytes))
            {
                Assert.AreEqual(
                    FlagWords.InEngineNumbers(hutBytes, 12, game.Flags.Count),
                    WorkerBytes.Hex(game.Buildings[HutSlot], width),
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
