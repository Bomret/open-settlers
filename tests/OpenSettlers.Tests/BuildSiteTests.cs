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
        var map = MapGenerator.Generate(ParseSeed(seedText), size!);

        Assert.AreEqual(expected, Observe(seedText, map));
    }

    [TestMethod]
    public void TheCastlesOwnNodeIsNoCastleSiteOnceTheCastleStands()
    {
        var map = MapGenerator.Generate(ParseSeed("1234,5678,9ABC"), MapSize.Offered[0]);
        var site = BuildSite.Evaluate(map, 15, 5, 0, hasCastle: false);

        var game = new Game(map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        Assert.IsTrue(site.IsCastleSite);
        Assert.IsFalse(BuildSite.Evaluate(map, 15, 5, 0, hasCastle: true).IsCastleSite);
    }

    [TestMethod]
    public void AFlagIsAllowedWhereTheNodeStateIs4Or6Or7AndASiteExists()
    {
        Assert.IsTrue(new BuildSiteResult(7, 1, 0).AllowsFlag);
        Assert.IsTrue(new BuildSiteResult(6, 3, 0).AllowsFlag);
        Assert.IsTrue(new BuildSiteResult(4, 1, 0).AllowsFlag);
        Assert.IsFalse(new BuildSiteResult(5, 1, 0).AllowsFlag);
        Assert.IsFalse(new BuildSiteResult(7, 0, 0).AllowsFlag);
    }

    private static string Observe(string seedText, Map map)
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
                if (BuildSite.ObjectClass(map.Layer(MapLayer.Object)[y * width + x]) >= 3)
                {
                    skipped++;
                    continue;
                }

                var result = BuildSite.Evaluate(map, x, y, 0, hasCastle: false);
                foreach (var value in new[] { result.Node, result.Site, result.Height & 0xff, result.Height >> 8 })
                {
                    hash = (hash ^ (byte)value) * FnvPrime;
                }

                evaluated++;
                var key = $"{result.Node}.{result.Site}";
                kinds[key] = kinds.GetValueOrDefault(key) + 1;
                if (result.IsCastleSite)
                {
                    if (valid < 16)
                    {
                        first.Add($"{x},{y},{result.Height}");
                    }

                    valid++;
                }
            }
        }

        return $"seed={seedText} size={map.Size.Value} width={width} height={map.Size.Height} evaluated={evaluated} "
            + $"skipped={skipped} valid={valid} digest={hash:X16} "
            + $"kinds={string.Join(' ', kinds.Select(pair => $"{pair.Key}:{pair.Value}"))} first={string.Join(' ', first)}";
    }

    private static MapSeed ParseSeed(string text)
    {
        var words = text.Split(',').Select(word => ushort.Parse(word, NumberStyles.HexNumber)).ToArray();

        return new MapSeed(words[0], words[1], words[2]);
    }
}
