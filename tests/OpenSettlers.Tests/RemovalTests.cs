using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of removals.txt are the output of the emulator verb remove (tools/GhidraDump/emulate.py, ADR 0008): the original's
// click routines for the remove click (0x4615cf: a flag by 0x462153, a building by 0x4617fd) and for the road click (0x462d88,
// the take-down 0x462e1d) run on a node of a game with a castle, flags, roads, and a woodcutter's hut, in the passes of the
// original's main loop. A scenario starts with a "#" line that names its verb command. The lines after it give, for the click
// and then for each pass where something changed, the class the check 0x44b6e6 gave the node and the sound the routine asked
// for, the map bytes (flag, object, and link) that changed, the settlers (s<slot>), the flag items (f<slot>), the building items
// (b<slot>), the counts of the tables (c=), and the generator (rng=). The verb takes the dummies of the flag table and of the
// building table that a real game's start takes, so its slot numbers are the engine's plus one. The engine runs the same
// commands in the same passes and must hold the same bytes after the click and after each pass.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class RemovalTests
{
    private const string Fixture = "removals.txt";

    internal sealed record Scenario(string Name, string Command, List<Dictionary<string, string>> Lines);

    public static IEnumerable<object[]> Scenarios =>
        Load()
            .Where(scenario => !scenario.Name.StartsWith("sweep", StringComparison.Ordinal))
            .Select(scenario => new object[] { scenario.Name });

    public static IEnumerable<object[]> Sweeps =>
        Load()
            .Where(scenario => scenario.Name.StartsWith("sweep", StringComparison.Ordinal))
            .Select(scenario => new object[] { scenario.Name });

    internal static List<Scenario> Load(string fixture = Fixture)
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(fixture))
        {
            // A scenario's "# name command" line has a verb command (no space before its first colon); the other "#" lines and
            // the blank lines of a fixture are notes.
            if (line.StartsWith("# ", StringComparison.Ordinal) && line[2..].Split(' ', 2) is [var head, var command])
            {
                if (!command.Split(':')[0].Contains(' ') && command.Contains(':'))
                {
                    scenarios.Add(new Scenario(head, command, []));
                }

                continue;
            }

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            scenarios[^1]
                .Lines.Add(
                    line.Split(' ')
                        .Select(field => field.Split('=', 2))
                        .ToDictionary(pair => pair[0], pair => pair.Length > 1 ? pair[1] : "")
                );
        }

        return scenarios;
    }

    private static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    // remove:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:PUTS]: the game before the first pass.
    internal static Game Started(string[] parts, Map map, bool handsOutGoods = false)
    {
        var game = GameFactory.Create(
            TestProgram.Tables(),
            map,
            int.Parse(parts[3], CultureInfo.InvariantCulture),
            handsOutGoods: handsOutGoods
        );
        var castle = Points(parts[4]).Single();
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, castle[0], castle[1]));
        // The flags take their slots in the order the verb lists them, which the order of the commands of one tick (by node) would
        // change: each is applied at once.
        game.ApplyCommands();
        foreach (var flag in Points(parts[5]))
        {
            game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, flag[0], flag[1]));
            game.ApplyCommands();
        }

        foreach (var road in parts[6].Split(';').Where(text => text.Length > 0).Select(text => text.Split(',')))
        {
            game.Enqueue(
                new Command(
                    0,
                    0,
                    CommandKind.BuildRoad,
                    int.Parse(road[0], CultureInfo.InvariantCulture),
                    int.Parse(road[1], CultureInfo.InvariantCulture),
                    [.. road[2].Select(c => c - '0')]
                )
            );
        }

        // The finished huts stand as a layout's finished-building entry leaves them, after the roads.
        game.ApplyCommands();
        foreach (var hut in Points(parts[7]))
        {
            Assert.IsTrue(
                Huts.Place(game, hut[0] - 1, hut[1] - 1, new BuildingType(hut[2])),
                "the fixture's hut has its site"
            );
        }

        return game;
    }

    internal static Map MapOf(string[] parts)
    {
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();

        return MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
    }

    // The class the check 0x44b6e6 gives each node, for the classes 1 to 4 that the remove click and the road click act on
    // (a flag the click refuses, a flag it takes down, a building, a node of a road); the engine says 0 for the other nodes.
    [TestMethod]
    [DynamicData(nameof(Sweeps))]
    public void TheClickClassOfEachNodeIsTheOriginals(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        var parts = scenario.Command.Split(':');
        var map = MapOf(parts);
        var game = Started(parts, map);
        for (var pass = 0; pass < int.Parse(parts[8], CultureInfo.InvariantCulture); pass++)
        {
            game.Tick();
        }

        var expected = scenario
            .Lines[0]["classes"]
            .Split(';')
            .Select(entry => entry.Split(':'))
            .ToDictionary(entry => entry[0], entry => int.Parse(entry[1].Split('/')[0], CultureInfo.InvariantCulture));
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                Assert.AreEqual(
                    expected.GetValueOrDefault($"{x},{y}"),
                    game.Removal.ClickClass(x, y),
                    $"{name}: the class of the node {x},{y}"
                );
            }
        }
    }

    // The settler's 16 bytes as the original's item holds them (Table16, WorkerWalkTests.Hex), where the engine keeps the same
    // bytes: the word at 12 holds the flag a settler walks to in the original's numbering (one more than the engine's, the
    // dummy of the flag table) and a pointer to a flag's item in the carriers' waiting states, which the engine keeps in its
    // own numbering and nothing reads; those words are left out, except where the original's walk home sets the target to 0.
    //
    // The walk home (state 2, mode 0xfe) holds the storage's flag there once it picked it: the castle's flag, slot 1 in the
    // original and 0 in the engine, where 0 is also the target before the pick. A settler that was a carrier waiting next to a
    // flag and went home (state 0x19 with the bytes 14 and 15 of the pointer, 0 and 0x23) keeps the pointer there until its
    // search for a flag writes the vector.
    private static string Masked(string expected, string actual)
    {
        var state = Convert.ToByte(expected[20..22], 16);
        var mode = Convert.ToByte(expected[22..24], 16);
        var residue = state == 0x19 && expected[28..32] == "0023";
        var flagWord = state switch
        {
            0x42 or 0x43 or 0x44 or 0x45 => true,
            2 or 5 or 7 or 0xf => mode != 0xfe,
            3 => mode == 0,
            _ => false,
        };
        if (state == 2 && mode == 0xfe && expected[24..28] == "0100" && actual[24..28] == "0000")
        {
            return string.Concat(actual[..24], "0100", actual[28..]);
        }

        return flagWord || residue ? string.Concat(actual[..24], expected[24..32]) : actual;
    }

    // The flag item's fields the engine holds, in the order of the verb's 70 bytes: byte 4 bit 7, the status, the road bytes, the
    // goods and their destination words, and the bytes of the directions (0x3c to 0x41).
    private static string FlagFields(Game game, int slot) =>
        Convert.ToHexString([
            FlagEncoder.GoodsByte(game.Flags[slot]),
            FlagEncoder.Status(game.Flags[slot]),
            .. Enumerable.Range(0, 6).Select(direction => FlagEncoder.RoadByte(game.Flags[slot].Side(direction))),
            .. game.Flags[slot].Places.Select(FlagEncoder.GoodByte),
            .. game.Flags[slot].Places.SelectMany(place => BitConverter.GetBytes(FlagEncoder.DestinationWord(place))),
            .. Enumerable.Range(0, 6).Select(direction => FlagEncoder.DirectionByte(game.Flags[slot].Side(direction))),
        ]);

    private static string Fields(string item) =>
        string.Concat(
            (Convert.ToByte(item[8..10], 16) & 0x80).ToString("X2", CultureInfo.InvariantCulture),
            item[10..24],
            item[24..72],
            item[120..132]
        );

    // The building's bytes as the item holds them: the node's offset, the type byte, the status, the flag's slot (one more than
    // the engine's), the two bytes of the fill, the worker, the progress, and the word at 14 that a building taken down keeps its
    // clock in (a building site holds its target height there, and its needs at 16 and 17). The castle's bytes from 14 hold a pointer to its
    // storage, which the engine does not keep.
    internal static string BuildingBytes(Building building, int width)
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes((building.Y * 2 * width + building.X) * 4));
        bytes.AddRange([(byte)(BuildingEncoder.TypeIndex(building) << 2), (byte)BuildingEncoder.Status(building)]);
        bytes.AddRange(BitConverter.GetBytes((ushort)(building.Flag + 1)));
        bytes.AddRange(BitConverter.GetBytes((ushort)BuildingEncoder.Fill(building)));
        bytes.AddRange(BitConverter.GetBytes((ushort)BuildingEncoder.Worker(building)));
        bytes.AddRange(BitConverter.GetBytes(building.Progress));
        if (building.Site is { } site)
        {
            // A building site holds its target height at 14 and the planks and stones it needs at 16 and 17 (0x449a63); taken down it
            // holds the clock there (0x4617fd).
            bytes.AddRange(BitConverter.GetBytes(building.IsDemolishing ? building.Stamp : (ushort)site.TargetHeight));
            bytes.AddRange([(byte)site.Planks, (byte)site.Stones]);
        }
        else
        {
            bytes.AddRange(BitConverter.GetBytes(building.Stamp));
            bytes.AddRange([0, 0]);
        }

        return Convert.ToHexString([.. bytes]);
    }

    // The map bytes of the flag, object, and link layers that a step changed, as the verb lists them: "x,y:0=old>new/3=old>new/4=old>new".
    private static string MapChanges(byte[][] before, Map map)
    {
        var width = map.Size.Width;
        var exported = MapLayerExporter.Export(map);
        var layers = new[] { (0, MapLayer.Flags), (3, MapLayer.Object), (4, MapLayer.Resource) };
        var changed = new List<string>();
        for (var index = 0; index < width * map.Size.Height; index++)
        {
            // The link byte of a flag or a building holds the slot, one less than the verb's (the dummies of the tables).
            static int Link(int value, int code) => value != 0 && code is 1 or 2 or 3 ? value + 1 : value;
            var parts = layers
                .Where(layer => exported.Layer(layer.Item2)[index] != before[layer.Item1][index])
                .Select(layer =>
                    layer.Item1 == 4
                        ? string.Create(
                            CultureInfo.InvariantCulture,
                            $"4={Link(before[4][index], before[3][index] & 0x7f):X2}>{Link(exported.Layer(MapLayer.Resource)[index], exported.Layer(MapLayer.Object)[index] & 0x7f):X2}"
                        )
                        : string.Create(
                            CultureInfo.InvariantCulture,
                            $"{layer.Item1}={before[layer.Item1][index]:X2}>{exported.Layer(layer.Item2)[index]:X2}"
                        )
                );
            var text = string.Join('/', parts);
            if (text.Length > 0)
            {
                changed.Add($"{index % width},{index / width}:{text}");
            }
        }

        return string.Join(';', changed);
    }

    // The verb also lists the changes of byte 5 of a node, the mark a settler leaves on the node it stands on (0x80); the engine
    // keeps that mark in its settler table (Game.Occupant) and no map layer holds it, so the settlers' bytes check it.
    private static string WithoutOccupancy(string changes) =>
        string.Join(
            ';',
            changes
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Split(':', 2))
                .Select(entry =>
                    (
                        Node: entry[0],
                        Layers: entry[1].Split('/').Where(layer => !layer.StartsWith("5=", StringComparison.Ordinal))
                    )
                )
                .Where(entry => entry.Layers.Any())
                .Select(entry => $"{entry.Node}:{string.Join('/', entry.Layers)}")
        );

    private static byte[][] Snapshot(Map map) =>
        [Layers.Of(map, MapLayer.Flags), [], [], Layers.Of(map, MapLayer.Object), Layers.Of(map, MapLayer.Resource)];

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void ARemovalChangesTheGameAsTheOriginalsRoutinesDo(string name) =>
        Replay(
            Fixture,
            name,
            (click, expectedClass) =>
                click == 'r' ? [CommandKind.RemoveRoad]
                : expectedClass == 2 ? [CommandKind.RemoveFlag]
                : expectedClass == 3 ? [CommandKind.RemoveBuilding]
                : [CommandKind.RemoveFlag, CommandKind.RemoveBuilding],
            line => line["sound"] == "4"
        );

    // Replays a scenario of a fixture of the verb remove: the commands each click asks for (from the click's letter and the class
    // the check gave the node) are enqueued at the pass of the click, and the engine must hold the lines' bytes after the click and
    // after each pass. A click the line marks as refused must change nothing; the clicks "c@x,y" only read the class and ask for
    // nothing.
    internal static void Replay(
        string fixture,
        string name,
        Func<char, int, int[]> kindsOf,
        Func<Dictionary<string, string>, bool> isRefused,
        bool clickOnly = false,
        Func<Settler, int, string>? hexOf = null,
        bool construct = false
    )
    {
        var scenario = Load(fixture).Single(candidate => candidate.Name == name);
        // remove:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:PUTS]
        var parts = scenario.Command.Split(':');
        var map = MapOf(parts);
        var width = map.Size.Width;
        // A construct scenario runs the castle's hand-out, which lays a good on the castle's flag at once where the original carries it
        // out and lays it down about 130 passes later (ConstructionBuilderTests): the castle keeps no planks or stones, so no good the
        // original has on the way exists in the engine, and the lines' "fill<slot>+=NN" add the counts the original's goods give the
        // site at the passes it counted or received them.
        var game = Started(parts, map, handsOutGoods: construct);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        // A click written N=click is made before the pass N, any other before the pass AT.
        var clicks = parts[10]
            .Split(';')
            .Select(text => text.Split('=', 2))
            .Select(pair =>
                (When: pair.Length > 1 ? int.Parse(pair[0], CultureInfo.InvariantCulture) : at, Text: pair[^1])
            )
            .Where(click => click.Text[0] != 'c')
            .Select(click =>
                (click.When, Kind: click.Text[0], Node: click.Text[2..].Split(',').Select(int.Parse).ToArray())
            )
            .ToList();
        var puts =
            parts.Length > 11 && parts[11].Length > 0
                ? parts[11].Split(';').Select(text => text.Split(',').Select(int.Parse).ToArray()).ToList()
                : [];
        var clickLines = scenario.Lines.Where(line => line.ContainsKey("click")).ToList();
        var passLines = scenario
            .Lines.Where(line => line.ContainsKey("pass"))
            .ToDictionary(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));

        // The lines list changes, so a run's output ends with its last change; a fixture whose output stops well before its pass
        // count (split-nofree-1000, at pass 1195 of 1400) has no expected value for the passes after it.
        passes = Math.Min(passes, passLines.Keys.Max() + 1);

        var settlers = new Dictionary<int, string>();
        var flags = new Dictionary<int, string>();
        var buildings = new Dictionary<int, string>();
        string? rng = null;
        string? counts = null;

        void Take(Dictionary<string, string> line)
        {
            foreach (var (key, value) in line)
            {
                switch (key[0], key.Length > 1 && char.IsDigit(key[1]))
                {
                    case ('s', true):
                        // A settler with no bytes in the line ("s14=") is not compared from that pass on.
                        if (value.Length == 0)
                        {
                            settlers.Remove(int.Parse(key[1..], CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            settlers[int.Parse(key[1..], CultureInfo.InvariantCulture)] = value;
                        }

                        break;
                    case ('f', true):
                        flags[int.Parse(key[1..], CultureInfo.InvariantCulture)] = value;
                        break;
                    case ('b', true):
                        buildings[int.Parse(key[1..], CultureInfo.InvariantCulture)] = value;
                        break;
                }
            }

            rng = line.GetValueOrDefault("rng") ?? rng;
            counts = line.GetValueOrDefault("c") ?? counts;
        }

        void Check(string when, Dictionary<string, string> line, byte[][] before)
        {
            Assert.AreEqual(
                WithoutOccupancy(line.GetValueOrDefault("m") ?? ""),
                MapChanges(before, map),
                $"{name}: the map bytes that changed {when}"
            );
            foreach (var (slot, expected) in settlers)
            {
                if (slot < game.Settlers.Count)
                {
                    Assert.AreEqual(
                        Masked(expected, expected),
                        Masked(expected, (hexOf ?? WorkerWalkTests.Hex)(game.Settlers[slot], width)),
                        $"{name}: settler {slot} {when}"
                    );
                }
            }

            foreach (var (slot, expected) in flags)
            {
                if (slot - 1 < game.Flags.Count)
                {
                    Assert.AreEqual(Fields(expected), FlagFields(game, slot - 1), $"{name}: flag {slot} {when}");
                }
            }

            foreach (var (slot, expected) in buildings)
            {
                // Slot 0 is the dummy of the building table, which has no building of the engine's.
                if (slot > 0 && slot - 1 < game.Buildings.Count && !game.Buildings[slot - 1].IsFree)
                {
                    var length = game.Buildings[slot - 1].Type == BuildingType.Castle ? 28 : 36;
                    Assert.AreEqual(
                        expected[..length],
                        BuildingBytes(game.Buildings[slot - 1], width)[..length],
                        $"{name}: building {slot} {when}"
                    );
                }
            }

            if (rng is not null)
            {
                var words = game.Draws.Generator;
                Assert.AreEqual(rng, $"{words.W0:X4},{words.W1:X4},{words.W2:X4}", $"{name}: the generator {when}");
            }

            if (counts is not null)
            {
                var items = counts.Split(',');
                Assert.AreEqual(
                    $"{game.Flags.Count + 1:X4},{game.Buildings.Count + 1:X4},{game.Settlers.Count:X4},{game.Map.Resource1Total:x}",
                    $"{items[0]},{items[1]},{items[2]},{items[3]}",
                    $"{name}: the counts of the tables {when}"
                );
            }
        }

        var clickIndex = 0;
        for (var pass = 0; pass < passes; pass++)
        {
            // A click at pass 0 comes after the setup commands, which are due at the same tick.
            if (pass == at)
            {
                game.ApplyCommands();
            }

            var before = Snapshot(map);
            if (clicks.Any(click => click.When == pass))
            {
                foreach (var click in clicks.Where(click => click.When == pass))
                {
                    var line = clickLines[clickIndex++];
                    var node = click.Node;
                    var expectedClass = int.Parse(line["class"], CultureInfo.InvariantCulture);
                    Assert.AreEqual(
                        expectedClass is >= 1 and <= 4 ? expectedClass : 0,
                        game.Removal.ClickClass(node[0], node[1]),
                        $"{name}: the class of the click on {node[0]},{node[1]}"
                    );
                    var digest = game.Digest();
                    foreach (var kind in kindsOf(click.Kind, expectedClass))
                    {
                        // A building click names the type of the building as its third number.
                        BuildingType? type =
                            kind == CommandKind.PlaceBuilding && node.Length > 2 ? new BuildingType(node[2]) : null;
                        game.Enqueue(new Command(game.TickCount, 0, kind, node[0], node[1], Building: type));
                    }

                    game.ApplyCommands();
                    if (isRefused(line))
                    {
                        Assert.AreEqual(digest, game.Digest(), $"{name}: a refused click changes nothing");
                    }
                    else
                    {
                        Assert.AreNotEqual(digest, game.Digest(), $"{name}: the click changes the game");
                    }

                    Take(line);
                    Check($"after the click on {node[0]},{node[1]}", line, before);
                    before = Snapshot(map);
                }

                // A click-only replay checks the click and stops: the passes after a building site's placement are the
                // construction's, which the engine does not run yet.
                if (clickOnly)
                {
                    return;
                }
            }

            foreach (var put in puts.Where(candidate => candidate[0] == pass))
            {
                // The fixture holds the original's good byte (the way plus one in bits 5 to 7) and destination word (the slot plus one).
                game.Goods.LayGood(
                    put[1] - 1,
                    put[2],
                    put[3] & 0x1f,
                    put[4] == 0 ? null : put[4] - 1,
                    put[3] >> 5 == 0 ? null : (put[3] >> 5) - 1
                );
            }

            if (construct)
            {
                while (game.CastleStock.Take(Good.Plank) || game.CastleStock.Take(Good.Stone)) { }
            }

            game.Tick();
            if (pass >= at)
            {
                var line = passLines.GetValueOrDefault(pass) ?? [];
                if (construct)
                {
                    ConstructionBuilderTests.Apply(game, line);
                }

                Take(line);
                Check($"after pass {pass}", line, before);
            }
        }
    }
}
