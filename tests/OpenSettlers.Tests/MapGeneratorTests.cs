using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The expected digests in map-digests.txt are the output of the emulator harness (tools/GhidraDump/emulate.py,
// command gen:N:S0,S1,S2) running MapGen_Generate at 0x4216e9 of the reference Siedler1.exe (ADR 0008). Each
// line holds the size value, the map seed words, the pass, the generator words after the pass, and the FNV-1a
// digests of the flags, height, terrain, lake, object, and resource layers (docs/rules/map-form.md), and after
// CountResource1 the total of resource type 1. The seeds are 1234,5678,9ABC,
// 6359,A0D2,B4FA (the map seed of the typed screen seed 1234567812345678), and FFFF,0001,8001.
[TestClass]
public sealed class MapGeneratorTests
{
    private static readonly string[] Seeds = ["1234,5678,9ABC", "6359,A0D2,B4FA", "FFFF,0001,8001"];

    public static IEnumerable<object[]> Passes => Enum.GetValues<MapPass>().Select(pass => new object[] { pass });

    [TestMethod]
    [DynamicData(nameof(Passes))]
    public void EveryPassMatchesTheEmulatorForEverySizeAndSeed(MapPass pass)
    {
        var expected = ExpectedLines().Where(line => line.Pass == pass).ToList();

        var actual = new List<ExpectedLine>();
        foreach (var size in MapSize.Offered)
        {
            foreach (var seed in Seeds)
            {
                actual.Add(Observe(size, ParseSeed(seed)).Single(line => line.Pass == pass));
            }
        }

        CollectionAssert.AreEqual(
            expected.Select(line => line.Text).ToList(),
            actual.Select(line => line.Text).ToList()
        );
    }

    [TestMethod]
    public void TheOfferedSizesAreThoseTheEmulatorFindsFromTheSizeValue()
    {
        // Emulator: gen:1:... gives width=32 height=32, gen:2:... 64 by 32, gen:3:... 64 by 64.
        CollectionAssert.AreEqual(
            new[] { (1, 32, 32), (2, 64, 32), (3, 64, 64) },
            MapSize.Offered.Select(size => (size.Value, size.Width, size.Height)).ToArray()
        );
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(4)]
    [DataRow(-1)]
    public void ASizeOutsideTheOriginalsSetIsRefusedWithAMessage(int value)
    {
        var accepted = MapSize.TryFromValue(value, out var size, out var problem);

        Assert.IsFalse(accepted);
        Assert.IsNull(size);
        Assert.AreEqual($"the map size {value} is not one of the sizes the original offers: 1, 2, 3", problem);
    }

    [TestMethod]
    public void ASizeOutsideTheOriginalsSetGeneratesNothing()
    {
        var passes = 0;

        var generated = MapGenerator.TryGenerate(
            TestProgram.Made(),
            new MapSeed(1, 2, 3),
            4,
            (_, _, _) => passes++,
            out var map,
            out var problem
        );

        Assert.IsFalse(generated);
        Assert.IsNull(map);
        Assert.IsNotNull(problem);
        Assert.AreEqual(0, passes);
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(-1, -1)]
    [DataRow(0, -1)]
    public void ALargeObjectNextToAnOccupiedTileIsRemovedAndKeepsTheWaterBit(int dx, int dy)
    {
        Assert.AreEqual(0x80, ObjectAfterOverlap(objectClass: 2, dx, dy));
    }

    [TestMethod]
    public void AnObjectOfClassOneNextToAnOccupiedTileStays()
    {
        Assert.AreEqual(0x80 | MadeCode, ObjectAfterOverlap(objectClass: 1, -1, 0));
    }

    private const int MadeCode = 5;

    // Generates with made tables that give MadeCode the class, puts that code with the water bit on one tile before the
    // overlap pass and marks its neighbor at (dx, dy) as occupied, and returns the tile's object byte after the pass.
    private static int ObjectAfterOverlap(int objectClass, int dx, int dy)
    {
        var made = TestProgram.Made();
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MadeCode] = (byte)objectClass;
        var tables = new RuleTables(classes, made.StartStock);
        var tile = -1;

        var map = MapGenerator.Generate(
            tables,
            new MapSeed(1, 2, 3),
            MapSize.Offered[0],
            (pass, map, _) =>
            {
                if (pass != MapPass.PlaceDeposits4)
                {
                    return;
                }

                tile = map.Index(10, 10);
                map.Bytes(MapLayer.Object)[tile] = 0x80 | MadeCode;
                map.Bytes(MapLayer.Flags)[map.Index(10 + dx, 10 + dy)] |= 0x40;
            }
        );

        return Layers.Of(map, MapLayer.Object)[tile];
    }

    [TestMethod]
    public void TheSameSeedAndSizeGiveTheSameMapEveryTime()
    {
        var seed = ParseSeed(Seeds[0]);
        var size = MapSize.Offered[1];

        var first = MapGenerator.Generate(TestProgram.Made(), seed, size);
        var second = MapGenerator.Generate(TestProgram.Made(), seed, size);

        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            CollectionAssert.AreEqual(Layers.Of(first, layer), Layers.Of(second, layer));
        }
    }

    [TestMethod]
    public void TheObserverSeesThePassesInTheOriginalsOrder()
    {
        var seen = new List<MapPass>();

        MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed(1, 2, 3),
            MapSize.Offered[0],
            (pass, _, _) => seen.Add(pass)
        );

        CollectionAssert.AreEqual(Enum.GetValues<MapPass>(), seen);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void EachPassDrawsAsManyValuesAsTheRulesState(int sizeValue)
    {
        MapSize.TryFromValue(sizeValue, out var size, out _);
        var tiles = size!.Width * size.Height;
        var draws = new Dictionary<MapPass, int>();
        var previous = default(RandomGenerator);
        var lakes = 0;

        MapGenerator.Generate(
            TestProgram.Made(),
            ParseSeed(Seeds[0]),
            size,
            (pass, map, generator) =>
            {
                draws[pass] = pass == MapPass.Seed ? 0 : DrawsBetween(previous, generator);
                previous = generator;
                if (pass == MapPass.FillLakes)
                {
                    lakes = map.Layer(MapLayer.Flags).ToArray().Count(flags => (flags & 0x40) != 0);
                }
            }
        );

        Assert.AreEqual(tiles / 256, draws[MapPass.SeedHeights]);
        Assert.AreEqual(1 + 3 * (tiles / 256 + tiles / 64 + tiles / 16 + tiles / 4), draws[MapPass.SubdivideHeights]);
        Assert.AreEqual(lakes, draws[MapPass.FillLakes]);
        Assert.IsTrue(lakes > 0);
        foreach (var pass in new[] { MapPass.ClearMap, MapPass.LimitSlopes, MapPass.LowerToWaterLevel })
        {
            Assert.AreEqual(0, draws[pass]);
        }

        foreach (var pass in new[] { MapPass.ClassifyTerrain, MapPass.KeepMainLand, MapPass.ScaleHeights })
        {
            Assert.AreEqual(0, draws[pass]);
        }
    }

    [TestMethod]
    public void TheSpiralTableMatchesTheEmulator()
    {
        // Emulator: tables:x builds the table with the original's builder at 0x41e726; this is the FNV-1a digest
        // of its first 256 entries, a byte each for the column and the row step.
        var hash = 0xcbf29ce484222325UL;
        foreach (var (dx, dy) in MapGenerator.SpiralOffsets)
        {
            foreach (var value in new[] { (byte)dx, (byte)dy })
            {
                hash = (hash ^ value) * 0x100000001b3UL;
            }
        }

        Assert.AreEqual(256, MapGenerator.SpiralOffsets.Count);
        Assert.AreEqual(0x754DC58840EF0513UL, hash);
    }

    [TestMethod]
    public void TheObjectsAndDepositsListTheTilesOfTheirLayersAsTheEmulatedMapHasThem()
    {
        // Seed 6359,A0D2,B4FA at size 3. The engine's layer digests and the type 1 total of this map equal the
        // emulator's (map-digests.txt); the entries are read from that engine map, which is therefore the
        // emulator's, and pinned here as column, row, and object code or deposit type and amount.
        MapSize.TryFromValue(3, out var size, out _);

        var map = MapGenerator.Generate(TestProgram.Tables(), ParseSeed(Seeds[1]), size!);

        Assert.AreEqual(552, map.Objects.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new PlacedObject(9, 0, new MapObject(82)),
                new PlacedObject(22, 0, new MapObject(73)),
                new PlacedObject(45, 0, new MapObject(21)),
            },
            map.Objects.Take(3).ToList()
        );
        Assert.AreEqual(1091, map.Deposits.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new ResourceDeposit(34, 0, 3, 4),
                new ResourceDeposit(35, 0, 3, 8),
                new ResourceDeposit(36, 0, 3, 8),
            },
            map.Deposits.Take(3).ToList()
        );
        Assert.AreEqual(new ResourceDeposit(18, 20, 1, 4), map.Deposits.First(deposit => deposit.Type == 1));
        Assert.AreEqual(956, map.Resource1Total);
    }

    private static int DrawsBetween(RandomGenerator from, RandomGenerator to)
    {
        var steps = 0;
        for (var generator = from; generator != to; generator = generator.Step())
        {
            steps++;
            Assert.IsTrue(steps < 20_000, "the generator never reached the later state");
        }

        return steps;
    }

    private static IEnumerable<ExpectedLine> Observe(MapSize size, MapSeed seed)
    {
        var lines = new List<ExpectedLine>();
        MapGenerator.Generate(
            TestProgram.Tables(),
            seed,
            size,
            (pass, map, generator) =>
                lines.Add(
                    new ExpectedLine
                    {
                        Pass = pass,
                        Text =
                            $"{size.Value} {Format(seed)} {pass} {generator.W0:X4},{generator.W1:X4},{generator.W2:X4}"
                            + (
                                pass == MapPass.Seed
                                    ? ""
                                    : " "
                                        + string.Join(
                                            " ",
                                            Enum.GetValues<MapLayer>().Select(layer => $"{map.Digest(layer):X16}")
                                        )
                            )
                            + (pass == MapPass.CountResource1 ? $" total={map.Resource1Total}" : ""),
                    }
                )
        );

        return lines;
    }

    private static IEnumerable<ExpectedLine> ExpectedLines() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "map-digests.txt"))
            .Where(line => line.Length > 0)
            .Select(line => new ExpectedLine { Pass = Enum.Parse<MapPass>(line.Split(' ')[2]), Text = line });

    private static MapSeed ParseSeed(string words)
    {
        var parts = words.Split(',').Select(part => ushort.Parse(part, NumberStyles.HexNumber)).ToArray();

        return new MapSeed(parts[0], parts[1], parts[2]);
    }

    private static string Format(MapSeed seed) => $"{seed.W0:X4},{seed.W1:X4},{seed.W2:X4}";

    private sealed record ExpectedLine
    {
        public required MapPass Pass { get; init; }
        public required string Text { get; init; }
    }
}
