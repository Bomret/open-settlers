using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of carrier-walks.txt are the output of the emulator verb carrier (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes over the item tables, with the flag table's pass 0x46414d between the phase step and the building
// pass (0x428728, the phase step, 0x46414d, 0x42cb2d, 0x42f3d9), on a game with a castle, flags, and roads and no building.
// Each scenario starts with a "#" line that names its verb command; the lines after it give, for the passes where the bytes
// changed, the 16 bytes of the castle's settlers 1 to 5 (the settlers without a job, from which the carriers are made;
// s1= to s5=) and the status byte and the six road bytes of a flag's item (bytes 5 to 11, f<slot>=), as hexadecimal. The
// engine runs the same commands and must hold the same bytes after each pass, which tests when a road asks for its
// carrier, which end of the road the carrier is sent to, and the carrier's walk to the node next to the road's far flag.
// A carrier waiting there (state 0x42) holds a pointer to its flag item in the dword at 12: the emulator's tables stand at
// 0x23000000, so the pointer is that plus 0x46 for each slot, and the engine keeps the slot.
[TestClass]
public sealed class CarrierWalkTests
{
    private const string Fixture = "carrier-walks.txt";
    private const string NoFlagBytes = "00000000000000";

    private sealed record Scenario(
        string Name,
        string Command,
        List<(int Pass, Dictionary<string, string> Values)> Lines
    );

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

            var fields = line.Split(' ')
                .Select(field => field.Split('='))
                .ToDictionary(pair => pair[0], pair => pair[1]);
            var pass = fields["pass"] == "start" ? -1 : int.Parse(fields["pass"], CultureInfo.InvariantCulture);
            fields.Remove("pass");
            scenarios[^1].Lines.Add((pass, fields));
        }

        return scenarios;
    }

    // The settler's 16 bytes as the original's item holds them (Table16), as in WorkerWalkTests.
    private static string Hex(Settler s, int width)
    {
        var bytes = new List<byte> { (byte)(s.Job << 2 | s.Player), (byte)s.Frame };
        bytes.AddRange(BitConverter.GetBytes(s.Timer));
        bytes.AddRange(BitConverter.GetBytes((s.Y * 2 * width + s.X) * 4));
        bytes.AddRange(BitConverter.GetBytes(s.Stamp));
        bytes.AddRange([(byte)s.State, (byte)s.Mode]);
        if (s.State == 0x42)
        {
            bytes.AddRange(BitConverter.GetBytes(0x23000000 + 0x46 * s.Target));
        }
        else
        {
            bytes.AddRange(BitConverter.GetBytes((ushort)s.Target));
            bytes.AddRange([(byte)s.Heading, (byte)s.Next]);
        }

        return Convert.ToHexString([.. bytes]);
    }

    private static string Hex(Game game, int flag) =>
        Convert.ToHexString([
            game.FlagStatus(flag),
            .. Enumerable.Range(0, 6).Select(direction => game.RoadByte(flag, direction)),
        ]);

    private static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    private static void Roads(Game game, string list, long tick)
    {
        foreach (var road in list.Split(';').Select(text => text.Split(',')))
        {
            game.Enqueue(
                new Command(
                    tick,
                    0,
                    CommandKind.BuildRoad,
                    int.Parse(road[0]),
                    int.Parse(road[1]),
                    [.. road[2].Select(c => c - '0')]
                )
            );
        }
    }

    // The longest run of passes in which a walking carrier's step had no time left, as the view draws it: the frame
    // stands still until the carrier's next turn.
    private static int LongestStall(bool everyPassTurns)
    {
        var scenario = Load()[0];
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = new Game(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture))
        {
            EveryPassTurns = everyPassTurns,
        };
        var castle = Points(parts[4]).Single();
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, castle[0], castle[1]));
        foreach (var flag in Points(parts[5]))
        {
            game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, flag[0], flag[1]));
        }

        Roads(game, parts[6], 0);
        var stalls = new Dictionary<int, int>();
        var longest = 0;
        for (var pass = 0; pass < int.Parse(parts[7], CultureInfo.InvariantCulture); pass++)
        {
            game.Tick();
            for (var slot = 0; slot < game.Settlers.Count; slot++)
            {
                var settler = game.Settlers[slot];
                var elapsed = (ushort)(game.Clock - settler.Stamp);
                var walking = settler.State is 2 or 3;
                stalls[slot] = walking && settler.Timer <= elapsed ? stalls.GetValueOrDefault(slot) + 1 : 0;
                longest = Math.Max(longest, stalls[slot]);
            }
        }

        return longest;
    }

    [TestMethod]
    public void AWalkingCarrierTakesItsNextStepThePassAfterItsStepEndsWhenEveryPassGivesATurn()
    {
        Assert.IsTrue(LongestStall(everyPassTurns: false) > 1, "the slice turns alone leave a step ended for passes");
        Assert.IsTrue(LongestStall(everyPassTurns: true) <= 1);
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheCarriersAreSentAndWalkAsTheOriginalsPassesMoveThem(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        // carrier:N:SEED:L:cx,cy:FLAGS:ROADS:PASSES[:LATEROADS:AT]
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = new Game(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
        var castle = Points(parts[4]).Single();
        var passes = int.Parse(parts[7], CultureInfo.InvariantCulture);

        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, castle[0], castle[1]));
        foreach (var flag in Points(parts[5]))
        {
            game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, flag[0], flag[1]));
        }

        Roads(game, parts[6], 0);
        if (parts.Length > 9)
        {
            Roads(game, parts[8], int.Parse(parts[9], CultureInfo.InvariantCulture));
        }

        var known = new Dictionary<string, string>();
        foreach (var line in scenario.Lines.Where(line => line.Pass == -1))
        {
            foreach (var (key, value) in line.Values)
            {
                known[key] = value;
            }
        }

        game.Tick();
        // The start line holds the flags the commands changed before the first pass; the first pass runs with them.
        for (var pass = 0; pass < passes; pass++)
        {
            if (pass > 0)
            {
                game.Tick();
            }

            foreach (var line in scenario.Lines.Where(line => line.Pass == pass))
            {
                foreach (var (key, value) in line.Values)
                {
                    known[key] = value;
                }
            }

            for (var slot = 1; slot <= 5; slot++)
            {
                if (known.TryGetValue($"s{slot}", out var expected))
                {
                    Assert.AreEqual(
                        expected,
                        Hex(game.Settlers[slot], map.Size.Width),
                        $"{name}: settler {slot} after pass {pass}"
                    );
                }
                else
                {
                    Assert.AreEqual(
                        21,
                        game.Settlers[slot].Job,
                        $"{name}: settler {slot} still waits in the castle after pass {pass}"
                    );
                    Assert.AreEqual(
                        1,
                        game.Settlers[slot].State,
                        $"{name}: settler {slot} still waits in the castle after pass {pass}"
                    );
                }
            }

            for (var flag = 0; flag < game.Flags.Count; flag++)
            {
                Assert.AreEqual(
                    known.GetValueOrDefault($"f{flag}", NoFlagBytes),
                    Hex(game, flag),
                    $"{name}: flag {flag} after pass {pass}"
                );
            }
        }
    }
}
