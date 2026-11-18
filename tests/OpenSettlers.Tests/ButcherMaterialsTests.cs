using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of butcher-materials.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes of a game with a castle, flags, roads, and a finished butcher's shop, with the phase routine 0x42965c at the last phase of each
// cycle, which hands the castle's goods out to the requests. The click k@2,0 empties the castle's stock of pigs at the pass 100 and the
// click k@2,9 gives it nine at the pass 700. The engine runs the same commands and must hold the same request words of the shop's flag, the
// same fill byte and status of the shop, the same pig and meat counts, and the same random generator as the lines list them
// (docs/rules/butchering.md). The fixture's header says what the engine does not reproduce (the carrying of the pig out of the castle), so
// the passes checked stop where that begins.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class ButcherMaterialsTests
{
    const string Fixture = "butcher-materials.txt";
    const int ShopFlag = 2;
    const int Shop = 1;

    [TestMethod]
    public void TheCastleHandsPigsOutToTheShopAsTheOriginalDoes()
    {
        var scenario = RemovalTests.Load(Fixture).Single();
        // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS
        var parts = scenario.Command.Split(':');
        var map = RemovalTests.MapOf(parts);
        var game = RemovalTests.Started(parts, map, handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        // The clicks: "k@G,N" sets the castle's stock of the good G to N before the pass AT, or "P=k@G,N" before the pass P.
        var clicks = parts[10]
            .Split(';')
            .Select(text => text.Split('=') is [var pass, var click] ? (int.Parse(pass), click) : (at, text))
            .ToList();
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
            foreach (var (_, click) in clicks.Where(click => click.Item1 == pass))
            {
                game.ApplyCommands();
                var stock = click["k@".Length..].Split(',').Select(int.Parse).ToArray();
                Assert.AreEqual(TestCatalog.CodeOf(Good.Pig), stock[0], "the clicks of the fixture set the pigs");
                while (game.CastleStock.Count(Good.Pig) > stock[1])
                {
                    game.CastleStock.Take(Good.Pig);
                }

                while (game.CastleStock.Count(Good.Pig) < stock[1])
                {
                    game.CastleStock.Add(Good.Pig);
                }
            }

            game.Tick();
            var line = lines.GetValueOrDefault(pass) ?? [];
            request = line.TryGetValue("f3", out var flag) ? flag[(0x42 * 2)..(0x46 * 2)] : request;
            building = line.GetValueOrDefault("b2") ?? building;
            counts = line.TryGetValue("pig", out var pig) ? $"pig={pig} meat={line["meat"]}" : counts;
            rng = line.GetValueOrDefault("rng") ?? rng;
            if (request is not null)
            {
                var asked = game.Flags[ShopFlag].RequestOf(RequestWord.First);
                var second = game.Flags[ShopFlag].RequestOf(RequestWord.Second);
                Assert.AreEqual(
                    request,
                    Convert.ToHexString([
                        asked.Asks is { } a ? (byte)(1 << a.Class) : (byte)0,
                        asked.Rank,
                        second.Asks is { } b ? (byte)(1 << b.Class) : (byte)0,
                        second.Rank,
                    ]),
                    $"the request words of the shop's flag after pass {pass}"
                );
            }

            if (building is not null)
            {
                var shop = game.Buildings[Shop];
                var bytes = Convert.FromHexString(building);
                Assert.AreEqual(
                    Convert.ToHexString(bytes[5..6]) + Convert.ToHexString(bytes[8..10]),
                    Convert.ToHexString([(byte)BuildingEncoder.Status(shop), (byte)shop.FillLow, (byte)shop.FillHigh]),
                    $"the shop's status and fill bytes after pass {pass}"
                );
            }

            if (counts is not null)
            {
                Assert.AreEqual(
                    counts,
                    $"pig={game.CastleStock.Count(Good.Pig)} meat={game.CastleStock.Count(Good.Meat)}",
                    $"the castle's pigs and meat after pass {pass}"
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
