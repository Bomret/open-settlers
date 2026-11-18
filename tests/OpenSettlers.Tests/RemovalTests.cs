using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of removals.txt are the output of the emulator verb remove (tools/GhidraDump/emulate.py, ADR 0008): the original's
// click routines for the remove click (0x4615cf: a flag by 0x462153, a building by 0x4617fd) and for the road click (0x462d88,
// the take-down 0x462e1d) run on a node of a game with a castle, flags, roads, and a woodcutter's hut, in the passes of the
// original's main loop. A scenario starts with a "#" line that names its verb command. The lines after it give, for the click
// and then for each pass where something changed, the class the check 0x44b6e6 gave the node and the sound the routine asked
// for, the map bytes (flag, object, and link) that changed, the settlers (s<slot>), the flag items (f<slot>), the building items
// (b<slot>), the counts of the tables (c=), and the generator (rng=). The verb takes the dummies of the flag table and of the
// building table that a real game's start takes, so its slot numbers are the engine's plus one. The engine runs the same
// commands in the same passes and must hold the same bytes after the click and after each pass.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class RemovalTests
{
    const string Fixture = "removals.txt";

    public static IEnumerable<object[]> Scenarios =>
        EmulatorFixture
            .Load(Fixture)
            .Where(scenario => !scenario.Name.StartsWith("sweep", StringComparison.Ordinal))
            .Select(scenario => new object[] { scenario.Name });

    public static IEnumerable<object[]> Sweeps =>
        EmulatorFixture
            .Load(Fixture)
            .Where(scenario => scenario.Name.StartsWith("sweep", StringComparison.Ordinal))
            .Select(scenario => new object[] { scenario.Name });

    // The class the check 0x44b6e6 gives each node, for the classes 1 to 4 that the remove click and the road click act on
    // (a flag the click refuses, a flag it takes down, a building, a node of a road); the engine says 0 for the other nodes.
    [TestMethod]
    [DynamicData(nameof(Sweeps))]
    public void TheClickClassOfEachNodeIsTheOriginals(string name)
    {
        var scenario = EmulatorFixture.Load(Fixture).Single(candidate => candidate.Name == name);
        var parts = scenario.Command.Split(':');
        var map = RemovalGame.MapOf(parts);
        var game = RemovalGame.Started(parts, map);
        for (var pass = 0; pass < int.Parse(parts[8], CultureInfo.InvariantCulture); pass++)
        {
            game.Tick();
        }

        var expected = scenario
            .Lines[0]["classes"]
            .Split(';')
            .Select(entry => entry.Split(':'))
            .ToDictionary(entry => entry[0], entry => int.Parse(entry[1].Split('/')[0], CultureInfo.InvariantCulture));
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                Assert.AreEqual(
                    expected.GetValueOrDefault($"{x},{y}"),
                    game.Removal.ClickClass(x, y),
                    $"{name}: the class of the node {x},{y}"
                );
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void ARemovalChangesTheGameAsTheOriginalsRoutinesDo(string name) =>
        RemovalReplay.Replay(
            Fixture,
            name,
            (click, expectedClass) =>
                click == 'r' ? [CommandKind.RemoveRoad]
                : expectedClass == 2 ? [CommandKind.RemoveFlag]
                : expectedClass == 3 ? [CommandKind.RemoveBuilding]
                : [CommandKind.RemoveFlag, CommandKind.RemoveBuilding],
            line => line["sound"] == "4"
        );
}
