using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The expected lines in castle-placements.txt are the output of the emulator harness (tools/GhidraDump/emulate.py,
// command castle:N:S0,S1,S2:L:x,y) running the castle placement at 0x442704 of the reference Siedler1.exe on the
// generated map, after the evaluator at 0x44b6e6 judged the node a castle site (ADR 0008). Each line holds the map
// seed words, the size, the node, the start stock level, the evaluator's three values, the 26 stock words of the
// castle, the FNV-1a digests of the map bytes after the placement (flags, height, terrain, object, and the first
// byte of record B, which holds the lake detail, the resource deposit, and the link to a building or flag; then the
// three other bytes of record B), and the number of nodes whose bytes the placement changed.
[TestClass]
public sealed class CastlePlacementTests
{
    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    public static IEnumerable<object[]> Lines =>
        File.ReadAllLines("castle-placements.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(Lines))]
    public void ThePlacementMatchesTheEmulatorInStockAndMap(string expected)
    {
        var fields = Fields(expected);
        MapSize.TryFromValue(int.Parse(fields["size"], CultureInfo.InvariantCulture), out var size, out _);
        var map = MapGenerator.Generate(ParseSeed(fields["seed"]), size!);
        var before = Snapshot(map);
        var tile = fields["tile"].Split(',').Select(int.Parse).ToArray();
        var game = new Game(TestProgram.Tables(), map, int.Parse(fields["level"], CultureInfo.InvariantCulture));

        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, tile[0], tile[1]));
        game.Tick();

        var site = BuildSite.Evaluate(
            TestProgram.Tables(),
            MapGenerator.Generate(ParseSeed(fields["seed"]), size!),
            tile[0],
            tile[1],
            0,
            false
        );
        var actual =
            $"seed={fields["seed"]} size={fields["size"]} tile={fields["tile"]} level={fields["level"]} "
            + $"evaluator={site.Node}.{site.Site}.{site.Height} stock={string.Join(',', game.Stock)} "
            + $"layers={Digests(map)} changes={Changes(before, Snapshot(map))}";

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ARefusedPlacementChangesNoState()
    {
        var game = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]));
        var before = game.Digest();

        // The node 0,0 is a node state 7, site 0: no castle goes there (emulator: site:1:1234,5678,9ABC).
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 0, 0));
        var tick = game.Digest();
        game.Tick();
        game.Enqueue(new Command(1, 0, CommandKind.PlaceCastle, 0, 0));
        game.Tick();

        Assert.IsNull(game.Castle);
        Assert.IsEmpty(game.Stock);
        Assert.AreEqual(before, tick);
        var fresh = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]));
        fresh.Tick();
        fresh.Tick();
        Assert.AreEqual(fresh.Digest(), game.Digest());
    }

    [TestMethod]
    public void ACommandOtherThanThePlacementIsRefusedBeforeTheCastleStands()
    {
        var game = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]));
        var fresh = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]));

        game.Enqueue(new Command(0, 0, 2, 15, 5));
        game.Enqueue(new Command(0, 0, 3, 15, 5));
        game.Tick();
        fresh.Tick();

        Assert.AreEqual(fresh.Digest(), game.Digest());
    }

    [TestMethod]
    public void APlacementOutsideTheMapOrForAnotherPlayerIsRefused()
    {
        var game = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]));

        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, -1, 5));
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 32));
        game.Enqueue(new Command(0, 1, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        Assert.IsNull(game.Castle);
    }

    [TestMethod]
    public void OnlyOneCastleIsPlaced()
    {
        var game = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]));

        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Enqueue(new Command(1, 0, CommandKind.PlaceCastle, 9, 7));
        game.Tick();
        game.Tick();

        Assert.AreEqual(new Castle(15, 5, 15), game.Castle);
    }

    [TestMethod]
    public void ThePlacementChangesTheDigestAndTheSameCommandsGiveTheSameDigest()
    {
        static Game Run()
        {
            var game = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]), 7);
            game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
            game.Tick();

            return game;
        }

        var fresh = new Game(TestProgram.Tables(), MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]), 7);
        fresh.Tick();

        Assert.AreEqual(Run().Digest(), Run().Digest());
        Assert.AreNotEqual(fresh.Digest(), Run().Digest());
    }

    // Emulator: opponents:S0,S1,S2 (FreeGame_RandomizeOpponents at 0x4692d4) wrote the setting byte at +0x37a, the
    // human player's start stock level, as the third value of each line.
    [TestMethod]
    [DataRow("1234,5678,9ABC", 5)]
    [DataRow("FFFF,0001,8001", 37)]
    [DataRow("3905,0577,7739", 2)]
    public void TheStartStockLevelOfAFreeGameFollowsFromItsSeed(string words, int expected)
    {
        var map = ParseSeed(words);

        Assert.AreEqual(expected, StartStock.LevelOf(new Seed(map.W0, map.W1, map.W2)));
    }

    // The fields of a line: a word with an equals sign starts a field, and the words after it, up to the next such
    // word, are its value (the digests of "layers" are five words).
    private static Dictionary<string, string> Fields(string line)
    {
        var fields = new Dictionary<string, string>();
        var key = "";
        foreach (var word in line.Split(' '))
        {
            if (word.Contains('='))
            {
                var pair = word.Split('=');
                key = pair[0];
                fields[key] = pair[1];
            }
            else
            {
                fields[key] += " " + word;
            }
        }

        return fields;
    }

    private static byte[][] Snapshot(Map map) =>
        Enum.GetValues<MapLayer>().Select(layer => map.Layer(layer).ToArray()).ToArray();

    private static int Changes(byte[][] before, byte[][] after)
    {
        var count = 0;
        for (var node = 0; node < before[0].Length; node++)
        {
            if (before.Select((layer, i) => layer[node] != after[i][node]).Any(changed => changed))
            {
                count++;
            }
        }

        return count;
    }

    private static string Digests(Map map)
    {
        var zero = Digest(new byte[map.Size.Width * map.Size.Height]);

        return string.Join(
            ' ',
            new[] { MapLayer.Flags, MapLayer.Height, MapLayer.Terrain, MapLayer.Object, MapLayer.Resource }
                .Select(layer => Digest(map.Layer(layer).ToArray()))
                .Concat([zero, zero, zero])
                .Select(hash => $"{hash:X16}")
        );
    }

    private static ulong Digest(byte[] bytes) => bytes.Aggregate(FnvOffset, (hash, value) => (hash ^ value) * FnvPrime);

    private static MapSeed ParseSeed(string text)
    {
        var words = text.Split(',').Select(word => ushort.Parse(word, NumberStyles.HexNumber)).ToArray();

        return new MapSeed(words[0], words[1], words[2]);
    }
}
