using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of home-walks.txt are the output of the emulator verb home (tools/GhidraDump/emulate.py, ADR 0008): the
// original's passes over the item tables on a game with a castle, flags, and roads and no building, where before one pass a
// settler is sent home as the teardown of a building sends the settlers in it (0x461b77: state 0x1c). Each scenario starts
// with a "#" line that names its verb command; the lines after it give, for the passes where they changed, the 16 bytes of
// the settler (s=), the random generator's words (rng=), and the word of the castle's list of waiting settlers that belongs to
// the settler's job (w=), and the bytes of a second settler the walk meets (o=, a carrier on the road). The game takes the building table's dummy first, as a real game's start does, so the castle is
// building slot 1. The engine runs the same commands, sends the same settler home in the same pass, and must hold the same
// bytes after each pass until the last: the settler is in state 1 in the castle and its slot is in the list. That tests the
// claim of the node (0x434946), the search for the nearest own flag with a road (0x4353ef) with its random fallback, the free
// walk, the arrival (0x4385ab), the walk to the storage (state 2 with the mode 0xfe, 0x4398ea with the pick 0x45d09b), the
// entry into the castle (0x43c7c2), the door (state 4, 0x43c96b), and the job's entry into the storage (0x43cd06).
[TestClass]
public sealed class HomeWalkTests
{
    private const string Fixture = "home-walks.txt";

    // The scenario of a settler on a flag no road leads from to the castle: it looks for another flag (state 0x19, mode 1).
    private const string NoStorage = "no-storage-road";

