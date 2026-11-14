using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of construction-digger.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the original's
// passes of a game with a castle, flags, roads, and a sawmill's site (a large building) on ground that is not level, where the castle's
// digger walks out to the site and works in state 8 until the seven nodes of the site lie at its height, then goes home (docs/rules/digging.md).
// The engine runs the same commands and must hold, after every pass, the digger's 16 bytes that the last line of the fixture gave, the
// heights of the seven nodes, and at the passes of the lines the site's bytes: node for node and tick for tick. The rules on made-up tables
// are in DiggerTests.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class ConstructionDiggerTests
{
    const string Fixture = "construction-digger.txt";
    const int DiggerSlot = 13;
    const int SiteSlot = 2;

    [TestMethod]
    [DataRow("digger-sawmill")]
    [DataRow("digger-busy-node")]
    public void TheDiggerLevelsTheSiteAsTheOriginalDoes(string name)
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == name);
        // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:PUTS]
        var parts = scenario.Command.Split(':');
        // PASS,FLAG,PLACE,GOOD,DEST: a good laid on a flag before the pass, the slots the original's (one higher than the engine's).
        var puts =
            parts.Length > 11
                ? parts[11].Split(';').Select(text => text.Split(',').Select(int.Parse).ToArray()).ToList()
                : [];
        var map = RemovalTests.MapOf(parts);
        var width = map.Size.Width;
        var game = RemovalTests.Started(parts, map, handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        var linesOf = scenario.Lines.ToDictionary(line => int.Parse(line["pass"], CultureInfo.InvariantCulture));
        var (siteX, siteY) = SiteNode(parts[10]);

        string? digger = null;
        Dictionary<(int X, int Y), int>? heights = null;
        for (var pass = 0; pass < passes; pass++)
        {
            if (pass == at)
            {
                Place(game, parts[10]);
                heights = Seven(siteX, siteY).ToDictionary(node => node, node => game.Map.At(node.X, node.Y).Height);
            }

            foreach (var put in puts.Where(candidate => candidate[0] == pass))
            {
                game.Goods.LayGood(put[1] - 1, put[2], put[3], put[4] == 0 ? null : put[4] - 1);
            }

            // The castle hands out nothing the engine could lay on a flag at once: the original delivers after 130 passes, so the planks and
            // stones are taken out of the stock, as the builder's fixture does.
            while (game.CastleStock.Take(Good.Plank) || game.CastleStock.Take(Good.Stone)) { }

            game.Tick();
            var when = $"{name}: after pass {pass}";
            if (linesOf.TryGetValue(pass, out var line))
            {
                if (line.TryGetValue("s13", out var bytes))
                {
                    digger = bytes;
                }

                if (line.TryGetValue("b2", out var building))
                {
                    Assert.AreEqual(
                        building,
                        RemovalTests.BuildingBytes(game.Buildings[SiteSlot - 1], width)[..building.Length],
                        $"{when}: the site"
                    );
                }

                foreach (
                    var change in line.GetValueOrDefault("h", "").Split(';', StringSplitOptions.RemoveEmptyEntries)
                )
                {
                    // x,y:old>new
                    var (node, from, to) = Change(change);
                    Assert.AreEqual(from, heights![node], $"{when}: the height the pass changes at {node}, before");
                    heights[node] = to;
                }
            }

            if (digger is not null)
            {
                var expected = digger;
                if (game.Settlers[DiggerSlot].Activity.GoingTo is ToStorage)
                {
                    // The walk to the storage has no target word until the first flag, where the original writes its storage's flag slot (1 for
                    // the castle, the dummy before it); the engine's walk keeps the castle's flag, slot 0, in its errand.
                    expected = expected[..24] + "0000" + expected[28..];
                }

                Assert.AreEqual(expected, Settler(game.Settlers[DiggerSlot], width), $"{when}: the digger");
            }

            if (heights is not null)
            {
                CollectionAssert.AreEqual(
                    heights.Values.ToList(),
                    heights.Keys.Select(node => game.Map.At(node.X, node.Y).Height).ToList(),
                    $"{when}: the heights of the seven nodes"
                );
            }
        }
    }

    [TestMethod]
    [DataRow("sweep-site")]
    [DataRow("sweep-site-flat")]
    public void ANodeNextToALargeSiteThatWaitsForItsDiggerIsJudgedAsTheOriginalJudgesIt(string name)
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == name);
        var game = Clicked(scenario.Command.Split(':'), withSite: true);

        AssertJudgedAsTheOriginalJudges(game, scenario);
    }

    // The sawmill's site the digger has levelled, started, and left (the pass 1959 of digger-sawmill, where the digger is in the castle
    // again): the engine's evaluator gives the verdict the emulator's gives on every node.
    [TestMethod]
    public void ANodeNextToALargeSiteTheDiggerHasStartedIsJudgedAsTheOriginalJudgesIt()
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == "sweep-site-started");
        var parts = scenario.Command.Split(':');
        var game = RemovalTests.Started(parts, RemovalTests.MapOf(parts), handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var sweep = int.Parse(parts[10].Split(';')[1].Split('=')[0], CultureInfo.InvariantCulture);
        for (var pass = 0; pass < sweep; pass++)
        {
            if (pass == at)
            {
                Place(game, parts[10].Split(';')[0]);
            }

            // As TheDiggerLevelsTheSiteAsTheOriginalDoes: the hand-out is taken out of the stock.
            while (game.CastleStock.Take(Good.Plank) || game.CastleStock.Take(Good.Stone)) { }

            game.Tick();
        }

        var site = game.Buildings[SiteSlot - 1];
        Assert.IsTrue(site.Constructing, "the site is no finished building");
        Assert.AreEqual((ushort)1, site.Progress, "the digger's end has started the site");

        AssertJudgedAsTheOriginalJudges(game, scenario);
    }

    static void AssertJudgedAsTheOriginalJudges(Game game, RemovalTests.Scenario scenario)
    {
        var expected = scenario
            .Lines[0]["verdicts"]
            .Split(';')
            .Select(entry => entry.Split(':'))
            .ToDictionary(entry => entry[0], entry => entry[1]);
        var differences = new List<string>();
        for (var y = 0; y < game.Map.Size.Height; y++)
        {
            for (var x = 0; x < game.Map.Size.Width; x++)
            {
                var result = BuildSite.Evaluate(game.Tables, game.Map, x, y, 0, hasCastle: true, game.Buildings);
                var judged = result.Node == 0 ? null : $"{result.Node}/{result.Site}/{result.Height}";
                if (judged != expected.GetValueOrDefault($"{x},{y}"))
                {
                    differences.Add(
                        $"{x},{y}: the engine {judged ?? "-"}, the original {expected.GetValueOrDefault($"{x},{y}") ?? "-"}"
                    );
                }
            }
        }

        Assert.IsEmpty(differences, string.Join("; ", differences));
    }

    [TestMethod]
    public void ALargeBuildingIsRefusedWhereTheWaitingSiteWidensTheRangeOfTheHeightsAndPlacedWhereNoSiteDoes()
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == "sweep-site-flat");
        var parts = scenario.Command.Split(':');

        foreach (var (withSite, placed) in new[] { (true, false), (false, true) })
        {
            var game = Clicked(parts, withSite);
            var revision = game.Revision;
            game.Enqueue(
                new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = CommandKind.PlaceBuilding,
                    X = 12,
                    Y = 9,
                    Building = BuildingType.Sawmill,
                }
            );
            game.Tick();

            Assert.AreEqual(
                placed,
                game.Revision != revision,
                withSite ? "the waiting site refuses it" : "no site refuses it"
            );
        }
    }

    // The program's term (0x44c151 to 0x44c204) counts the site only while it is under construction and its progress word is 0: a site
    // the digger's end has started (progress 1), and a finished large building, leave the heights of the judged node alone. The rows
    // set the state by hand; the emulator's verdict on a started site is the sweep-site-started of the test above.
    [TestMethod]
    [DataRow("waits", false)]
    [DataRow("started", true)]
    [DataRow("finished", true)]
    public void ALargeBuildingInTheThirdRingWidensTheRangeOnlyWhileItWaitsForItsDigger(string state, bool placed)
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == "sweep-site-flat");
        var game = Clicked(scenario.Command.Split(':'), withSite: true);
        var site = game.Buildings[SiteSlot - 1];
        Assert.IsTrue(site.Constructing);
        Assert.AreEqual((ushort)0, site.Progress);
        switch (state)
        {
            case "started":
                site.Progress = 1;
                break;
            case "finished":
                site.Constructing = false;
                break;
        }

        var revision = game.Revision;
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = 12,
                Y = 9,
                Building = BuildingType.Sawmill,
            }
        );
        game.Tick();

        Assert.AreEqual(placed, game.Revision != revision, $"the {state} site");
    }

    // The game of the scenario's command at the pass after its clicks: the site's click only where `withSite`, the heights of the clicks
    // l@x,y,H (the verb's click sets the node and its six neighbours to H), and none of the sweep's clicks v@.
    static Game Clicked(string[] parts, bool withSite)
    {
        // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS
        var game = RemovalTests.Started(parts, RemovalTests.MapOf(parts), handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        for (var pass = 0; pass < int.Parse(parts[9], CultureInfo.InvariantCulture); pass++)
        {
            if (pass == at)
            {
                foreach (
                    var click in parts[10].Split(';').Where(click => click[0] != 'v' && (withSite || click[0] != 'b'))
                )
                {
                    if (click[0] == 'l')
                    {
                        var level = click[2..]
                            .Split(',')
                            .Select(number => int.Parse(number, CultureInfo.InvariantCulture))
                            .ToArray();
                        foreach (var (x, y) in Seven(level[0], level[1]))
                        {
                            game.Map.At(x, y).Height = level[2];
                        }
                    }
                    else
                    {
                        Place(game, click);
                    }
                }
            }

            game.Tick();
        }

        return game;
    }

    static (int X, int Y) SiteNode(string click)
    {
        var node = click[2..].Split(',');

        return (int.Parse(node[0], CultureInfo.InvariantCulture), int.Parse(node[1], CultureInfo.InvariantCulture));
    }

    // The node and its six neighbours.
    static IEnumerable<(int X, int Y)> Seven(int x, int y) =>
        new[] { (0, 0) }.Concat(Direction.Steps).Select(step => (x + step.Item1, y + step.Item2));

    static void Place(Game game, string click)
    {
        game.ApplyCommands();
        var node = click[2..].Split(',').Select(number => int.Parse(number, CultureInfo.InvariantCulture)).ToArray();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = node[0],
                Y = node[1],
                Building = new BuildingType { Code = node[2] },
            }
        );
        game.ApplyCommands();
    }

    static ((int X, int Y) Node, int From, int To) Change(string text)
    {
        var halves = text.Split(':');
        var node = halves[0].Split(',').Select(number => int.Parse(number, CultureInfo.InvariantCulture)).ToArray();
        var heights = halves[1].Split('>').Select(number => int.Parse(number, CultureInfo.InvariantCulture)).ToArray();

        return ((node[0], node[1]), heights[0], heights[1]);
    }

    // The settler's 16 bytes as the original's item holds them. The slots the item names (a flag or a building) are one higher than the
    // engine's, the dummies of the original's tables; the digger's work holds the site's height and a node index in that word, no slot.
    static string Settler(Settler settler, int width)
    {
        var bytes = Convert.FromHexString(WorkerWalkTests.Hex(settler, width));
        if (
            settler.Activity
            is Leaving
                or Walks { Errand: not ToStorage }
                or WaitsOutStep { Then: Walks { Errand: not ToStorage } }
        )
        {
            var target = BitConverter.ToUInt16(bytes, 12) + 1;
            bytes[12] = (byte)target;
            bytes[13] = (byte)(target >> 8);
        }

        return Convert.ToHexString(bytes);
    }
}
