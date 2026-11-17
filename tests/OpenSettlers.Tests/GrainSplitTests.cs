using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of grain-split.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes of a game with a castle, flags, roads, a finished pig farm, and a finished mill, with the phase routine 0x42965c at the last
// phase of each cycle. The click k@4,5 gives the castle five grains at the pass 700, when both buildings ask for grain. The engine runs
// the same commands and must hand the grains out as the lines show: the request words of both flags, the fill bytes and status of both
// buildings, the castle's grain and pig counts, and the generator (docs/rules/grain-split.md).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class GrainSplitTests
{
    const string Fixture = "grain-split.txt";
    const int MillFlag = 1;
    const int PigFarmFlag = 2;
    const int PigFarm = 1;
    const int Mill = 2;
    const int FarmFlag = 3;

    // With neither building asking, no flag has a request for grain, so the hand-out finds no taker and the grain stays in the castle. The
    // cases of one asker are pig-materials.txt (the pig farm alone) and mill-materials.txt (the mill alone).
    [TestMethod]
    public void WithNeitherAskingTheGrainStaysInTheCastle()
    {
        var parts = "construct:1:1234,5678,9ABC:0:15,5:18,6;15,9:16,6,00;16,6,2223::700:900:k@4,5".Split(':');
        var game = RemovalTests.Started(parts, RemovalTests.MapOf(parts), handsOutGoods: true);
        for (var pass = 0; pass < 700; pass++)
        {
            game.Tick();
        }

        while (game.CastleStock.Count(Good.Grain) < 5)
        {
            game.CastleStock.Add(Good.Grain);
        }

        for (var pass = 700; pass < 1000; pass++)
        {
            game.Tick();
        }

        Assert.AreEqual(5, game.CastleStock.Count(Good.Grain), "the castle's grain after 300 passes with no asker");
    }

    [TestMethod]
    public void WithTheMillAndThePigFarmBothFullTheGrainStaysInTheCastle()
    {
        var game = Both(0);
        foreach (var slot in new[] { PigFarm, Mill })
        {
            game.Buildings[slot].SetFill(FillCounter.Low, 0x77);
        }

        // The handlers rank their requests once in a cycle of passes.
        for (var pass = 700; pass < 760; pass++)
        {
            game.Tick();
        }

        foreach (var slot in new[] { MillFlag, PigFarmFlag })
        {
            Assert.AreEqual(
                0,
                game.Flags[slot].RequestOf(RequestWord.First).Rank,
                $"the request rank of the flag {slot} while its building is full"
            );
        }

        while (game.CastleStock.Count(Good.Grain) < 5)
        {
            game.CastleStock.Add(Good.Grain);
        }

        for (var pass = 760; pass < 900; pass++)
        {
            game.Tick();
        }

        Assert.AreEqual(
            5,
            game.CastleStock.Count(Good.Grain),
            "the castle's grain after 150 passes with both buildings full"
        );
    }

    // The split's state is the fills, the request ranks, and the castle's stock, and the digest holds all three: the same seed and
    // commands, twice, give the same digest, for grain from the castle's stock and for grain from a farm's flag.
    [TestMethod]
    public void TheSameRunTwiceGivesTheSameDigestWithBothBuildingsAsking()
    {
        Assert.AreEqual(
            CastleRunDigest(null),
            CastleRunDigest(null),
            "the digest of the castle's grain split, run twice"
        );
        Assert.AreEqual(FarmRunDigest(false), FarmRunDigest(false), "the digest of the farm's grain split, run twice");
    }

    // A digest that left a building's fill, or the castle's stock, out of the split's state would not tell these runs apart: the runs
    // differ in one fill byte of the mill, in one more grain in the castle's stock, or in the farm's grain.
    [TestMethod]
    public void TheDigestDiffersWhereTheSplitsStateDiffers()
    {
        var plain = CastleRunDigest(null);
        Assert.AreNotEqual(
            plain,
            CastleRunDigest(game => game.Buildings[Mill].SetFill(FillCounter.Low, game.Buildings[Mill].FillLow + 1)),
            "one fill byte of the mill"
        );
        Assert.AreNotEqual(
            plain,
            CastleRunDigest(game => game.CastleStock.Add(Good.Grain)),
            "one more grain in the castle's stock"
        );
        Assert.AreNotEqual(FarmRunDigest(false), FarmRunDigest(true), "the farm's grain on its flag or not");
    }

    static ulong CastleRunDigest(Action<Game>? change)
    {
        var game = Both(5);
        change?.Invoke(game);
        for (var pass = 700; pass < 860; pass++)
        {
            game.Tick();
        }

        return new GameDigest().Of(game);
    }

    static ulong FarmRunDigest(bool grainOnTheFlag)
    {
        var parts = RemovalTests.Load("grain-split-farm.txt").Single().Command.Split(':');
        var game = RemovalTests.Started(parts, RemovalTests.MapOf(parts), handsOutGoods: true);
        for (var pass = 0; pass < 700; pass++)
        {
            if (pass == 500 && grainOnTheFlag)
            {
                game.Goods.LayGood(FarmFlag, 0, Good.Grain.Code);
            }

            game.Tick();
        }

        return new GameDigest().Of(game);
    }

    static Game Both(int grains)
    {
        var scenario = RemovalTests.Load(Fixture).Single();
        var parts = scenario.Command.Split(':');
        var game = RemovalTests.Started(parts, RemovalTests.MapOf(parts), handsOutGoods: true);
        for (var pass = 0; pass < 700; pass++)
        {
            game.Tick();
        }

        game.ApplyCommands();
        while (game.CastleStock.Count(Good.Grain) < grains)
        {
            game.CastleStock.Add(Good.Grain);
        }

        return game;
    }

    [TestMethod]
    public void TheCastleSharesGrainBetweenTheMillAndThePigFarmAsTheOriginalDoes() =>
        Replay(
            Fixture,
            (game, pass) =>
            {
                if (pass == 700)
                {
                    game.ApplyCommands();
                    while (game.CastleStock.Count(Good.Grain) < 5)
                    {
                        game.CastleStock.Add(Good.Grain);
                    }
                }
            },
            to: 855
        );

    // The grain of a farm, put on the farm's flag three flags from the pig farm's and one from the mill's: the pig farm's request ranks 0xff
    // against the mill's 0x7f, and the farther flag takes it.
    [TestMethod]
    public void GrainFromTheFarmGoesToTheRequestThatRanksHighestAsTheOriginalDoes() =>
        Replay(
            "grain-split-farm.txt",
            (game, pass) =>
            {
                if (pass == 500)
                {
                    game.Goods.LayGood(FarmFlag, 0, Good.Grain.Code);
                }
            },
            from: 300
        );

    // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:PUTS][:castle-off@PASS]; before runs ahead of each pass's tick.
    // The checks run from the pass from to the pass to.
    static void Replay(string fixture, Action<Game, int> before, int from = 0, int to = int.MaxValue)
    {
        var scenario = RemovalTests.Load(fixture).Single();
        var parts = scenario.Command.Split(':');
        var map = RemovalTests.MapOf(parts);
        var game = RemovalTests.Started(parts, map, handsOutGoods: true);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        var lines = scenario
            .Lines.Where(line => line.ContainsKey("pass"))
            .ToDictionary(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));
        var last = lines.Keys.Max();
        var requests = new Dictionary<string, string>();
        var buildings = new Dictionary<string, string>();
        string? grains = null;
        string? pigs = null;
        string? rng = null;

        for (var pass = 0; pass <= Math.Min(Math.Min(last, passes - 1), to); pass++)
        {
            before(game, pass);
            game.Tick();
            var line = lines.GetValueOrDefault(pass) ?? [];
            foreach (var key in new[] { "f2", "f3" })
            {
                if (line.TryGetValue(key, out var flag))
                {
                    requests[key] = flag[(0x42 * 2)..(0x46 * 2)];
                }
            }

            foreach (var key in new[] { "b2", "b3" })
            {
                if (line.TryGetValue(key, out var building))
                {
                    buildings[key] = building;
                }
            }

            grains = line.GetValueOrDefault("grain") ?? grains;
            pigs = line.GetValueOrDefault("pig") ?? pigs;
            rng = line.GetValueOrDefault("rng") ?? rng;
            if (pass < from)
            {
                continue;
            }

            foreach (var (key, slot) in new[] { ("f2", MillFlag), ("f3", PigFarmFlag) })
            {
                if (requests.TryGetValue(key, out var request))
                {
                    var asked = game.Flags[slot].RequestOf(RequestWord.First);
                    var second = game.Flags[slot].RequestOf(RequestWord.Second);
                    Assert.AreEqual(
                        request,
                        Convert.ToHexString([
                            asked.Asks is { } a ? (byte)(1 << a.Class) : (byte)0,
                            asked.Rank,
                            second.Asks is { } b ? (byte)(1 << b.Class) : (byte)0,
                            second.Rank,
                        ]),
                        $"the request words of the flag {key} after pass {pass}"
                    );
                }
            }

            foreach (var (key, slot) in new[] { ("b2", PigFarm), ("b3", Mill) })
            {
                if (buildings.TryGetValue(key, out var text))
                {
                    var building = game.Buildings[slot];
                    var bytes = Convert.FromHexString(text);
                    Assert.AreEqual(
                        Convert.ToHexString(bytes[5..6]) + Convert.ToHexString(bytes[8..10]),
                        Convert.ToHexString([
                            (byte)BuildingEncoder.Status(building),
                            (byte)building.FillLow,
                            (byte)building.FillHigh,
                        ]),
                        $"the status and fill bytes of the building {key} after pass {pass}"
                    );
                }
            }

            if (grains is not null && pigs is not null)
            {
                Assert.AreEqual(
                    $"grain={grains} pig={pigs}",
                    $"grain={game.CastleStock.Count(Good.Grain)} pig={game.CastleStock.Count(Good.Pig)}",
                    $"the castle's grain and pigs after pass {pass}"
                );
            }

            if (rng is not null)
            {
                var words = game.Draws.Generator;
                Assert.AreEqual(rng, $"{words.W0:X4},{words.W1:X4},{words.W2:X4}", $"the generator after pass {pass}");
            }
        }
    }
}