    private sealed record Scenario(
        string Name,
        string Command,
        List<(int Pass, string? Settler, string? Rng, string? Waiting, string? Other)> Lines
    );

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    private static List<Scenario> Load()
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(Fixture))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var parts = line[2..].Split(' ', 2);
                scenarios.Add(new Scenario(parts[0], parts[1], []));
                continue;
            }

            var fields = line.Split(' ')
                .Select(field => field.Split('='))
                .ToDictionary(pair => pair[0], pair => pair[1]);
            scenarios[^1]
                .Lines.Add(
                    (
                        int.Parse(fields["pass"], CultureInfo.InvariantCulture),
                        fields.GetValueOrDefault("s"),
                        fields.GetValueOrDefault("rng"),
                        fields.GetValueOrDefault("w"),
                        fields.GetValueOrDefault("o")
                    )
                );
        }

        return scenarios;
    }

    private static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    private static Game CastleWithRoad()
    {
        MapSize.TryFromValue(1, out var size, out _);
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(0x1234, 0x5678, 0x9ABC), size!);
        var game = GameFactory.Create(TestProgram.Tables(), map, 0);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 18, 20));
        game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, 16, 20));
        game.Enqueue(new Command(0, 0, CommandKind.BuildRoad, 19, 21, [3, 4, 3]));

        return game;
    }

    // A settler claims the node it stands on only where no other settler stands there (0x434946): of two sent home from the
    // castle's node the second waits in state 0x1c until the first has left the node.
    [TestMethod]
    public void ASecondSettlerSentHomeFromTheSameNodeWaitsForTheFirstToLeave()
    {
        var game = CastleWithRoad();
        for (var pass = 0; pass < 5; pass++)
        {
            game.Tick();
        }

        game.Settlers[12].Activity = new ClaimsHomeNode();
        game.Settlers[13].Activity = new ClaimsHomeNode();
        var firstClaimed = false;
        for (var pass = 0; pass < 120 && game.Settlers[13].Activity is ClaimsHomeNode; pass++)
        {
            game.Tick();
            if (game.Settlers[12].Activity is not ClaimsHomeNode && !firstClaimed)
            {
                firstClaimed = true;
                Assert.IsInstanceOfType<ClaimsHomeNode>(
                    game.Settlers[13].Activity,
                    "the node is taken while the first stands on it"
                );
            }
        }

        Assert.IsTrue(firstClaimed);
        Assert.IsNotInstanceOfType<ClaimsHomeNode>(
            game.Settlers[13].Activity,
            "the second claims the node once it is free"
        );
        Assert.AreNotEqual(
            (game.Settlers[12].X, game.Settlers[12].Y),
            (game.Settlers[13].X, game.Settlers[13].Y),
            "the first has left the node"
        );
    }

    // The jobs 22 to 26 (the knights) take state 0x35 where the others take the free walk (0x435821); that state is not
    // ported, so a knight sent home waits there.
    [TestMethod]
    public void AKnightSentHomeTakesState0x35()
    {
        var game = CastleWithRoad();
        for (var pass = 0; pass < 5; pass++)
        {
            game.Tick();
        }

        var knight = game.Settlers.First(settler => settler.Job == new Job(22));
        knight.Activity = new ClaimsHomeNode();
        for (var pass = 0; pass < 80; pass++)
        {
            game.Tick();
        }

        Assert.IsInstanceOfType<KnightWalks>(knight.Activity);
        Assert.IsInstanceOfType<Home>(
            ((KnightWalks)knight.Activity).Trip.Purpose,
            "the vector of the walk home is set as for the others"
        );
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void ASettlerSentHomeWalksAsTheOriginalsPassesMoveIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        // home:N:SEED:L:cx,cy:FLAGS:ROADS:SLOT[@x,y]:AT:PASSES[:STATE,MODE[:WATCH]]
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = GameFactory.Create(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
        var castle = Points(parts[4]).Single();
        var who = parts[7].Split('@');
        var slot = int.Parse(who[0], CultureInfo.InvariantCulture);
        var at = int.Parse(parts[8], CultureInfo.InvariantCulture);
        var passes = int.Parse(parts[9], CultureInfo.InvariantCulture);

        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, castle[0], castle[1]));
        foreach (var flag in Points(parts[5]))
        {
            game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, flag[0], flag[1]));
        }

        foreach (var road in parts[6].Split(';').Select(text => text.Split(',')))
        {
            game.Enqueue(
                new Command(
                    0,
                    0,
                    CommandKind.BuildRoad,
                    int.Parse(road[0]),
                    int.Parse(road[1]),
                    [.. road[2].Select(c => c - '0')]
                )
            );
        }

        string? settler = null;
        string? rng = null;
        string? waiting = null;
        string? other = null;
        var watched = parts.Length > 11 ? int.Parse(parts[11], CultureInfo.InvariantCulture) : -1;
        for (var pass = 0; pass < passes; pass++)
        {
            if (pass == at)
            {
                var sent = game.Settlers[slot];
                if (who.Length > 1)
                {
                    var node = who[1].Split(',').Select(int.Parse).ToArray();
                    sent.X = node[0];
                    sent.Y = node[1];
                }

                sent.Activity = new ClaimsHomeNode();
                if (parts.Length > 10)
                {
                    // The scenario forces the state and the mode the original's run had at that pass.
                    var force = parts[10].Split(',').Select(int.Parse).ToArray();
                    sent.Activity = (force[0], force[1]) switch
                    {
                        (0x19, 1) => new SearchesHome(true),
                        (2, 0xfe) => new Walks(new ToStorage(), new Heading(0)),
                        _ => throw new InvalidOperationException($"{name}: no activity for the forced {parts[10]}"),
                    };
                }
            }

            game.Tick();
            foreach (var line in scenario.Lines.Where(line => line.Pass == pass))
            {
                settler = line.Settler ?? settler;
                rng = line.Rng ?? rng;
                waiting = line.Waiting ?? waiting;
                other = line.Other ?? other;
            }

            var words = game.Draws.Generator;
            Assert.AreEqual(
                rng,
                $"{words.W0:X4},{words.W1:X4},{words.W2:X4}",
                $"{name}: the generator after pass {pass}"
            );
            Assert.AreEqual(
                settler,
                WorkerWalkTests.Hex(game.Settlers[slot], map.Size.Width),
                $"{name}: the settler after pass {pass}"
            );
            // A carrier in state 0x42 holds a pointer to the flag's item in its last four bytes, where the engine keeps the slot.
            if (watched >= 0 && other![20..22] != "42")
            {
                Assert.AreEqual(
                    other,
                    WorkerWalkTests.Hex(game.Settlers[watched], map.Size.Width),
                    $"{name}: the settler it meets after pass {pass}"
                );
            }

            var listed = game.Roster.Waiting(game.Settlers[slot].Job);
            Assert.AreEqual(
                $"{listed & 0xff:X2}{listed >> 8:X2}",
                waiting,
                $"{name}: the castle's list after pass {pass}"
            );
        }

        // The settler has walked into the castle and is in its list; the settler that finds no road to a storage does not.
        if (name != NoStorage)
        {
            Assert.IsInstanceOfType<WaitsInCastle>(
                game.Settlers[slot].Activity,
                $"{name}: the settler waits in the castle"
            );
            Assert.AreEqual(
                slot,
                game.Roster.Waiting(game.Settlers[slot].Job),
                $"{name}: the settler is in the castle's list"
            );
        }
    }
}
