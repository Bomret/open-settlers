using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of construction-builder.txt are the output of the emulator verb construct (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes of a game with a castle, flags, roads, and building sites, in which the castle's builder walks out to a site, takes its
// planks, and builds it, and the finished forester's worker, a settler without a job, walks out to it (docs/rules/construction.md). The
// engine runs the same commands, with the goods a scenario lays on a flag by hand, and must hold the same bytes at each checkpoint: the
// 16 bytes of the builder, the worker, and a carrier (with the draw of each blow), the site's 18 bytes, its flag's request bytes, and the
// generator. The fixture's header says what the test lends the engine (the delivery of the planks, which the engine's hand-out does at
// once). The rules on made-up tables are in BuilderTests.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class ConstructionBuilderTests
{
    private const string Fixture = "construction-builder.txt";

    [TestMethod]
    [DataRow("builder-forester")]
    [DataRow("builder-two-sites")]
    [DataRow("builder-no-hammer")]
    [DataRow("builder-no-jobless")]
    [DataRow("builder-waits-at-the-door")]
    public void TheBuilderBuildsTheSiteAsTheOriginalDoes(string name)
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == name);
        // construct:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:PUTS]
        var parts = scenario.Command.Split(':');
        var map = RemovalTests.MapOf(parts);
        var width = map.Size.Width;
        var game = RemovalTests.Started(parts, map, handsOutGoods: true);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);
        var linesOf = scenario
            .Lines.GroupBy(line => int.Parse(line["pass"], CultureInfo.InvariantCulture))
            .ToDictionary(group => group.Key, group => group.ToList());

        // A click written PASS=click runs before that pass instead of before AT, as the verb runs it.
        var clicks = parts[10]
            .Split(';')
            .Select(spec =>
                spec.Contains('=')
                    ? (
                        Pass: int.Parse(spec[..spec.IndexOf('=')], CultureInfo.InvariantCulture),
                        Click: spec[(spec.IndexOf('=') + 1)..]
                    )
                    : (Pass: at, Click: spec)
            )
            .ToList();
        // PASS,FLAG,PLACE,GOOD,DEST: a good laid by hand on a place of the flag of that slot before that pass, as RemovalTests lays it.
        var puts =
            parts.Length > 11
                ? parts[11]
                    .Split(';')
                    .Select(text =>
                        text.Split(',').Select(number => int.Parse(number, CultureInfo.InvariantCulture)).ToArray()
                    )
                    .ToList()
                : [];

        for (var pass = 0; pass < passes; pass++)
        {
            if (pass == at)
            {
                game.ApplyCommands();
            }

            foreach (var (_, click) in clicks.Where(click => click.Pass == pass))
            {
                var node = click[2..]
                    .Split(',')
                    .Select(number => int.Parse(number, CultureInfo.InvariantCulture))
                    .ToArray();
                if (click[0] == 'k')
                {
                    // k@G,N: the castle's stock of the good G written by hand to N.
                    var good = new Good(node[0]);
                    while (game.CastleStock.Take(good)) { }

                    for (var i = 0; i < node[1]; i++)
                    {
                        game.CastleStock.Add(good);
                    }

                    continue;
                }

                game.Enqueue(
                    new Command(
                        game.TickCount,
                        0,
                        CommandKind.PlaceBuilding,
                        node[0],
                        node[1],
                        Building: new BuildingType(node[2])
                    )
                );
                game.ApplyCommands();
            }

            foreach (var put in puts.Where(candidate => candidate[0] == pass))
            {
                game.Goods.LayGood(
                    put[1] - 1,
                    put[2],
                    put[3] & 0x1f,
                    put[4] == 0 ? null : put[4] - 1,
                    put[3] >> 5 == 0 ? null : (put[3] >> 5) - 1
                );
            }

            // The castle hands out nothing the engine could lay on a flag at once: the original delivers after 130 passes, so the fixture
            // names the passes of its deliveries. The hand-out still takes its draw from the generator each cycle.
            while (game.CastleStock.Take(Good.Plank) || game.CastleStock.Take(Good.Stone)) { }

            game.Tick();
            foreach (var line in linesOf.GetValueOrDefault(pass) ?? [])
            {
                Apply(game, line);
                Check(game, line, width, $"{name}: after pass {pass}");
            }
        }
    }

    // A line "fill<slot>+=NN" adds to byte 8 of the building in that slot: a good the original has handed out or delivered.
    internal static void Apply(Game game, Dictionary<string, string> line)
    {
        foreach (var (key, value) in line.Where(pair => pair.Key.StartsWith("fill", StringComparison.Ordinal)))
        {
            var building = game.Buildings[int.Parse(key[4..^1], CultureInfo.InvariantCulture) - 1];
            building.SetFill(FillCounter.Low, building.FillLow + Convert.ToInt32(value, 16));
        }
    }

    private static void Check(Game game, Dictionary<string, string> line, int width, string when)
    {
        foreach (var (key, value) in line)
        {
            switch (key[0], key.Length > 1 && char.IsDigit(key[1]))
            {
                case ('s', true):
                    var slot = int.Parse(key[1..], CultureInfo.InvariantCulture);
                    var settler = game.Settlers[slot];
                    var expected = value;
                    if (settler.Activity.GoingTo is ToStorage)
                    {
                        // The walk to the storage has no target word until the first flag, where the original writes its storage's flag
                        // slot (1 for the castle, the dummy before it); the engine's walk keeps the castle's flag, slot 0, in its errand.
                        expected = value[..24] + "0000" + value[28..];
                    }

                    if (settler.Job == Job.Carrier && value[20..24] == "0300")
                    {
                        // A carrier that walks his road with no good keeps in the word at 12 the low word of the pointer to the flag item he
                        // waited for (0x46 for each slot); the engine keeps no such word.
                        expected = value[..24] + Settler(settler, width)[24..28] + value[28..];
                    }

                    Assert.AreEqual(expected, Settler(settler, width), $"{when}: the settler {slot}");
                    break;
                case ('b', true):
                    var site = int.Parse(key[1..], CultureInfo.InvariantCulture);
                    Assert.AreEqual(
                        value,
                        RemovalTests.BuildingBytes(game.Buildings[site - 1], width)[..value.Length],
                        $"{when}: the building {site}"
                    );
                    break;
                case ('f', true):
                    var flag = game.Flags[int.Parse(key[1..], CultureInfo.InvariantCulture) - 1];
                    Assert.AreEqual(
                        value,
                        Convert.ToHexString([
                            ClassByte(flag.RequestOf(RequestWord.First)),
                            flag.RequestOf(RequestWord.First).Rank,
                            ClassByte(flag.RequestOf(RequestWord.Second)),
                            flag.RequestOf(RequestWord.Second).Rank,
                        ]),
                        $"{when}: the request words of flag {key[1..]}"
                    );
                    break;
                case ('r', false) when key == "rng":
                    var words = game.Draws.Generator;
                    Assert.AreEqual(value, $"{words.W0:X4},{words.W1:X4},{words.W2:X4}", $"{when}: the generator");
                    break;
            }
        }
    }

    // The settler's 16 bytes as the original's item holds them. The slots the item names (a flag or a building) are one higher than the
    // engine's, the dummies of the original's tables.
    internal static string Settler(Settler settler, int width)
    {
        var bytes = Convert.FromHexString(WorkerWalkTests.Hex(settler, width));
        var slot = settler.Activity switch
        {
            Builds => true,
            Leaving or Walks { Errand: not ToStorage } => true,
            WaitsOutStep { Then: Walks { Errand: not ToStorage } } => true,
            _ => false,
        };
        if (slot)
        {
            var target = BitConverter.ToUInt16(bytes, 12) + 1;
            bytes[12] = (byte)target;
            bytes[13] = (byte)(target >> 8);
        }

        return Convert.ToHexString(bytes);
    }

    // The emulator's class byte of a request word: bit k set asks for the class k.
    private static byte ClassByte(Request request) => request.Asks is { } asked ? (byte)(1 << asked.Class) : (byte)0;
}
