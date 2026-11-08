using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The expected lines in build-sites.txt are the output of the emulator harness (tools/GhidraDump/emulate.py,
// command site:N:S0,S1,S2) running the build-site evaluator at 0x44b6e6 of the reference Siedler1.exe on every
// node of the generated map, for a player without a castle (ADR 0008). Each line holds the map seed words, the
// size, the number of nodes judged, the number whose result is a castle site (node state 7, site 5), the FNV-1a
// digest of every result in row-major order, the count of each (node state, site) pair, and the first castle sites
// with their height. The seeds are 1234,5678,9ABC, 6359,A0D2,B4FA (the map seed of the typed screen seed
// 1234567812345678), and FFFF,0001,8001.
[TestClass]
public sealed class BuildSiteTests
{
    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    public static IEnumerable<object[]> Lines =>
        File.ReadAllLines("build-sites.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(Lines))]
    public void TheEvaluatorMatchesTheEmulatorOnEveryNodeOfTheMap(string expected)
    {
        var seedText = expected.Split(' ')[0]["seed=".Length..];
        var sizeValue = int.Parse(expected.Split(' ')[1]["size=".Length..], CultureInfo.InvariantCulture);
        MapSize.TryFromValue(sizeValue, out var size, out _);
        var map = MapGenerator.Generate(TestProgram.Tables(), ParseSeed(seedText), size!);

        Assert.AreEqual(
            expected,
            $"seed={seedText} size={sizeValue} width={map.Size.Width} height={map.Size.Height} {Observe(map, hasCastle: false)}"
        );
    }

    public static IEnumerable<object[]> AfterLines =>
        File.ReadAllLines("castle-sites.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(AfterLines))]
    public void WithTheCastleStandingTheEvaluatorMatchesTheEmulatorOnEveryNodeOfTheMap(string expected)
    {
        var fields = expected
            .Split(' ')
            .Take(4)
            .ToDictionary(field => field.Split('=')[0], field => field.Split('=')[1]);
        MapSize.TryFromValue(int.Parse(fields["size"], CultureInfo.InvariantCulture), out var size, out _);
        var map = MapGenerator.Generate(TestProgram.Tables(), ParseSeed(fields["seed"]), size!);
        var tile = fields["tile"].Split(',').Select(int.Parse).ToArray();
        var game = GameFactory.Create(TestProgram.Tables(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, tile[0], tile[1]));
        game.Tick();

        Assert.AreEqual(
            expected,
            $"seed={fields["seed"]} size={fields["size"]} tile={fields["tile"]} level={fields["level"]} {Observe(map, hasCastle: true)}"
        );
    }

    [TestMethod]
    public void TheCastlesOwnNodeIsNoCastleSiteOnceTheCastleStands()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]);
        var site = BuildSite.Evaluate(TestProgram.Made(), map, 15, 5, 0, hasCastle: false);

        var game = GameFactory.Create(TestProgram.Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        Assert.IsTrue(site.IsCastleSite);
        Assert.IsFalse(BuildSite.Evaluate(TestProgram.Made(), map, 15, 5, 0, hasCastle: true).IsCastleSite);
    }

    // flag-verdicts.txt holds the output of the emulator verb flag: and, after a castle, castle-flags: (tools/GhidraDump,
    // ADR 0008), which run the flag button (0x442244) on every tile and count, per pair (node state, site) the
    // evaluator left, the tiles where the routine goes on to put a flag. Each pair puts a flag on all its tiles or
    // on none. No pair of the node states 4 and 5 occurs on these maps, so no verdict is pinned for them.
    public static IEnumerable<object[]> FlagLines =>
        File.ReadAllLines("flag-verdicts.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(FlagLines))]
    public void TheFlagIsAllowedWhereTheEmulatorsFlagButtonPutsOne(string expected)
    {
        var pairs = expected[(expected.IndexOf("pairs=", StringComparison.Ordinal) + "pairs=".Length)..].Split(' ');

        foreach (var pair in pairs)
        {
            var kind = pair.Split(':')[0].Split('.');
            var counts = pair.Split(':')[1].Split('/');
            var flagged = int.Parse(counts[0], CultureInfo.InvariantCulture);
            var judged = int.Parse(counts[1], CultureInfo.InvariantCulture);
            Assert.IsTrue(flagged == 0 || flagged == judged, $"the emulator splits the pair {pair}");
            var result = new BuildSiteResult(
                int.Parse(kind[0], CultureInfo.InvariantCulture),
                int.Parse(kind[1], CultureInfo.InvariantCulture),
                0
            );

            Assert.AreEqual(flagged == judged, result.AllowsFlag, $"pair {pair}");
        }
    }

    // The judgment of every node whose object has a class below 3, as the harness line states it. Without a castle
    // the valid nodes are the castle sites; with one they are the sites of a large building (site 4).
    private static string Observe(Map map, bool hasCastle)
    {
        var width = map.Size.Width;
        var hash = FnvOffset;
        var evaluated = 0;
        var skipped = 0;
        var valid = 0;
        var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var first = new List<string>();
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (TestProgram.Tables().ClassOf(map.At(x, y).Object).IsFlagOrBuilding)
                {
                    skipped++;
                    continue;
                }

                var result = BuildSite.Evaluate(TestProgram.Tables(), map, x, y, 0, hasCastle);
                foreach (var value in new[] { result.Node, result.Site, result.Height & 0xff, result.Height >> 8 })
                {
                    hash = (hash ^ (byte)value) * FnvPrime;
                }

                evaluated++;
                var key = $"{result.Node}.{result.Site}";
                kinds[key] = kinds.GetValueOrDefault(key) + 1;
                if (hasCastle ? result.Site == 4 : result.IsCastleSite)
                {
                    if (valid < 16)
                    {
                        first.Add($"{x},{y},{result.Height}");
                    }

                    valid++;
                }
            }
        }

        return $"evaluated={evaluated} skipped={skipped} valid={valid} digest={hash:X16} "
            + $"kinds={string.Join(' ', kinds.Select(pair => $"{pair.Key}:{pair.Value}"))} first={string.Join(' ', first)}";
    }

    private static MapSeed ParseSeed(string text)
    {
        var words = text.Split(',').Select(word => ushort.Parse(word, NumberStyles.HexNumber)).ToArray();

        return new MapSeed(words[0], words[1], words[2]);
    }
}
