using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of flag-goods.txt are the output of the emulator verb goods (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes over the item tables (0x428728, 0x428d10, 0x46414d, 0x42cb2d, 0x42f3d9) on a game that starts as the original's game
// start leaves its tables (flag slot 0 is a dummy, so the castle's flag is slot 1 and the hut's flag slot 2), with a finished
// woodcutter's hut at a flag. Each scenario starts with a "#" line that names its verb command. The lines after it give, for a
// pass, the flags' goods fields where they changed (f<slot>= : byte 4 and the status byte at 5, the 8 goods at 0xc, the 8
// destination words at 0x14, and the 6 direction bytes at 0x3c) and the 16 bytes of the castle's carrier (s5=, from the pass
// where it first waits next to its flag). The engine runs the same commands and must hold the same fields after each pass,
// which tests when a good gets its destination and its direction, and when the waiting carrier is told to fetch it. A
// pass-5 put in the command (the last field) is the put of 0x436b46 by hand, before that pass.
//
// The scenarios chain-log and several-goods run a road of three flags between the hut and the castle's flag (the flags 5, 7, 6,
// and 1 of the emulator's numbering). Their f lines keep to the flags that change: each is checked at every pass. Their s lines
// (a carrier's 16 bytes, at the pass it takes a new state or a new good, and at every twelfth change), the l lines (the logs in
// the castle) and the c lines (bytes 8 and 9 of the castle's item) are checked at the pass they are listed at. They give the
// carrier's lifting of a good, its drop at a flag where no good waits for its road, its delivery into the castle in state 0xe,
// and the castle's count of logs rising by one for each delivery. several-goods lays 2 logs on the hut's flag (the one on the
// higher place goes first: the flag's turn visits the places from the last) and one on the flag 6 at pass 5, so that two
// carriers work at once. The verb runs the original's default transport priorities at the game's start (0x445570), and the engine
// reads the same ranks from the install's program: when the carrier takes the first log, the exchange lines up the second, so
// both logs of the hut's flag reach the castle. ranked-goods lays three goods of different ranks on the hut's flag, the highest on
// the last place: after the carrier takes it, the good of the higher rank of the other two waits next, not the one on the lower
// place (docs/rules/goods.md, "Transport priorities"). push-out lays the same three goods with the lowest-ranked first and the
// highest in the middle: the flag's turn gives the road to the last place's good, and the middle good, of a higher rank, takes
// its place (0x46501a); the good pushed out keeps its direction and leaves after it. The exchange of a good for
// a good at a flag needs a good that goes the way the carrier came, which no good does while the castle is the only
// destination: ExchangeTests covers it with made-up tables.
//
// The emulator numbers the flags one higher than the engine (the dummy), and so do the destination words. Byte 4 holds more
// than bit 7 (bit 6 marks the hut): the engine keeps bit 7 only. The carrier's bytes at 12 to 15 hold the pointer to its flag's
// item that the original keeps while it waits (docs/rules/carriers.md); the engine keeps the slot.
[TestClass]
public sealed class FlagGoodsTests
{
    private const string Fixture = "flag-goods.txt";

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

            var fields = line.Split(' ').Select(field => field.Split('=')).ToDictionary(p => p[0], p => p[1]);
            var pass = int.Parse(fields["pass"], CultureInfo.InvariantCulture);
            fields.Remove("pass");
            scenarios[^1].Lines.Add((pass, fields));
        }

        return scenarios;
    }

    private static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    // A flag's goods fields in the order of the fixture's f fields.
    private static string Fields(Game game, int flag) =>
        Convert.ToHexString([
            .. new[] { (byte)(game.FlagGoodsBit(flag) ? 0x80 : 0), FlagEncoder.Status(game.Flags[flag]) },
            .. game.FlagGoods(flag).Select(good => (byte)good),
            .. game.FlagDestinations(flag).SelectMany(word => BitConverter.GetBytes((ushort)word)),
            .. Enumerable.Range(0, 6).Select(direction => FlagEncoder.DirectionByte(game.Flags[flag].Side(direction))),
        ]);

    private static string Expected(string fields) =>
        string.Concat((Convert.ToByte(fields[..2], 16) & 0x80).ToString("X2"), fields[2..]);

    // The carrier's 16 bytes as the original's item holds them while it waits and the turn it fetches a good.
    private static string Carrier(Settler s, int width)
    {
        var bytes = new List<byte> { (byte)(s.Job << 2 | s.Player), (byte)s.Frame };
        bytes.AddRange(BitConverter.GetBytes(s.Timer));
        bytes.AddRange(BitConverter.GetBytes((s.Y * 2 * width + s.X) * 4));
        bytes.AddRange(BitConverter.GetBytes(s.Stamp));
        bytes.AddRange([(byte)s.State, (byte)s.Mode]);
        var pointer = 0x23000000 + 0x46 * (s.Target + 1);
        bytes.AddRange(BitConverter.GetBytes((ushort)(s.State is 0x42 or 0x43 ? pointer : s.Target)));
        bytes.Add((byte)s.Heading);
        bytes.Add(s.State is 0x42 or 0x43 ? (byte)(pointer >> 24) : (byte)s.Next);

        return Convert.ToHexString([.. bytes]);
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void AGoodGetsItsDestinationAndDirectionAndTheWaitingCarrierFetchesItAsTheOriginalsPassesDoIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        // goods:N:SEED:L:cx,cy:FLAGS:ROADS:fx,fy,T:PASSES[:WARM[:LATEROADS:AT[:PASS,PLACE,GOOD]]]
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = new Game(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
        var castle = Points(parts[4]).Single();
        var flags = Points(parts[5]);
        var hut = parts[7].Split(',').Select(int.Parse).ToArray();
        var passes = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var hutFlag = flags.FindIndex(flag => flag[0] == hut[0] && flag[1] == hut[1]) + 1;
        // Each put is PASS,PLACE,GOOD for the hut's flag, or PASS,PLACE,GOOD,SLOT for a flag of the emulator's numbering.
        var puts =
            parts.Length > 12
                ? parts[12]
                    .Split(';')
                    .Select(text => text.Split(',').Select(int.Parse).ToArray())
                    .Select(put =>
                        (Pass: put[0], Place: put[1], Good: put[2], Flag: put.Length > 3 ? put[3] - 1 : hutFlag)
                    )
                    .ToList()
                : [];

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
        var known = new Dictionary<string, string>();
        for (var pass = 0; pass < passes; pass++)
        {
            foreach (var put in puts.Where(put => put.Pass == pass))
            {
                game.LayGood(put.Flag, put.Place, put.Good);
            }

            game.Tick();
            var checkpoint = scenario.Lines.Where(line => line.Pass == pass).SelectMany(line => line.Values).ToList();
            foreach (var (key, value) in checkpoint)
            {
                if (key[0] == 'f')
                {
                    known[key] = value;
                }
            }

            foreach (var (key, value) in known)
            {
                var flag = int.Parse(key[1..], CultureInfo.InvariantCulture) - 1;
                Assert.AreEqual(Expected(value), Fields(game, flag), $"{name}: flag {flag} after pass {pass}");
            }

            // The carriers (s), the castle's log count (l), and the castle's bytes 8 and 9 (c) are checked at the passes a line
            // lists them.
            foreach (var (key, value) in checkpoint)
            {
                switch (key[0])
                {
                    case 's':
                        var settler = game.Settlers[int.Parse(key[1..], CultureInfo.InvariantCulture)];
                        Assert.AreEqual(
                            Masked(value, value),
                            Masked(value, Carrier(settler, map.Size.Width)),
                            $"{name}: the carrier {key} after pass {pass}"
                        );
                        break;
                    case 'l':
                        Assert.AreEqual(
                            int.Parse(value, CultureInfo.InvariantCulture),
                            game.CastleLogs,
                            $"{name}: the logs in the castle after pass {pass}"
                        );
                        break;
                    case 'c':
                        Assert.AreEqual(
                            value,
                            Convert.ToHexString(BitConverter.GetBytes(game.Buildings[0].Fill)),
                            $"{name}: bytes 8 and 9 of the castle after pass {pass}"
                        );
                        break;
                }
            }
        }
    }

    // The word at 12 of a carrier holds the pointer to the flag's item in states 0x42 and 0x43, the destination in state 0xe and
    // in state 3 while the carrier carries a good (its byte 11 is not 0), and else the flag it walks to or what it held last,
    // which the engine keeps in its own numbering and nothing reads: only the first three are compared. The fixture's bytes
    // say which: the state at 10 and the good at 11.
    private static string Masked(string expected, string bytes)
    {
        var state = Convert.ToByte(expected[20..22], 16);
        var carrying = state == 3 && Convert.ToByte(expected[22..24], 16) != 0;

        return state is 0x42 or 0x43 or 0xe || carrying ? bytes : string.Concat(bytes[..24], "0000", bytes[28..]);
    }

    // Tests of the game's own goods logic, on made-up tables (no install): a good waits where no road holds a carrier, and
    // the digest holds the goods' new fields.
    [TestMethod]
    public void AGoodOnAFlagWithNoCarrierWaitsAndTheFlagAsksAgainAtEachTurn()
    {
        var game = new Game(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 18, 6));
        game.Tick();
        game.LayGood(1, 0, 7);
        var before = game.Digest();

        for (var tick = 0; tick < 70; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(7, game.FlagGoods(1)[0], "the good has no direction");
        Assert.IsTrue(game.FlagGoodsBit(1), "the flag asks again at its next turn");
        Assert.AreEqual(0, game.FlagDestinations(1)[0]);
        Assert.AreNotEqual(before, game.Digest(), "the digest holds the new fields");
    }
}
