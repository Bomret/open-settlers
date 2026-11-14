using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

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

    sealed record Scenario(string Name, string Command, List<(int Pass, string? Worker, string? Hut)> Lines);

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

            var fields = line.Split(' ')
                .Select(field => field.Split('='))
                .ToDictionary(pair => pair[0], pair => pair[1]);
            var pass = fields["pass"] == "start" ? -1 : int.Parse(fields["pass"], CultureInfo.InvariantCulture);
            scenarios[^1].Lines.Add((pass, fields.GetValueOrDefault("s10"), fields.GetValueOrDefault("b")));
        }

        return scenarios;
    }

    // The settler's 16 bytes as the original's item holds them (Table16): the job and player, the frame, the timer, the
    // node's offset (four bytes for each node, and the original's row holds the two records of its nodes, so a row is
    // twice the map's width), the stamp, the state, the mode, the target, the heading, and the next state.
    internal static string Hex(Settler s, int width)
    {
        var raw = SettlerEncoder.Fields(s);
        var bytes = new List<byte> { (byte)(s.Job.Code << 2 | s.Player), (byte)s.Frame };
        bytes.AddRange(BitConverter.GetBytes(s.Timer));
        bytes.AddRange(BitConverter.GetBytes((s.Y * 2 * width + s.X) * 4));
        bytes.AddRange(BitConverter.GetBytes(raw.Stamp));
        bytes.AddRange([(byte)raw.State, (byte)raw.Mode]);
        bytes.AddRange(BitConverter.GetBytes((ushort)raw.Target));
        bytes.AddRange([(byte)raw.Heading, (byte)raw.Next]);

        return Convert.ToHexString([.. bytes]);
    }

    // The hut's 18 bytes (Table18): the node's offset, the type byte, the status, the flag slot, bytes 8 and 9, the worker's
    // slot, the progress, and the pointer to a storage item, which a hut has none.
    internal static string Hex(Building b, int width)
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes((b.Y * 2 * width + b.X) * 4));
        bytes.AddRange([(byte)(BuildingEncoder.TypeIndex(b) << 2), (byte)BuildingEncoder.Status(b)]);
        bytes.AddRange(BitConverter.GetBytes((ushort)b.Flag));
        bytes.AddRange([0, 0]);
        bytes.AddRange(BitConverter.GetBytes((ushort)BuildingEncoder.Worker(b)));
        bytes.AddRange(BitConverter.GetBytes(b.Progress));
        bytes.AddRange([0, 0, 0, 0]);

        return Convert.ToHexString([.. bytes]);
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
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = GameFactory.Create(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
        var castle = Points(parts[4]).Single();
        var hut = parts[7].Split(',').Select(int.Parse).ToArray();
        var late = parts.Length > 11 ? parts[10] : "";
        var lateAt = parts.Length > 11 ? int.Parse(parts[11], CultureInfo.InvariantCulture) : -1;
        var passes = int.Parse(parts[8], CultureInfo.InvariantCulture);

        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, castle[0], castle[1]));
        foreach (var flag in Points(parts[5]))
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
        Assert.IsTrue(Huts.Place(game, hut[0] - 1, hut[1] - 1), "the fixture's hut has its site");
        if (late.Length > 0)
        {
            foreach (var road in late.Split(';').Select(text => text.Split(',')))
            {
                game.Enqueue(
                    new Command(
                        lateAt,
                        0,
                        CommandKind.BuildRoad,
                        int.Parse(road[0]),
                        int.Parse(road[1]),
                        [.. road[2].Select(c => c - '0')]
                    )
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
