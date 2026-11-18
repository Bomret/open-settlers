using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of settler-supply.txt are the output of the emulator verb supply (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes over a castle game set up as a free game's start leaves the player block, with the settler rate and,
// where the command gives it, the castle's stock of swords and shields (goods 25 and 26) written by hand, each command in a launch of
// its own. Each scenario starts
// with a "#" line that names its verb command. Its start line gives the period and the timer after the placement; each later
// line gives, for a pass where a settler was made or the knights or the stock changed, the 16 bytes of each new settler
// (s<slot>=), the countdown, the knight accumulator, and the knights owed (timer=), the stock of goods 25 and 26 (goods=), and the
// random generator's words (rng=). The engine runs the same game with the settler supply on and must hold the same values
// after each pass: a pass without a line makes no settler and leaves the knights and the stock as they were.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class SettlerSupplyRecordingTests
{
    const string Fixture = "settler-supply.txt";

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
        foreach (var line in File.ReadAllLines(Fixture))
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
                .Lines.Add(
                    line.Split(' ').Select(field => field.Split('=')).ToDictionary(pair => pair[0], pair => pair[1])
                );
        }

        return scenarios;
    }

    static string Timer(SettlerSupply supply) =>
        $"{supply.Countdown:X4},{supply.KnightAccumulator:X4},{supply.KnightsOwed:X4}";

    static string Goods(Game game) => $"{game.CastleStock.Count(Good.Sword)},{game.CastleStock.Count(Good.Shield)}";

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheSupplyMakesTheOriginalsSettlersAndKnights(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        // supply:N:SEED:L:cx,cy:RATE:PASSES[:G,N;G,N...]
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(
            TestProgram.Tables(),
            new MapSeed
            {
                W0 = seed[0],
                W1 = seed[1],
                W2 = seed[2],
            },
            size!
        );
        var game = GameFactory.Create(
            TestProgram.Tables(),
            map,
            int.Parse(parts[3], CultureInfo.InvariantCulture),
            settlerSupply: true,
            settlerRate: int.Parse(parts[5], CultureInfo.InvariantCulture)
        );
        var castle = parts[4].Split(',').Select(int.Parse).ToArray();
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = castle[0],
                Y = castle[1],
            }
        );
        game.ApplyCommands();
        if (parts.Length > 7)
        {
            foreach (var good in parts[7].Split(';').Select(text => text.Split(',').Select(int.Parse).ToArray()))
            {
                var kind = new Good { Code = good[0] };
                while (game.CastleStock.Count(kind) > good[1])
                {
                    game.CastleStock.Take(kind);
                }
            }
        }

        var start = scenario.Lines[0];
        var supply = game.Supply!;
        Assert.AreEqual(start["period"], $"{supply.Period:X4}", $"{name}: the period");
        Assert.AreEqual(start["timer"], Timer(supply), $"{name}: the timer at the start");
        Assert.AreEqual(start["goods"], Goods(game), $"{name}: the stock at the start");

        var lines = scenario.Lines.Skip(1).ToDictionary(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));
        var passes = int.Parse(parts[6], CultureInfo.InvariantCulture);
        var knights = start["timer"][5..];
        var goods = start["goods"];
        for (var pass = 0; pass < passes; pass++)
        {
            var count = game.Settlers.Count;
            game.Tick();
            if (!lines.TryGetValue(pass, out var line))
            {
                Assert.AreEqual(count, game.Settlers.Count, $"{name}: no settler in pass {pass}");
                Assert.AreEqual(knights, Timer(supply)[5..], $"{name}: the knights after pass {pass}");
                Assert.AreEqual(goods, Goods(game), $"{name}: the stock after pass {pass}");
                continue;
            }

            var made = line.Keys.Where(key => key.StartsWith('s')).ToList();
            Assert.AreEqual(count + made.Count, game.Settlers.Count, $"{name}: the settlers made in pass {pass}");
            foreach (var key in made)
            {
                var slot = int.Parse(key[1..], CultureInfo.InvariantCulture);
                Assert.AreEqual(
                    line[key],
                    WorkerBytes.Hex(game.Settlers[slot], map.Size.Width),
                    $"{name}: the new settler {slot} after pass {pass}"
                );
            }

            Assert.AreEqual(line["timer"], Timer(supply), $"{name}: the timer after pass {pass}");
            Assert.AreEqual(line["goods"], Goods(game), $"{name}: the stock after pass {pass}");
            var words = game.Draws.Generator;
            Assert.AreEqual(
                line["rng"],
                $"{words.W0:X4},{words.W1:X4},{words.W2:X4}",
                $"{name}: the generator after pass {pass}"
            );
            knights = line["timer"][5..];
            goods = line["goods"];
        }
    }
}
