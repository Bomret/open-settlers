using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SeedInfoTests
{
    const string SeedText = "1234567812345678";

    static Map MapOf(string seedText, int sizeValue)
    {
        Seed.TryParse(seedText, out var seed, out _);
        MapSize.TryFromValue(sizeValue, out var size, out _);

        return MapGenerator.Generate(seed.ToMapSeed(), size!);
    }

    [TestMethod]
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
    public void TheReportIsTheSameOnEveryRun()
    {
        Assert.AreEqual(SeedInfo.Report(SeedText, MapOf(SeedText, 2)), SeedInfo.Report(SeedText, MapOf(SeedText, 2)));
    }

    [TestMethod]
    public void TheTerrainCountsSumToTwoTrianglesForEachTile()
    {
        var text = SeedInfo.Report(SeedText, MapOf(SeedText, 3));

        var counts = SectionLines(text, "Terrain tiles").Select(line => int.Parse(line.Split(' ')[^1]));
        Assert.AreEqual(2 * 64 * 64, counts.Sum());
    }

    [TestMethod]
    public void TheResourceOneTotalMatchesTheMap()
    {
        var map = MapOf(SeedText, 1);

        var text = SeedInfo.Report(SeedText, map);

        var line = SectionLines(text, "Deposits").Single(l => l.StartsWith("  1:"));
        Assert.AreEqual($"amount {map.Resource1Total}", line[line.IndexOf("amount", StringComparison.Ordinal)..]);
    }

    [TestMethod]
    public void TheResourceLineSaysItSharesTheLakeBytes()
    {
        var text = SeedInfo.Report(SeedText, MapOf(SeedText, 1));

        StringAssert.Contains(text, "Resource: ");
        StringAssert.Contains(text, "same bytes as Lake");
    }

    [TestMethod]
    public void RunningAMapThatGeneratesExitsZeroWithTheReport()
    {
        Seed.TryParse(SeedText, out var seed, out _);

        var code = SeedInfo.Run(SeedText, seed, MapSize.Offered[0], out var text);

        Assert.AreEqual(0, code);
        Assert.AreEqual(SeedInfo.Report(SeedText, MapOf(SeedText, 1)), text);
    }

    [TestMethod]
    public void ASeedWithNoLandMassExitsOneWithAMessage()
    {
        Seed.TryParse(SeedText, out var seed, out _);

        var code = SeedInfo.Run(
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
