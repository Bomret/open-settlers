using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of construction-removal.txt are the output of the emulator verb remove (tools/GhidraDump/emulate.py, ADR 0008): the original's
// remove click (0x4615cf, the building's take-down 0x4617fd with the release of its goods 0x4312ed) on a building site that has a worker at work
// or on the way, and goods on site, on a carrier, and waiting at a flag (docs/rules/removal.md, "Taking a building site down"). The engine
// places the site and makes the same click in the same passes and must hold the bytes of the lines after each click and each pass.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class ConstructionRemovalTests
{
    private const string Fixture = "construction-removal.txt";

    [TestMethod]
    [DataRow("builder-at-work")]
    [DataRow("builder-on-the-way")]
    [DataRow("digger-at-work")]
    [DataRow("digger-on-the-way")]
    [DataRow("site-unreached")]
    [DataRow("goods-waiting")]
    [DataRow("goods-carried")]
    [DataRow("goods-at-the-flag")]
    public void RemovingABuildingSiteChangesTheGameAsTheOriginalDoes(string name) => Replay(name, construct: false);

    // A site that holds goods when it is taken down: the castle has handed out planks, which the site counts as on the way (the low
    // nibble of its byte 8), and one has arrived, which it counts as on site (the high nibble). The click clears both counts with the
    // site; the fixture's header says why only the site's bytes, the builder, the map, and the generator are compared.
    [TestMethod]
    [DataRow("fill-on-the-way")]
    [DataRow("fill-on-site")]
    public void RemovingASiteThatHoldsGoodsClearsItsCountsAsTheOriginalDoes(string name) =>
        Replay(name, construct: true);

    private static void Replay(string name, bool construct) =>
        RemovalTests.Replay(
            Fixture,
            name,
            (click, _) => click == 'b' ? [CommandKind.PlaceBuilding] : [CommandKind.RemoveBuilding],
            line => line["sound"] == "4",
            hexOf: Settler,
            construct: construct
        );

    // The program's ring-3 term (0x44c151 to 0x44c204) reads the word at 14 of every large building with the construction bit and the
    // progress 0, and a large site taken down still has both: the word then holds the clock of its take-down, whose low byte widens the
    // range of the heights where the target height widened it before. One sweep after the take-down is the check: Removal rewrites the
    // clock at every burn step, but the emulator's sweeps at five later passes of the burn (low bytes of the clock 0x18 to 0x40) and
    // with the take-down at passes 96, 98, and 100 (low bytes 0x00 to 0x20) list the same verdicts as this one: the low byte
    // of the clock changes no verdict in this terrain, so a later sweep would pin nothing this one does not.
    [TestMethod]
    public void ALargeSiteTakenDownWidensTheHeightsOfItsRingByTheClockItHoldsAsTheOriginalDoes()
    {
        var scenario = RemovalTests.Load(Fixture).Single(candidate => candidate.Name == "sweep-removed-site");
        // remove:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:b@x,y,T;N=f@x,y;N=v@0,0
        var parts = scenario.Command.Split(':');
        var game = RemovalTests.Started(parts, RemovalTests.MapOf(parts));
        var clicks = parts[10].Split(';');
        var sweepAt = int.Parse(clicks[2].Split('=')[0], CultureInfo.InvariantCulture);
        var removeAt = int.Parse(clicks[1].Split('=')[0], CultureInfo.InvariantCulture);
        var site = clicks[0]
            [2..]
            .Split(',')
            .Select(number => int.Parse(number, CultureInfo.InvariantCulture))
            .ToArray();
        for (var pass = 0; pass < sweepAt; pass++)
        {
            if (pass == int.Parse(parts[8], CultureInfo.InvariantCulture))
            {
                game.ApplyCommands();
                game.Enqueue(
                    new Command(
                        game.TickCount,
                        0,
                        CommandKind.PlaceBuilding,
                        site[0],
                        site[1],
                        Building: new BuildingType(site[2])
                    )
                );
                game.ApplyCommands();
            }

            if (pass == removeAt)
            {
                game.Enqueue(new Command(game.TickCount, 0, CommandKind.RemoveBuilding, site[0], site[1]));
                game.ApplyCommands();
            }

            game.Tick();
        }

        var expected = scenario
            .Lines[0]["verdicts"]
            .Split(';')
            .Select(entry => entry.Split(':'))
            // The classes 1 to 4 are the remove click's (Removal.ClickClass); the evaluator of a building site judges the nodes 5 to 7.
            .Where(entry => entry[1][0] is '5' or '6' or '7')
            .ToDictionary(entry => entry[0], entry => entry[1]);
        var differences = new List<string>();
        for (var y = 0; y < game.Map.Size.Height; y++)
        {
            for (var x = 0; x < game.Map.Size.Width; x++)
            {
                var result = BuildSite.Evaluate(game.Tables, game.Map, x, y, 0, hasCastle: true, game.Buildings);
                var judged = result.Node < 5 ? null : $"{result.Node}/{result.Site}/{result.Height}";
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

    // The settler's 16 bytes as the builder's fixture reads them, and the site's flag the builder's walk home keeps in the word at 12 (it
    // leaves the site with the word the work held, which numbers the flag one higher than the engine's table).
    private static string Settler(OpenSettlers.Domain.Settler settler, int width)
    {
        var bytes = Convert.FromHexString(ConstructionBuilderTests.Settler(settler, width));
        if (settler.Job == Job.Builder && bytes[10] is 0x19 or 0x1c)
        {
            var target = BitConverter.ToUInt16(bytes, 12) + 1;
            bytes[12] = (byte)target;
            bytes[13] = (byte)(target >> 8);
        }

        return Convert.ToHexString(bytes);
    }
}
