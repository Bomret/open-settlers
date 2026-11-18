using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;
using static OpenSettlers.Adapters.Testing.RemovalGame;
using static OpenSettlers.Testing.EmulatorFixture;

namespace OpenSettlers.Tests;

/// <summary>
/// The replay of a fixture of the emulator verb remove (tools/GhidraDump/emulate.py, ADR 0008): the original's click routines run
/// on a node of a game with a castle, flags, roads, and huts, in the passes of the original's main loop. A scenario starts with a
/// "#" line that names its verb command. The lines after it give, for the click and then for each pass where something changed,
/// the class the check 0x44b6e6 gave the node and the sound the routine asked for, the map bytes (flag, object, and link) that
/// changed, the settlers (s&lt;slot&gt;), the flag items (f&lt;slot&gt;), the building items (b&lt;slot&gt;), the counts of the
/// tables (c=), and the generator (rng=). The verb takes the dummies of the flag table and of the building table that a real
/// game's start takes, so its slot numbers are the engine's plus one. The engine runs the same commands in the same passes and
/// must hold the same bytes after the click and after each pass. It stays with the tests because it reads the click class
/// (<c>Game.Removal.ClickClass</c>) and lays goods (<c>Game.Goods.LayGood</c>) through members that are internal to the application.
/// </summary>
static class RemovalReplay
{
    // The settler's 16 bytes as the original's item holds them (Table16, WorkerBytes.Hex), where the engine keeps the same
    // bytes: the word at 12 holds the flag a settler walks to in the original's numbering (one more than the engine's, the
    // dummy of the flag table) and a pointer to a flag's item in the carriers' waiting states, which the engine keeps in its
    // own numbering and nothing reads; those words are left out, except where the original's walk home sets the target to 0.
    //
    // The walk home (state 2, mode 0xfe) holds the storage's flag there once it picked it: the castle's flag, slot 1 in the
    // original and 0 in the engine, where 0 is also the target before the pick. A settler that was a carrier waiting next to a
    // flag and went home (state 0x19 with the bytes 14 and 15 of the pointer, 0 and 0x23) keeps the pointer there until its
    // search for a flag writes the vector.
    static string Masked(string expected, string actual)
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
    static string FlagFields(Game game, int slot) =>
        Convert.ToHexString([
            FlagEncoder.GoodsByte(game.Flags[slot]),
            FlagEncoder.Status(game.Flags[slot]),
            .. Enumerable.Range(0, 6).Select(direction => FlagEncoder.RoadByte(game.Flags[slot].Side(direction))),
            .. game.Flags[slot].Places.Select(place => (byte)FlagEncoder.GoodWord(place, TestCatalog.Default)),
            .. game.Flags[slot].Places.SelectMany(place => BitConverter.GetBytes(FlagEncoder.DestinationWord(place))),
            .. Enumerable.Range(0, 6).Select(direction => FlagEncoder.DirectionByte(game.Flags[slot].Side(direction))),
        ]);

    static string Fields(string item) =>
        string.Concat(
            (Convert.ToByte(item[8..10], 16) & 0x80).ToString("X2", CultureInfo.InvariantCulture),
            item[10..24],
            item[24..72],
            item[120..132]
        );

    // The map bytes of the flag, object, and link layers that a step changed, as the verb lists them: "x,y:0=old>new/3=old>new/4=old>new".
    static string MapChanges(byte[][] before, Map map)
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
    static string WithoutOccupancy(string changes) =>
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

    static byte[][] Snapshot(Map map) =>
        [Layers.Of(map, MapLayer.Flags), [], [], Layers.Of(map, MapLayer.Object), Layers.Of(map, MapLayer.Resource)];

    // Replays a scenario of a fixture of the verb remove: the commands each click asks for (from the click's letter and the class
    // the check gave the node) are enqueued at the pass of the click, and the engine must hold the lines' bytes after the click and
    // after each pass. A click the line marks as refused must change nothing; the clicks "c@x,y" only read the class and ask for
    // nothing.
    public static void Replay(
        string fixture,
        string name,
        Func<char, int, int[]> kindsOf,
        Func<Dictionary<string, string>, bool> isRefused,
        Func<Settler, int, string>? hexOf = null,
        bool construct = false
    )
    {
        var scenario = Load(fixture).Single(candidate => candidate.Name == name);
        // remove:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:PUTS[:SETS]][:castle-off@PASS], the last part for the emulator alone
        // (the castle's handler, which the engine does not port, is switched off there: docs/rules/pig-farming.md)
        var parts = scenario
            .Command.Split(':')
            .Where(part => !part.StartsWith("castle-off@", StringComparison.Ordinal))
            .ToArray();
        var map = MapOf(parts);
        var width = map.Size.Width;
        // A construct scenario empties the castle of planks and stones before every pass (below), so the castle's hand-out, which
        // would lay a good on the castle's flag at once where the original carries it out and lays it down about 130 passes later
        // (ConstructionBuilderTests), hands nothing out: no good the original has on the way exists in the engine, and the lines'
        // "fill<slot>+=NN" lend the counts the original's goods give the site at the passes it counted or received them.
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
        // The SETS write an object code (decimal) into a node's object byte before the pass, as the verb does: PASS,X,Y,CODE.
        var sets =
            parts.Length > 12 && parts[12].Length > 0
                ? parts[12].Split(';').Select(text => text.Split(',').Select(int.Parse).ToArray()).ToList()
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
                        Masked(expected, (hexOf ?? WorkerBytes.Hex)(game.Settlers[slot], width)),
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
                var words = game.RandomState;
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

            foreach (var set in sets.Where(candidate => candidate[0] == pass))
            {
                map.At(set[1], set[2]).Object = new MapObject { Code = (byte)set[3] };
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
                            kind == CommandKind.PlaceBuilding && node.Length > 2
                                ? TestCatalog.BuildingOf(node[2])
                                : null;
                        game.Enqueue(
                            new Command
                            {
                                Tick = game.TickCount,
                                Player = 0,
                                Kind = kind,
                                X = node[0],
                                Y = node[1],
                                Building = type,
                            }
                        );
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
            }

            foreach (var put in puts.Where(candidate => candidate[0] == pass))
            {
                // The fixture holds the original's good byte (the way plus one in bits 5 to 7) and destination word (the slot plus one).
                game.Goods.LayGood(
                    put[1] - 1,
                    put[2],
                    TestCatalog.GoodOf(put[3] & 0x1f),
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
                    ApplyFills(game, line);
                }

                Take(line);
                Check($"after pass {pass}", line, before);
            }
        }
    }
}
