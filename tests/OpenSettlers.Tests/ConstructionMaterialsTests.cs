using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of construction-materials.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes of a game with a castle, flags, roads, and building sites, with the phase routine 0x42965c at the last phase of each
// cycle, which hands the castle's goods out to the requests. The engine runs the same commands and must hold the same request words,
// the same goods on site and on the way, the same stock, and the same random generator as the lines list them
// (docs/rules/construction-materials.md). The fixture's header says what the engine does not reproduce (the carrying of the good out of
// the castle, the builder), so the passes checked stop where those begin. The rules on made-up tables are in SitePassTests,
// RequestSearchTests, and StorageDistributionTests.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class ConstructionMaterialsTests
{
    private const string Fixture = "construction-materials.txt";

    [TestMethod]
    [DataRow("two-sites")]
    [DataRow("stock-empty")]
    public void TheRequestsTheGoodsOnSiteAndOnTheWayAndTheStockMatchTheEmulator(string name)
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == name);
        // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS
        var parts = scenario.Command.Split(':');
        var map = RemovalTests.MapOf(parts);
        var game = RemovalTests.Started(parts, map, handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        var lines = scenario
            .Lines.Where(line => line.ContainsKey("pass"))
            .ToDictionary(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));
        var clickLines = scenario.Lines.Where(line => line.ContainsKey("click")).ToList();
        var flags = new Dictionary<int, string>();
        var buildings = new Dictionary<int, string>();
        string? stock = null;
        string? rng = null;

        void Take(Dictionary<string, string> line)
        {
            foreach (var (key, value) in line.Where(pair => pair.Key.Length > 0))
            {
                switch (key[0], key.Length > 1 && char.IsDigit(key[1]))
                {
                    case ('f', true):
                        flags[int.Parse(key[1..], CultureInfo.InvariantCulture)] = value;
                        break;
                    case ('b', true):
                        buildings[int.Parse(key[1..], CultureInfo.InvariantCulture)] = value;
                        break;
                    case ('s', false) when key == "st":
                        stock = value;
                        break;
                    case ('r', false) when key == "rng":
                        rng = value;
                        break;
                }
            }
        }

        for (var pass = 0; pass < passes; pass++)
        {
            if (pass == at)
            {
                game.ApplyCommands();
                var clicks = parts[10].Split(';');
                for (var index = 0; index < clicks.Length; index++)
                {
                    var click = clicks[index];
                    var node = click[2..].Split(',').Select(number => int.Parse(number, CultureInfo.InvariantCulture));
                    Click(game, click[0], node.ToArray());
                    Take(clickLines[index]);
                    Check(game, $"{name}: after the click {click}", flags, buildings, stock, rng);
                }
            }

            game.Tick();
            Take(lines.GetValueOrDefault(pass) ?? []);
            Check(game, $"{name}: after pass {pass}", flags, buildings, stock, rng);
        }
    }

    // b@x,y,T places the site of the type T, l@x,y,H levels the node and its six neighbours to the height H (the digger's work).
    private static void Click(Game game, char kind, int[] node)
    {
        if (kind == 'b')
        {
            game.Enqueue(
                new Command(game.TickCount, 0, CommandKind.PlaceBuilding, node[0], node[1], Building: new BuildingType(node[2]))
            );
            game.ApplyCommands();

            return;
        }

        for (var direction = -1; direction < Direction.Count; direction++)
        {
            var (dx, dy) = direction < 0 ? (0, 0) : Direction.Steps[direction];
            game.Map.At(node[0] + dx, node[1] + dy).Height = node[2];
        }
    }

    private static void Check(
        Game game,
        string when,
        Dictionary<int, string> flags,
        Dictionary<int, string> buildings,
        string? stock,
        string? rng
    )
    {
        foreach (var (slot, expected) in flags)
        {
            if (slot - 1 >= game.Flags.Count)
            {
                continue;
            }

            var bytes = Convert.FromHexString(expected);
            var flag = game.Flags[slot - 1];
            Assert.AreEqual(
                Convert.ToHexString(bytes[0x42..0x46]),
                Convert.ToHexString(
                    [
                        flag.RequestOf(RequestWord.First).Classes,
                        flag.RequestOf(RequestWord.First).Rank,
                        flag.RequestOf(RequestWord.Second).Classes,
                        flag.RequestOf(RequestWord.Second).Rank,
                    ]
                ),
                $"{when}: the request words of flag {slot}"
            );
        }

        foreach (var (slot, expected) in buildings)
        {
            // Slot 0 is the dummy of the building table, slot 1 the castle.
            if (slot < 2 || slot - 1 >= game.Buildings.Count)
            {
                continue;
            }

            var bytes = Convert.FromHexString(expected);
            var building = game.Buildings[slot - 1];
            Assert.AreEqual(Held(bytes[8]), Held(building.FillLow), $"{when}: the planks of site {slot}, on site and on the way");
            Assert.AreEqual(Held(bytes[9]), Held(building.FillHigh), $"{when}: the stones of site {slot}, on site and on the way");
            Assert.AreEqual(
                bytes[12] | bytes[13] << 8,
                (int)building.Progress,
                $"{when}: the progress word of site {slot}"
            );
        }

        if (stock is not null)
        {
            var bytes = Convert.FromHexString(stock);
            foreach (var good in new[] { Good.Log, Good.Plank, Good.Stone })
            {
                Assert.AreEqual(
                    bytes[4 + 2 * good.Code] | bytes[5 + 2 * good.Code] << 8,
                    game.CastleStock.Count(good),
                    $"{when}: the castle's count of the good {good}"
                );
            }
        }

        if (rng is not null)
        {
            var words = game.Draws.Generator;
            Assert.AreEqual(rng, $"{words.W0:X4},{words.W1:X4},{words.W2:X4}", $"{when}: the generator");
        }
    }

    // The goods a fill byte holds: the high nibble is on site, the low nibble on the way.
    private static int Held(int fill) => (fill >> 4) + (fill & 0xf);
}
