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
    const ulong FnvOffset = 0xcbf29ce484222325;
    const ulong FnvPrime = 0x100000001b3;

    public static IEnumerable<object[]> Lines =>
        File.ReadAllLines("castle-placements.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(Lines))]
    [TestCategory(Needs.Install)]
    public void ThePlacementMatchesTheEmulatorInStockAndMap(string expected)
    {
        var fields = Fields(expected);
        MapSize.TryFromValue(int.Parse(fields["size"], CultureInfo.InvariantCulture), out var size, out _);
        var map = MapGenerator.Generate(TestProgram.Tables(), ParseSeed(fields["seed"]), size!);
        var before = Snapshot(map);
        var tile = fields["tile"].Split(',').Select(int.Parse).ToArray();
        var game = GameFactory.Create(
            TestProgram.Tables(),
            map,
            int.Parse(fields["level"], CultureInfo.InvariantCulture)
        );

        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = tile[0],
                Y = tile[1],
            }
        );
        game.ApplyCommands();

        var site = BuildSite.Evaluate(
            TestProgram.Tables(),
            MapGenerator.Generate(TestProgram.Tables(), ParseSeed(fields["seed"]), size!),
            tile[0],
            tile[1],
            0,
            false
        );
        var actual =
            $"seed={fields["seed"]} size={fields["size"]} tile={fields["tile"]} level={fields["level"]} "
            + $"evaluator={site.Node}.{site.Site}.{site.Height} stock={string.Join(',', game.CastleStock.Stock)} "
            + $"layers={Digests(map)} changes={Changes(before, Snapshot(map))}";

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ARefusedPlacementChangesNoState()
    {
        var game = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );
        var before = game.Digest();

        // The node 0,0 is a node state 7, site 0: no castle goes there (emulator: site:1:1234,5678,9ABC).
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 0,
                Y = 0,
            }
        );
        var tick = game.Digest();
        game.Tick();
        game.Enqueue(
            new Command
            {
                Tick = 1,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 0,
                Y = 0,
            }
        );
        game.Tick();

        Assert.IsNull(game.CastleStock.Castle);
        Assert.IsEmpty(game.CastleStock.Stock);
        Assert.AreEqual(before, tick);
        var fresh = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );
        fresh.Tick();
        fresh.Tick();
        Assert.AreEqual(fresh.Digest(), game.Digest());
    }

    [TestMethod]
    public void ACommandOtherThanThePlacementIsRefusedBeforeTheCastleStands()
    {
        var game = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );
        var fresh = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );

        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = 2,
                X = 15,
                Y = 5,
            }
        );
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = 3,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();
        fresh.Tick();

        Assert.AreEqual(fresh.Digest(), game.Digest());
    }

    [TestMethod]
    public void APlacementOutsideTheMapOrForAnotherPlayerIsRefused()
    {
        var game = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );

        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = -1,
                Y = 5,
            }
        );
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 32,
            }
        );
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 1,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();

        Assert.IsNull(game.CastleStock.Castle);
    }

    [TestMethod]
    public void OnlyOneCastleIsPlaced()
    {
        var game = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );

        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Enqueue(
            new Command
            {
                Tick = 1,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 9,
                Y = 7,
            }
        );
        game.Tick();
        game.Tick();

        Assert.AreEqual(
            new Castle
            {
                X = 15,
                Y = 5,
                Height = 15,
            },
            game.CastleStock.Castle
        );
    }

    [TestMethod]
    public void ThePlacementChangesTheDigestAndTheSameCommandsGiveTheSameDigest()
    {
        static Game Run()
        {
            var game = GameFactory.Create(
                TestProgram.Made(),
                MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]),
                7
            );
            game.Enqueue(
                new Command
                {
                    Tick = 0,
                    Player = 0,
                    Kind = CommandKind.PlaceCastle,
                    X = 15,
                    Y = 5,
                }
            );
            game.Tick();

            return game;
        }

        var fresh = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]),
            7
        );
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

        Assert.AreEqual(
            expected,
            StartStock.LevelOf(
                new Seed
                {
                    W0 = map.W0,
                    W1 = map.W1,
                    W2 = map.W2,
                }
            )
        );
    }

    // The fields of a line: a word with an equals sign starts a field, and the words after it, up to the next such
    // word, are its value (the digests of "layers" are five words).
    static Dictionary<string, string> Fields(string line)
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

    static byte[][] Snapshot(Map map) => [.. Enum.GetValues<MapLayer>().Select(layer => Layers.Of(map, layer))];

    static int Changes(byte[][] before, byte[][] after)
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

    static string Digests(Map map)
    {
        var zero = Digest(new byte[map.Size.Width * map.Size.Height]);

        return string.Join(
            ' ',
            new[] { MapLayer.Flags, MapLayer.Height, MapLayer.Terrain, MapLayer.Object, MapLayer.Resource }
                .Select(layer => Digest(Layers.Of(map, layer)))
                .Concat([zero, zero, zero])
                .Select(hash => $"{hash:X16}")
        );
    }

    static ulong Digest(byte[] bytes) => bytes.Aggregate(FnvOffset, (hash, value) => (hash ^ value) * FnvPrime);

    static MapSeed ParseSeed(string text)
    {
        var words = text.Split(',').Select(word => ushort.Parse(word, NumberStyles.HexNumber)).ToArray();

        return new MapSeed
        {
            W0 = words[0],
            W1 = words[1],
            W2 = words[2],
        };
    }
}
