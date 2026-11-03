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
// the settler (s=) and the random generator's words (rng=). The engine runs the same commands, sends the same settler home
// in the same pass, and must hold the same bytes after each pass until the settler reaches state 2 with the mode 0xfe, the
// walk to the storage that the next slice ports. That tests the claim of the node (0x434946), the search for the nearest own
// flag with a road (0x4353ef) with its random fallback, the free walk, and the arrival (0x4385ab).
[TestClass]
public sealed class HomeWalkTests
{
    private const string Fixture = "home-walks.txt";

    private sealed record Scenario(string Name, string Command, List<(int Pass, string? Settler, string? Rng)> Lines);

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
                        fields.GetValueOrDefault("rng")
                    )
                );
        }

        return scenarios;
    }

    private static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    // The walk to the storage: state 2 with the mode 0xfe (bytes 10 and 11 of the item).
    private static bool AtStorageWalk(string settler) => settler[20..24] == "02FE";

    private static Game CastleWithRoad()
    {
        MapSize.TryFromValue(1, out var size, out _);
        var map = MapGenerator.Generate(new MapSeed(0x1234, 0x5678, 0x9ABC), size!);
        var game = new Game(TestProgram.Tables(), map, 0);
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

        game.Settlers[12].State = 0x1c;
        game.Settlers[13].State = 0x1c;
        var firstClaimed = false;
        for (var pass = 0; pass < 120 && game.Settlers[13].State == 0x1c; pass++)
        {
            game.Tick();
            if (game.Settlers[12].State != 0x1c && !firstClaimed)
            {
                firstClaimed = true;
                Assert.AreEqual(0x1c, game.Settlers[13].State, "the node is taken while the first stands on it");
            }
        }

        Assert.IsTrue(firstClaimed);
        Assert.AreNotEqual(0x1c, game.Settlers[13].State, "the second claims the node once it is free");
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

        var knight = game.Settlers.First(settler => settler.Job == 22);
        knight.State = 0x1c;
        for (var pass = 0; pass < 80; pass++)
        {
            game.Tick();
        }

        Assert.AreEqual(0x35, knight.State);
        Assert.AreEqual(0x80, knight.Target >> 8, "the vector of the walk home is set as for the others");
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void ASettlerSentHomeWalksAsTheOriginalsPassesMoveIt(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        // home:N:SEED:L:cx,cy:FLAGS:ROADS:SLOT[@x,y]:AT:PASSES[:STATE,MODE]
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(new MapSeed(seed[0], seed[1], seed[2]), size!);
        var game = new Game(TestProgram.Tables(), map, int.Parse(parts[3], CultureInfo.InvariantCulture));
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

                sent.State = 0x1c;
                if (parts.Length > 10)
                {
                    var force = parts[10].Split(',').Select(int.Parse).ToArray();
                    sent.State = force[0];
                    sent.Mode = force[1];
                }
            }

            game.Tick();
            foreach (var line in scenario.Lines.Where(line => line.Pass == pass))
            {
                settler = line.Settler ?? settler;
                rng = line.Rng ?? rng;
            }

            var words = game.Random;
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
            if (AtStorageWalk(settler!))
            {
                return;
            }
        }

        Assert.Fail($"{name}: the settler never reached the walk to the storage");
    }
}
