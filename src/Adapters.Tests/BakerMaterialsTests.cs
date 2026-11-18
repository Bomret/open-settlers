using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of baker-materials.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes of a game with a castle, flags, roads, and a finished bakery, with the phase routine 0x42965c at the last phase of each cycle,
// which hands the castle's goods out to the requests. The click k@5,3 gives the castle three flours at the pass 700. The engine runs the
// same commands and must hold the same request words of the bakery's flag, the same fill byte and status of the bakery, the same flour
// and bread counts, and the same random generator as the lines list them (docs/rules/baking.md). The fixture's header says what the
// engine does not reproduce (the carrying of the flour out of the castle), so the passes checked stop where that begins.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class BakerMaterialsTests
{
    const string Fixture = "baker-materials.txt";
    const int BakeryFlag = 2;
    const int Bakery = 1;

    [TestMethod]
    public void TheCastleHandsFlourOutToTheBakeryAsTheOriginalDoes()
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
                var click = scenario.Lines.Single(line => line.ContainsKey("click"));
                Assert.AreEqual("k@5,3", click["click"]);
                counts = $"flour={click["flour"]} bread={click["bread"]}";
                while (game.CastleStock.Count(Good.Flour) < 3)
                {
                    game.CastleStock.Add(Good.Flour);
                }
            }

            game.Tick();
            var line = lines.GetValueOrDefault(pass) ?? [];
            request = line.TryGetValue("f3", out var flag) ? flag[(0x42 * 2)..(0x46 * 2)] : request;
            building = line.GetValueOrDefault("b2") ?? building;
            counts = line.TryGetValue("flour", out var flour) ? $"flour={flour} bread={line["bread"]}" : counts;
            rng = line.GetValueOrDefault("rng") ?? rng;
            if (request is not null)
            {
                var asked = game.Flags[BakeryFlag].RequestOf(RequestWord.First);
                var second = game.Flags[BakeryFlag].RequestOf(RequestWord.Second);
                Assert.AreEqual(
                    request,
                    Convert.ToHexString([
                        asked.Asks is { } a ? (byte)(1 << a.Class) : (byte)0,
                        asked.Rank,
                        second.Asks is { } b ? (byte)(1 << b.Class) : (byte)0,
                        second.Rank,
                    ]),
                    $"the request words of the bakery's flag after pass {pass}"
                );
            }

            if (building is not null)
            {
                var bakery = game.Buildings[Bakery];
                var bytes = Convert.FromHexString(building);
                Assert.AreEqual(
                    Convert.ToHexString(bytes[5..6]) + Convert.ToHexString(bytes[8..10]),
                    Convert.ToHexString([
                        (byte)BuildingEncoder.Status(bakery),
                        (byte)bakery.FillLow,
                        (byte)bakery.FillHigh,
                    ]),
                    $"the bakery's status and fill bytes after pass {pass}"
                );
            }

            if (counts is not null)
            {
                Assert.AreEqual(
                    counts,
                    $"flour={game.CastleStock.Count(Good.Flour)} bread={game.CastleStock.Count(Good.Bread)}",
                    $"the castle's flour and bread after pass {pass}"
                );
            }

            if (rng is not null)
            {
                var words = game.RandomState;
                Assert.AreEqual(rng, $"{words.W0:X4},{words.W1:X4},{words.W2:X4}", $"the generator after pass {pass}");
            }
        }
    }
}
