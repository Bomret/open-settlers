using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of pig-materials.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes of a game with a castle, flags, roads, and a finished pig farm, with the phase routine 0x42965c at the last phase of each cycle,
// which hands the castle's goods out to the requests. The click k@4,3 gives the castle three grains at the pass 700. The engine runs the
// same commands and must hold the same request words of the pig farm's flag, the same fill bytes and status of the pig farm, the same
// grain and pig counts, and the same random generator as the lines list them (docs/rules/pig-farming.md). The fixture's header says what
// the engine does not reproduce (the carrying of the grain out of the castle), so the passes checked stop where that begins.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class PigFarmMaterialsTests
{
    const string Fixture = "pig-materials.txt";
    const int PigFarmFlag = 2;
    const int PigFarm = 1;

    [TestMethod]
    public void TheCastleHandsGrainOutToThePigFarmAsTheOriginalDoes()
    {
        var scenario = RemovalTests.Load(Fixture).Single();
        // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:castle-off@PASS]
        var parts = scenario.Command.Split(':');
        var map = RemovalTests.MapOf(parts);
        var game = RemovalTests.Started(parts, map, handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        var lines = scenario
            .Lines.Where(line => line.ContainsKey("pass"))
            .ToDictionary(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));
        var last = lines.Keys.Max();
        string? request = null;
        string? building = null;
        string? grains = null;
        string? pigs = null;
        string? counts = null;
        string? rng = null;

        for (var pass = 0; pass <= Math.Min(last, passes - 1); pass++)
        {
            if (pass == at)
            {
                game.ApplyCommands();
                Assert.AreEqual("k@4,3", scenario.Lines.Single(line => line.ContainsKey("click"))["click"]);
                while (game.CastleStock.Count(Good.Grain) < 3)
                {
                    game.CastleStock.Add(Good.Grain);
                }
            }

            game.Tick();
            var line = lines.GetValueOrDefault(pass) ?? [];
            request = line.TryGetValue("f3", out var flag) ? flag[(0x42 * 2)..(0x46 * 2)] : request;
            building = line.GetValueOrDefault("b2") ?? building;
            grains = line.GetValueOrDefault("grain") ?? grains;
            pigs = line.GetValueOrDefault("pig") ?? pigs;
            counts = grains is null || pigs is null ? null : $"grain={grains} pig={pigs}";
            rng = line.GetValueOrDefault("rng") ?? rng;
            if (request is not null)
            {
                var asked = game.Flags[PigFarmFlag].RequestOf(RequestWord.First);
                var second = game.Flags[PigFarmFlag].RequestOf(RequestWord.Second);
                Assert.AreEqual(
                    request,
                    Convert.ToHexString([
                        asked.Asks is { } a ? (byte)(1 << a.Class) : (byte)0,
                        asked.Rank,
                        second.Asks is { } b ? (byte)(1 << b.Class) : (byte)0,
                        second.Rank,
                    ]),
                    $"the request words of the pig farm's flag after pass {pass}"
                );
            }

            if (building is not null)
            {
                var farm = game.Buildings[PigFarm];
                var bytes = Convert.FromHexString(building);
                Assert.AreEqual(
                    Convert.ToHexString(bytes[5..6]) + Convert.ToHexString(bytes[8..10]),
                    Convert.ToHexString([(byte)BuildingEncoder.Status(farm), (byte)farm.FillLow, (byte)farm.FillHigh]),
                    $"the pig farm's status and fill bytes after pass {pass}"
                );
            }

            if (counts is not null)
            {
                Assert.AreEqual(
                    counts,
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
