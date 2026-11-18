using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of busy-road-walks.txt are the output of the emulator verb goods (tools/GhidraDump/emulate.py, ADR 0008) on the mill's game of
// mill-walks.txt with four stones on the mill's flag: the road to the castle gets a second carrier, drains, and lets one carrier go home
// (docs/rules/carriers.md). Each line gives, for a pass, the 16 bytes of the carriers (s2=, s5=) and the status byte and the six road bytes
// of the castle's flag (f1=) and the mill's flag (f2=) where they changed, and the generator's words (rng=). The engine runs the same
// command (MillerTests.Start) and must hold the same values at each listed pass. The flags carry the original's numbers, which count the
// dummy slot the original's game start takes, so the engine's slot is one less.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class BusyRoadTests
{
    const string Fixture = "busy-road-walks.txt";

    sealed record Scenario
    {
        public required string Name { get; init; }

        public required string Command { get; init; }

        public required List<Dictionary<string, string>> Lines { get; init; }
    }

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    static List<Scenario> Load()
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(Fixture).Where(line => !line.StartsWith("//", StringComparison.Ordinal)))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var parts = line[2..].Split(' ', 2);
                scenarios.Add(
                    new Scenario
                    {
                        Name = parts[0],
                        Command = parts[1],
                        Lines = [],
                    }
                );
                continue;
            }

            scenarios[^1]
                .Lines.Add(line.Split(' ').Select(field => field.Split('=')).ToDictionary(p => p[0], p => p[1]));
        }

        return scenarios;
    }

    static string FlagHex(Game game, int flag) =>
        Convert.ToHexString([
            FlagEncoder.Status(game.Flags[flag]),
            .. Enumerable.Range(0, 6).Select(direction => FlagEncoder.RoadByte(game.Flags[flag].Side(direction))),
        ]);

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheCarriersOfABusyRoadWorkItAndTheSpareOneGoesHomeAsTheOriginalsPassesRunIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        var game = MillerTests.Start(scenario.Command, out var map, out _, out var passes, out var puts);
        var width = map.Size.Width;
        var last = scenario.Lines.Max(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));

        for (var pass = 0; pass <= Math.Min(last, passes - 1); pass++)
        {
            foreach (var put in puts.Where(put => put.Pass == pass))
            {
                game.LayGood(put.Flag, put.Place, TestCatalog.GoodOf(put.Good));
            }

            game.Tick();
            var line = scenario.Lines.SingleOrDefault(candidate =>
                candidate["pass"] == pass.ToString(CultureInfo.InvariantCulture)
            );
            if (line is null)
            {
                continue;
            }

            foreach (var slot in new[] { 2, 5 })
            {
                if (line.TryGetValue($"s{slot}", out var carrier))
                {
                    var expected = FlagWords.InEngineNumbers(carrier, 24, game.Flags.Count);
                    Assert.AreEqual(
                        FlagGoodsTests.Masked(expected, expected),
                        FlagGoodsTests.Masked(expected, FlagGoodsTests.Carrier(game.Settlers[slot], width)),
                        $"{name}: the carrier of slot {slot} after pass {pass}"
                    );
                }
            }

            foreach (var flag in new[] { 1, 2 })
            {
                if (line.TryGetValue($"f{flag}", out var bytes))
                {
                    Assert.AreEqual(
                        bytes,
                        FlagHex(game, flag - 1),
                        $"{name}: the flag {flag}'s status and roads after pass {pass}"
                    );
                }
            }

            Assert.AreEqual(
                line["rng"],
                string.Join(
                    ',',
                    new[] { game.RandomState.W0, game.RandomState.W1, game.RandomState.W2 }.Select(word =>
                        word.ToString("X4")
                    )
                ),
                $"{name}: the random generator after pass {pass}"
            );
        }
    }
}
