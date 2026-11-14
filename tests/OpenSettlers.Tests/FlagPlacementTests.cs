using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of flag-placements.txt are the output of the emulator verb flagput: (tools/GhidraDump/emulate.py), which
// places the castle on a generated map and then runs the flag button (0x442244, with the placement 0x4422c5 to its
// end) of the reference Siedler1.exe on each listed tile in turn, keeping the map between tiles (ADR 0008). Each line
// holds the map, the castle, the tile, whether the flag was placed, the evaluator's pair, the link word the tile
// holds afterwards, the total of resource 1, and the map bytes the attempt changed (position of the byte: 0 flags,
// 1 height, 2 terrain, 3 object, 4 the lake and resource byte).
[TestClass]
public sealed class FlagPlacementTests
{
    const long CastleTick = 0;

    public static IEnumerable<object[]> Runs =>
        File.ReadAllLines("flag-placements.txt")
            .Select(line => string.Join(' ', line.Split(' ').Take(4)))
            .Distinct()
            .Select(run => new object[] { run });

    [TestMethod]
    [DynamicData(nameof(Runs))]
    [TestCategory(Needs.Install)]
    public void TheFlagsAreAllowedAndWrittenAsTheEmulatorsFlagButtonDoes(string run)
    {
        var lines = File.ReadAllLines("flag-placements.txt").Where(line => line.StartsWith(run + ' ')).ToArray();
        var header = Fields(lines[0]);
        MapSize.TryFromValue(int.Parse(header["size"], CultureInfo.InvariantCulture), out var size, out _);
        var map = MapGenerator.Generate(TestProgram.Tables(), ParseSeed(header["seed"]), size!);
        var castle = header["castle"].Split(',').Select(int.Parse).ToArray();
        var game = GameFactory.Create(
            TestProgram.Tables(),
            map,
            int.Parse(header["level"], CultureInfo.InvariantCulture)
        );
        game.Enqueue(new Command(CastleTick, 0, CommandKind.PlaceCastle, castle[0], castle[1]));
        game.ApplyCommands();

        foreach (var line in lines)
        {
            var fields = Fields(line);
            var tile = fields["tile"].Split(',').Select(int.Parse).ToArray();
            var before = Snapshot(map);
            var flags = game.Flags.Count;

            game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, tile[0], tile[1]));
            game.ApplyCommands();

            var placed = game.Flags.Count > flags;
            var actual =
                $"outcome={(placed ? "PLACED" : "REFUSED")} link={Layers.Of(map, MapLayer.Resource)[map.Size.Width * tile[1] + tile[0]]} "
                + $"resource1={map.Resource1Total} changes={Changes(before, Snapshot(map), map.Size.Width)}";
            var expected =
                $"outcome={fields["outcome"]} link={fields["link"]} resource1={fields["resource1"]} changes={line[(line.IndexOf(" changes=", StringComparison.Ordinal) + " changes=".Length)..]}";

