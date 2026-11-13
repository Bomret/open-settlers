using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SeedInfoTests
{
    const string SeedText = "1234567812345678";

    static Map MapOf(string seedText, int sizeValue, RuleTables? tables = null)
    {
        Seed.TryParse(seedText, out var seed, out _);
        MapSize.TryFromValue(sizeValue, out var size, out _);

        return MapGenerator.Generate(tables ?? TestProgram.Tables(), seed.ToMapSeed(), size!);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheReportOfOneSeedAndSizeIsPinned()
    {
        var text = SeedInfo.Report(SeedText, MapOf(SeedText, 1));

        Assert.AreEqual(
            "Seed: 1234567812345678\nSize: 1 (32 x 32 tiles)\nLayer digests (FNV-1a 64, row-major):\n"
                + "  Flags: 87defb91f0dae965\n  Height: 3882dc6272dff080\n  Terrain: b42d2b6493d50015\n"
                + "  Lake: 753a6157b48c6dea\n  Object: a0ec71c9fae7bd4a\n"
                + "  Resource: 753a6157b48c6dea (same bytes as Lake, so the digests match)\n"
                + "Terrain tiles (both triangles of each tile, by type):\n"
                + "  1: 1\n  2: 28\n  3: 68\n  4: 80\n  5: 936\n  6: 82\n  7: 48\n  8: 36\n  9: 24\n  10: 9\n"
                + "  11: 77\n  12: 120\n  13: 172\n  14: 104\n  15: 263\n"
                + "Objects (by code):\n  8: 7\n  9: 3\n  10: 4\n  11: 5\n  12: 5\n  13: 4\n  14: 2\n  15: 6\n"
                + "  16: 10\n  17: 4\n  18: 5\n  19: 6\n  20: 6\n  21: 5\n  22: 3\n  23: 6\n  73: 1\n  74: 2\n"
                + "  75: 1\n  77: 3\n  78: 2\n  79: 4\n  80: 3\n  82: 8\n  83: 2\n  84: 1\n  85: 5\n  92: 1\n"
                + "Deposits (by resource type):\n  1: 37 tiles, amount 260\n  2: 63 tiles, amount 464\n"
                + "  3: 164 tiles, amount 1300\n  4: 34 tiles, amount 340\n",
            text
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheReportIsTheSameOnEveryRun()
    {
        Assert.AreEqual(SeedInfo.Report(SeedText, MapOf(SeedText, 2)), SeedInfo.Report(SeedText, MapOf(SeedText, 2)));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheTerrainCountsSumToTwoTrianglesForEachTile()
    {
        var text = SeedInfo.Report(SeedText, MapOf(SeedText, 3));

        var counts = SectionLines(text, "Terrain tiles").Select(line => int.Parse(line.Split(' ')[^1]));
        Assert.AreEqual(2 * 64 * 64, counts.Sum());
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheResourceOneTotalMatchesTheMap()
    {
        var map = MapOf(SeedText, 1);

        var text = SeedInfo.Report(SeedText, map);

        var line = SectionLines(text, "Deposits").Single(l => l.StartsWith("  1:"));
        Assert.AreEqual($"amount {map.Resource1Total}", line[line.IndexOf("amount", StringComparison.Ordinal)..]);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void RunningAMapThatGeneratesExitsZeroWithTheReport()
    {
        Seed.TryParse(SeedText, out var seed, out _);

        var code = SeedInfo.Run(TestProgram.Tables, SeedText, seed, MapSize.Offered[0], out var text);

        Assert.AreEqual(0, code);
        Assert.AreEqual(SeedInfo.Report(SeedText, MapOf(SeedText, 1)), text);
    }

    // The report of a made-up map with no object and no deposit, whose terrain is all type 0 and whose layers are all zero bytes. The
    // expected text is hand arithmetic: the layer digest is the FNV-1a 64 of 1024 zero bytes (32 x 32 tiles, FNV constants of the
    // FNV specification), the 2 * 1024 triangles of the 1024 tiles are all of type 0, and the two last sections hold no line.
    [TestMethod]
    public void TheReportOfAMapWithNoObjectAndNoDepositHasEmptySections()
    {
        var map = new Map(MapSize.Offered[0]);
        var zeros = Fnv(new byte[32 * 32]).ToString("x16");

        var text = SeedInfo.Report("7", map);

        Assert.AreEqual(
            "Seed: 7\nSize: 1 (32 x 32 tiles)\nLayer digests (FNV-1a 64, row-major):\n"
                + $"  Flags: {zeros}\n  Height: {zeros}\n  Terrain: {zeros}\n  Lake: {zeros}\n  Object: {zeros}\n"
                + $"  Resource: {zeros} (same bytes as Lake, so the digests match)\n"
                + "Terrain tiles (both triangles of each tile, by type):\n  0: 2048\n"
                + "Objects (by code):\nDeposits (by resource type):\n",
            text
        );
    }

    // Tables of made-up values generate a map with no install, so the run exits 0 with the report of that map.
    [TestMethod]
    public void RunningMadeUpTablesExitsZeroWithTheReportOfTheirMap()
    {
        Seed.TryParse(SeedText, out var seed, out _);
        var expected = SeedInfo.Report(SeedText, MapOf(SeedText, 1, TestProgram.Made()));

        var code = SeedInfo.Run(TestProgram.Made, SeedText, seed, MapSize.Offered[0], out var text);

        Assert.AreEqual(0, code);
        Assert.AreEqual(expected, text);
        StringAssert.StartsWith(text, "Seed: 1234567812345678\nSize: 1 (32 x 32 tiles)\n");
    }

    static ulong Fnv(byte[] bytes) =>
        bytes.Aggregate(0xcbf29ce484222325, (hash, value) => (hash ^ value) * 0x100000001b3);

    [TestMethod]
    public void AnyOtherGeneratorFailureExitsOneWithItsMessage()
    {
        Seed.TryParse(SeedText, out var seed, out _);

        var code = SeedInfo.Run(
            TestProgram.Made,
            SeedText,
            seed,
            MapSize.Offered[0],
            out var text,
            (_, _) => throw new IndexOutOfRangeException("a tile outside the map")
        );

        Assert.AreEqual(1, code);
        StringAssert.Contains(text, "a tile outside the map");
    }

    [TestMethod]
    [DataRow("io")]
    [DataRow("data")]
    [DataRow("access")]
    public void UnreadableTablesExitTwoWithTheProblemAndNoMap(string failure)
    {
        Seed.TryParse(SeedText, out var seed, out _);
        var generated = false;

        var code = SeedInfo.Run(
            () =>
                throw (
                    failure switch
                    {
                        "io" => new System.IO.IOException("no install"),
                        "data" => new System.IO.InvalidDataException("no install"),
                        _ => (Exception)new UnauthorizedAccessException("no install"),
                    }
                ),
            SeedText,
            seed,
            MapSize.Offered[0],
            out var text,
            (_, _) =>
            {
                generated = true;
                throw new InvalidOperationException();
            }
        );

        Assert.AreEqual(2, code);
        Assert.AreEqual("no install", text);
        Assert.IsFalse(generated);
    }

    // A stub generator stands in: no seed is known that reaches the generator's no-land-mass refusal, and a search
    // for one would cost a generation per candidate.
    [TestMethod]
    public void ASeedWithNoLandMassExitsOneWithAMessage()
    {
        Seed.TryParse(SeedText, out var seed, out _);

        var code = SeedInfo.Run(
            TestProgram.Made,
            SeedText,
            seed,
            MapSize.Offered[0],
            out var text,
            (_, _) => throw new InvalidOperationException("no land mass covers a quarter of the map")
        );

        Assert.AreEqual(1, code);
        StringAssert.Contains(text, "no map for this seed");
        StringAssert.Contains(text, "no land mass");
    }

    static string[] SectionLines(string text, string heading)
    {
        var lines = text.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith(heading, StringComparison.Ordinal));

        return [.. lines.Skip(start + 1).TakeWhile(line => line.StartsWith("  ", StringComparison.Ordinal))];
    }
}
