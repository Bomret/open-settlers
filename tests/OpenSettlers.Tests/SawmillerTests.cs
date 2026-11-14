using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of saw-walks.txt are the output of the emulator verb goods (tools/GhidraDump/emulate.py, ADR 0008): the original's passes
// over the item tables on a game started as the original's game start leaves its tables, with a finished sawmill (type 17, a large
// building) at a flag, and the carriers that bring it logs and take its planks to the castle (the whole run is checked). A scenario
// starts with a "#" line that names its verb command; the lines after it give, for a pass, the 16 bytes of the castle's sawmiller
// (slot 11, s11=) where his node, state, or part of the work changed, the 18 bytes of the building (b=) where they changed, the flag's
// byte 4 and goods bytes (g=) where they changed, the request words of the flag (req=) where they changed, the castle's log and plank
// counts (log=, plank=) where they changed, and the generator's words (rng=). The engine runs the same commands and must hold the same
// values at each listed pass.
//
// **Inferred input:** the verb writes the marks 0x42 on the building's node and 0x10 on its flag's node by hand, as the woodcutter's
// scenarios have them (WoodcutterTests), and the flag pointer a built site leaves in the building's item (0x43ef82).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class SawmillerTests
{
    const string Fixture = "saw-walks.txt";
    const int WorkerSlot = 11;
    const int HutSlot = 1;

    sealed record Scenario(string Name, string Command, List<Dictionary<string, string>> Lines);

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    static List<Scenario> Load()
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

    static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    static string Words(RandomGenerator generator) =>
        string.Join(',', new[] { generator.W0, generator.W1, generator.W2 }.Select(word => word.ToString("X4")));

    // The building's 18 bytes with its two fill bytes, which the shared hex of the walk tests leaves out; the flag pointer is no byte
    // of the engine's.
    static string BuildingHex(Building building, int width)
    {
        var hex = WorkerWalkTests.Hex(building, width);

        return hex[..16] + building.FillLow.ToString("X2") + building.FillHigh.ToString("X2") + hex[20..];
    }

    // The request words of a flag as the bytes 0x42 to 0x45 of its item: a class byte (bit k for the class k) and the rank, twice.
    static string Requests(Flag flag) =>
        string.Concat(
            new[] { RequestWord.First, RequestWord.Second }.Select(word =>
            {
                var request = flag.RequestOf(word);

                return (request.Asks is { } asked ? 1 << asked.Class : 0).ToString("X2", CultureInfo.InvariantCulture)
                    + request.Rank.ToString("X2", CultureInfo.InvariantCulture);
            })
        );

    static Game Start(
        string command,
        out Map map,
        out int hutFlag,
        out int passes,
        out List<(int Pass, int Place, int Good, int Flag)> puts
    )
    {
        // goods:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES[:WARM[:LATEROADS:AT[:PASS,PLACE,GOOD,SLOT;...]]]
        var parts = command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = GameFactory.Create(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
        var castle = Points(parts[4]).Single();
        var flags = Points(parts[5]);
        var hut = parts[7].Split(',').Select(int.Parse).ToArray();
        passes = int.Parse(parts[8], CultureInfo.InvariantCulture);
        // Each put lays a good on a place of the flag of the slot (the original numbers its flags from 1) before the pass.
        puts =
        [
            .. parts.Length > 12
                ? parts[12]
                    .Split(';')
                    .Select(text => text.Split(',').Select(int.Parse).ToArray())
                    .Select(put => (put[0], put[1], put[2], put[3] - 1))
                : [],
        ];

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
        Assert.AreEqual(BuildingType.Sawmill.Code, hut[2], "the fixture's building is a sawmill");
        Assert.IsTrue(
            Huts.Place(game, hut[0] - 1, hut[1] - 1, BuildingType.Sawmill),
            "the fixture's building has its site"
        );
        hutFlag = flags.FindIndex(flag => flag[0] == hut[0] && flag[1] == hut[1]) + 1;

        return game;
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheSawmillerWorksAsTheOriginalsPassesRunIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        var game = Start(scenario.Command, out var map, out var hutFlag, out var passes, out var puts);
        var width = map.Size.Width;
        var last = scenario.Lines.Max(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));

        for (var pass = 0; pass <= Math.Min(last, passes - 1); pass++)
        {
            foreach (var put in puts.Where(put => put.Pass == pass))
            {
                game.Goods.LayGood(put.Flag, put.Place, put.Good);
            }

            game.Tick();
            var line = scenario.Lines.SingleOrDefault(candidate =>
                candidate["pass"] == pass.ToString(CultureInfo.InvariantCulture)
            );
            if (line is null)
            {
                continue;
            }

            if (line.TryGetValue("s11", out var worker))
            {
                Assert.AreEqual(
                    WoodcutterTests.InEngineNumbers(worker, 24, game.Flags.Count),
                    WorkerWalkTests.Hex(game.Settlers[WorkerSlot], width),
                    $"{name}: the sawmiller after pass {pass}"
                );
            }

            if (line.TryGetValue("b", out var hutBytes))
            {
                Assert.AreEqual(
                    WoodcutterTests.InEngineNumbers(hutBytes, 12, game.Flags.Count),
                    BuildingHex(game.Buildings[HutSlot], width),
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

            if (line.TryGetValue("req", out var requests))
            {
                Assert.AreEqual(
                    requests,
                    Requests(game.Flags[hutFlag]),
                    $"{name}: the request words of the building's flag after pass {pass}"
                );
            }

            if (line.TryGetValue("log", out var logs))
            {
                Assert.AreEqual(
                    int.Parse(logs, CultureInfo.InvariantCulture),
                    game.CastleStock.Logs,
                    $"{name}: the castle's logs after pass {pass}"
                );
            }

            if (line.TryGetValue("plank", out var planks))
            {
                Assert.AreEqual(
                    int.Parse(planks, CultureInfo.InvariantCulture),
                    game.CastleStock.Planks,
                    $"{name}: the castle's planks after pass {pass}"
                );
            }

            if (line.TryGetValue("rng", out var rng))
            {
                Assert.AreEqual(rng, Words(game.Draws.Generator), $"{name}: the random generator after pass {pass}");
            }
        }
    }
}
