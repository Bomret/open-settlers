using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of mill-materials.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes of a game with a castle, flags, roads, and a finished mill, with the phase routine 0x42965c at the last phase of each cycle,
// which hands the castle's goods out to the requests. The click k@4,3 gives the castle three grains at the pass 700. The engine runs the
// same commands and must hold the same request words of the mill's flag, the same fill byte and status of the mill, the same grain
// and flour counts, and the same random generator as the lines list them (docs/rules/milling.md). The fixture's header says what the
// engine does not reproduce (the carrying of the grain out of the castle), so the passes checked stop where that begins.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class MillMaterialsTests
{
    const string Fixture = "mill-materials.txt";
    const int MillFlag = 2;
    const int Mill = 1;

    [TestMethod]
    public void TheCastleHandsGrainOutToTheMillAsTheOriginalDoes()
    {
        var scenario = EmulatorFixture.Load(Fixture).Single();
        // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS
        var parts = scenario.Command.Split(':');
        var map = RemovalGame.MapOf(parts);
        var game = RemovalGame.Started(parts, map, handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        var lines = scenario
            .Lines.Where(line => line.ContainsKey("pass"))
            .ToDictionary(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));
        var last = lines.Keys.Max();
        string? request = null;
        string? building = null;
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
            counts = line.TryGetValue("grain", out var grain) ? $"grain={grain} flour={line["flour"]}" : counts;
            rng = line.GetValueOrDefault("rng") ?? rng;
            if (request is not null)
            {
                var asked = game.Flags[MillFlag].RequestOf(RequestWord.First);
                var second = game.Flags[MillFlag].RequestOf(RequestWord.Second);
                Assert.AreEqual(
                    request,
                    Convert.ToHexString([
                        asked.Asks is { } a ? (byte)(1 << a.Class) : (byte)0,
                        asked.Rank,
                        second.Asks is { } b ? (byte)(1 << b.Class) : (byte)0,
                        second.Rank,
                    ]),
                    $"the request words of the mill's flag after pass {pass}"
                );
            }

            if (building is not null)
            {
                var mill = game.Buildings[Mill];
                var bytes = Convert.FromHexString(building);
                Assert.AreEqual(
                    Convert.ToHexString(bytes[5..6]) + Convert.ToHexString(bytes[8..10]),
                    Convert.ToHexString([(byte)BuildingEncoder.Status(mill), (byte)mill.FillLow, (byte)mill.FillHigh]),
                    $"the mill's status and fill bytes after pass {pass}"
                );
            }

            if (counts is not null)
            {
                Assert.AreEqual(
                    counts,
                    $"grain={game.CastleStock.Count(Good.Grain)} flour={game.CastleStock.Count(Good.Flour)}",
                    $"the castle's grain and flour after pass {pass}"
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