            Assert.AreEqual(expected, actual, $"{run} tile {fields["tile"]}");
        }
    }

    [TestMethod]
    public void TheCastlesFlagIsTheFirstFlagAndAPlacedFlagTakesTheNextSlot()
    {
        var game = GameWithCastle();
        var (x, y) = FirstFlagSite(game);

        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, y));
        game.Tick();

        Assert.AreEqual((16, 6), (game.Flags[0].X, game.Flags[0].Y));
        Assert.AreEqual((x, y), (game.Flags[1].X, game.Flags[1].Y));
        Assert.AreEqual(1, game.Map.At(x, y).Link);
        Assert.AreEqual(MapObjectKind.Flag, game.Map.At(x, y).Object.Kind);
    }

    [TestMethod]
    public void ARefusedFlagChangesNoState()
    {
        var game = GameWithCastle();
        var same = GameWithCastle();

        // The castle's own node holds a building: no flag goes there.
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 15, 5));
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 16, 6));
        game.Tick();
        same.Tick();

        Assert.AreEqual(1, game.Flags.Count);
        Assert.AreEqual(same.Digest(), game.Digest());
    }

    [TestMethod]
    public void AFlagIsRefusedBeforeTheCastleStandsAndOutsideTheMapAndForAnotherPlayer()
    {
        var game = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );
        var fresh = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0])
        );
        game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, 15, 5));
        game.Tick();
        fresh.Tick();
        Assert.AreEqual(fresh.Digest(), game.Digest());

        game = GameWithCastle();
        fresh = GameWithCastle();
        var (x, y) = FirstFlagSite(game);
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, -1, y));
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, 32));
        game.Enqueue(new Command(game.TickCount, 1, CommandKind.PlaceFlag, x, y));
        game.Tick();
        fresh.Tick();

        Assert.AreEqual(fresh.Digest(), game.Digest());
    }

    [TestMethod]
    public void TheFlagTableAndItsSlotsAreInTheDigest()
    {
        var one = GameWithCastle();
        var two = GameWithCastle();
        var (x, y) = FirstFlagSite(two);
        two.Enqueue(new Command(two.TickCount, 0, CommandKind.PlaceFlag, x, y));
        two.Tick();
        one.Tick();

        Assert.AreNotEqual(one.Digest(), two.Digest());
    }

    [TestMethod]
    public void AFlagAtTheEdgeOfTheMapIsWrittenOnTheNodeAcrossTheWrap()
    {
        var game = GameWithCastle(17, 0, "6359,A0D2,B4FA");
        var width = game.Map.Size.Width;
        var (x, y) = Enumerable
            .Range(0, width)
            .Select(column => (X: column, Y: game.Map.Size.Height - 1))
            .First(node => Allows(game, node.X, node.Y));

        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, y));
        game.Tick();

        Assert.AreEqual(2, game.Flags.Count);
        Assert.AreEqual((x, y), (game.Flags[1].X, game.Flags[1].Y));
        Assert.AreEqual(MapObjectKind.Flag, game.Map.At(x, y).Object.Kind);
    }

    [TestMethod]
    public void The257thFlagIsRefusedWhereTheNodeAllowsIt()
    {
        var game = GameWithCastle();
        var width = game.Map.Size.Width;
        for (var pass = 0; pass < 3; pass++)
        {
            for (var node = 0; node < width * game.Map.Size.Height; node++)
            {
                game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, node % width, node / width));
            }

            game.Tick();
        }

        var (x, y) = FirstFlagSite(game);
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, y));
        game.Tick();

        Assert.AreEqual(256, game.Flags.Count);
    }

    [TestMethod]
    public void ADepositOfZeroAmountWithTypeBitsMovesToTheFirstEmptyNeighbour()
    {
        // Inferred: read from 0x449f58 and 0x44a0aa, no emulator run backs it (open lead in
        // docs/milestones/m3-first-loop.md). The first neighbour is offered the deposit even when its amount is 0, so
        // the type bits move and the amount 0 with them.
        var map = new Map(MapSize.Offered[0]);
        map.At(5, 5).DepositType = 1;

        FlagPlacement.Apply(map, 5, 5, 3);

        Assert.AreEqual(1, map.At(6, 5).DepositType);
        Assert.AreEqual(0, map.At(6, 5).DepositAmount);
    }

    [TestMethod]
    public void ADepositMovesAcrossTheColumnWrap()
    {
        // Inferred: read from 0x449f58, no emulator run backs it (open lead in docs/milestones/m3-first-loop.md). The
        // first neighbour is east, which wraps from the last column to the first.
        var map = new Map(MapSize.Offered[0]);
        var last = map.Size.Width - 1;
        map.At(last, 5).DepositType = 1;
        map.At(last, 5).DepositAmount = 3;

        FlagPlacement.Apply(map, last, 5, 3);

        Assert.AreEqual(1, map.At(0, 5).DepositType);
        Assert.AreEqual(3, map.At(0, 5).DepositAmount);
    }

    static Game GameWithCastle(int x = 15, int y = 5, string seed = "1234,5678,9ABC")
    {
        var game = GameFactory.Create(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), ParseSeed(seed), MapSize.Offered[0])
        );
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, x, y));
        game.Tick();

        return game;
    }

    static bool Allows(Game game, int x, int y) =>
        BuildSite.Evaluate(TestProgram.Made(), game.Map, x, y, 0, hasCastle: true).AllowsFlag;

    // The first node, in row-major order, where the flag button puts a flag.
    static (int X, int Y) FirstFlagSite(Game game) =>
        Enumerable
            .Range(0, game.Map.Size.Width * game.Map.Size.Height)
            .Select(node => (X: node % game.Map.Size.Width, Y: node / game.Map.Size.Width))
            .First(node => Allows(game, node.X, node.Y));

    static Dictionary<string, string> Fields(string line) =>
        line.Split(' ')
            .Select(word => word.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1]);

    static byte[][] Snapshot(Map map) =>
        new[] { MapLayer.Flags, MapLayer.Height, MapLayer.Terrain, MapLayer.Object, MapLayer.Resource }
            .Select(layer => Layers.Of(map, layer))
            .ToArray();

    // The changed bytes as the harness lists them: each changed tile in row-major order with "position=old>new".
    static string Changes(byte[][] before, byte[][] after, int width) =>
        string.Join(
            ' ',
            Enumerable
                .Range(0, before[0].Length)
                .Select(node =>
                    (
                        Node: node,
                        Diff: string.Join(
                            '/',
                            Enumerable
                                .Range(0, before.Length)
                                .Where(i => before[i][node] != after[i][node])
                                .Select(i => $"{i}={before[i][node]:X2}>{after[i][node]:X2}")
                        )
                    )
                )
                .Where(entry => entry.Diff.Length > 0)
                .Select(entry => $"{entry.Node % width},{entry.Node / width}:{entry.Diff}")
        );

    static MapSeed ParseSeed(string text)
    {
        var words = text.Split(',').Select(word => ushort.Parse(word, NumberStyles.HexNumber)).ToArray();

        return new MapSeed(words[0], words[1], words[2]);
    }
}
