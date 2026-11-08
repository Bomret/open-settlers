using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A golden test that pins the game as it plays: fixed scenarios (a seed or a checked-in layout, a command script, the
// every-pass setting) run on made-up tables, and at several ticks each records the game's digest (Game.Digest) and a
// short digest of the whole recorded event log (each event's tick, type, thing, raw field=value state, cause, and
// other). The expected lines live in golden.txt, taken once from the engine as it stood before the onion refactor of
// the simulation, on made-up tables (LayoutSettlersTests.MadeTables), so it needs no install: paths that depend on the
// real program tables' step times or building classes are not pinned (INV-0001: digests, not dumps). A change to any rule outcome, the order of two settlers in a pass, or
// an event field changes a line. INV-0002: a scenario run twice gives the same lines. INV-0007: the digest holds no
// event store, so the digest of a run is the same whether or not anything reads the events.
[TestClass]
public sealed class GoldenTests
{
    private const string Fixture = "golden.txt";
    private static readonly MapSeed GeneratedSeed = new(0x1234, 0x5678, 0x9ABC);
    private const string GeneratedWhere = "generated seed=1234,5678,9ABC size=1";

    // A command at a tick: the scripts enqueue every command before the first tick.
    private sealed record Step(long Tick, int Kind, int X, int Y, int[]? Directions = null);

    private sealed record Scenario(
        string Name,
        string Where,
        Func<bool, Game> Start,
        IReadOnlyList<Step> Script,
        IReadOnlyList<int> Checkpoints
    );

    // The castle at 15,5 puts its flag at 16,6. Flags at 18,6 (slot 1), 19,4 (2), and 17,3 (3) with roads between
    // them, and a woodcutter's hut at 16,2 on its own flag, as ThingHistoryRemovalTests lays them out.
    private static readonly Step[] Town =
    [
        new(0, CommandKind.PlaceCastle, 15, 5),
        new(1, CommandKind.PlaceFlag, 18, 6),
        new(2, CommandKind.PlaceFlag, 19, 4),
        new(3, CommandKind.PlaceFlag, 17, 3),
        new(4, CommandKind.BuildRoad, 16, 6, [0, 0]),
        new(5, CommandKind.BuildRoad, 18, 6, [5, 5, 0]),
        new(6, CommandKind.BuildRoad, 17, 3, [0, 1]),
        new(7, CommandKind.PlaceBuilding, 16, 2),
    ];

    // The class 4 the removal click takes a building for.
    private const byte BuildingClass = 4;

    private static readonly int[] Loop = [0, 1, 2, 3, 4, 5];

    private static readonly IReadOnlyList<int> Checkpoints = [100, 700, 1500, 3000, 6000];

    private static Game Generated(bool everyPass)
    {
        var map = MapGenerator.Generate(TestProgram.Made(), GeneratedSeed, MapSize.Offered[0]);

        return new Game(LayoutSettlersTests.MadeTables(BuildingClass), map) { EveryPassTurns = everyPass };
    }

    private static Func<bool, Game> OnLayout(string file) =>
        everyPass =>
        {
            var path = Path.Combine(CheckedInLayoutsTests.LayoutsFolder(), file);
            var read = Layout.Read(File.ReadAllText(path), file);
            Assert.IsNotNull(read.Layout, string.Join("; ", read.Problems));
            var game = new Game(LayoutSettlersTests.MadeTables(BuildingClass), read.Layout.BuildMap());
            game.EveryPassTurns = everyPass;
            Assert.IsNull(read.Layout.Apply(game));

            return game;
        };

    private static readonly Scenario[] Scenarios =
    [
        new("town-woodcutter-goods", GeneratedWhere, Generated, Town, Checkpoints),
        new(
            "town-removals",
            GeneratedWhere,
            Generated,
            [
                .. Town,
                new(900, CommandKind.RemoveFlag, 18, 6),
                new(1500, CommandKind.RemoveBuilding, 16, 2),
                new(2200, CommandKind.RemoveRoad, 17, 6),
                new(2800, CommandKind.RemoveFlag, 17, 3),
            ],
            Checkpoints
        ),
        new(
            "loop-and-split",
            GeneratedWhere,
            Generated,
            [
                new(0, CommandKind.PlaceCastle, 15, 5),
                new(1, CommandKind.BuildRoad, 16, 6, Loop),
                new(1600, CommandKind.PlaceFlag, 18, 8),
                new(3200, CommandKind.PlaceFlag, 17, 6),
            ],
            Checkpoints
        ),
        new(
            "head-on",
            GeneratedWhere,
            Generated,
            [
                new(0, CommandKind.PlaceCastle, 15, 5),
                new(1, CommandKind.PlaceFlag, 18, 6),
                new(2, CommandKind.PlaceFlag, 20, 6),
                new(3, CommandKind.BuildRoad, 16, 6, [0, 0]),
                new(4, CommandKind.BuildRoad, 18, 6, [0, 0]),
                new(405, CommandKind.RemoveRoad, 19, 6),
                new(406, CommandKind.BuildRoad, 18, 6, [0, 0]),
            ],
            Checkpoints
        ),
        new(
            "woodcutter-layout",
            "layouts/woodcutter.jsonc size=1",
            OnLayout("woodcutter.jsonc"),
            [
                new(2500, CommandKind.RemoveBuilding, 13, 10),
                new(3500, CommandKind.RemoveRoad, 12, 11),
                new(4500, CommandKind.RemoveFlag, 14, 11),
            ],
            Checkpoints
        ),
        new("carrier-layout", "layouts/carrier.jsonc size=1", OnLayout("carrier.jsonc"), [], Checkpoints),
        new(
            "head-on-pass-layout",
            "layouts/head-on-pass.jsonc size=1",
            OnLayout("head-on-pass.jsonc"),
            [new(400, CommandKind.RemoveRoad, 15, 11), new(401, CommandKind.PlaceBuilding, 13, 10)],
            Checkpoints
        ),
    ];

    // The 64-bit FNV-1a of the events as lines, in the order recorded.
    private static ulong EventDigest(IEnumerable<ThingEvent> events)
    {
        var hash = 0xcbf29ce484222325UL;
        foreach (var e in events)
        {
            var line = $"{e.Tick} {e.Type} {e.Thing} {e.State} {e.Cause} {e.Other}\n";
            foreach (var b in Encoding.UTF8.GetBytes(line))
            {
                hash = (hash ^ b) * 0x100000001b3UL;
            }
        }

        return hash;
    }

    private sealed record Outcome(string Line, Game Game, IReadOnlyList<string> Digests);

    // readEvents false never touches Game.Events, so the line holds only the digests.
    private static Outcome Run(Scenario scenario, bool everyPass, bool readEvents = true)
    {
        var game = scenario.Start(everyPass);
        foreach (var step in scenario.Script)
        {
            game.Enqueue(new Command(step.Tick, 0, step.Kind, step.X, step.Y, step.Directions));
        }

        var points = new List<string>();
        var digests = new List<string>();
        foreach (var checkpoint in scenario.Checkpoints)
        {
            while (game.TickCount < checkpoint)
            {
                game.Tick();
            }

            var digest = $"{checkpoint}:{game.Digest():x16}";
            digests.Add(digest);
            points.Add(readEvents ? $"{digest}:{game.Events.Count}:{EventDigest(game.Events.All):x16}" : digest);
        }

        var line =
            $"{scenario.Name} | {scenario.Where} every-pass={(everyPass ? "on" : "off")} | {string.Join(" ", points)}";

        return new Outcome(line, game, digests);
    }

    private static IEnumerable<(Scenario Scenario, bool EveryPass)> Runs() =>
        Scenarios.SelectMany(scenario => new[] { true, false }.Select(everyPass => (scenario, everyPass)));

    [TestMethod]
    public void EveryScenarioPlaysAsTheCommittedLinesRecord()
    {
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, Fixture))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        var actual = Runs().Select(r => Run(r.Scenario, r.EveryPass).Line).ToArray();

        CollectionAssert.AreEqual(
            expected,
            actual,
            "the game no longer plays as golden.txt records; actual lines:\n" + string.Join("\n", actual)
        );
    }

    [TestMethod]
    public void AScenarioRunTwiceGivesTheSameLines()
    {
        foreach (var (scenario, everyPass) in Runs())
        {
            Assert.AreEqual(Run(scenario, everyPass).Line, Run(scenario, everyPass).Line, scenario.Name);
        }
    }

    [TestMethod]
    public void TheDigestIsTheSameWhetherOrNotAnythingReadsTheEvents()
    {
        foreach (var (scenario, everyPass) in Runs())
        {
            var read = Run(scenario, everyPass).Digests;
            var unread = Run(scenario, everyPass, readEvents: false).Digests;

            CollectionAssert.AreEqual(read.ToArray(), unread.ToArray(), scenario.Name);
        }
    }

    private static Func<ThingEvent, bool> OfType(params string[] types) => e => types.Contains(e.Type);

    // Every behaviour the engine has leaves each of these events at least once in the log of one scenario.
    private static readonly (string Behaviour, Func<ThingEvent, bool> Is)[] Behaviours =
    [
        ("castle", OfType(EventType.PlaceCastle)),
        ("flags", OfType(EventType.PlaceFlag)),
        ("roads", OfType(EventType.BuildRoad)),
        ("a flag on a road", OfType(EventType.SplitRoads)),
        (
            "the loop road: a road whose two ends are one flag",
            e => e.Type == EventType.BuildRoad && e.Other == e.Thing.ToString()
        ),
        ("the woodcutter searching a tree", OfType(EventType.SearchTree)),
        ("the woodcutter cutting a tree", OfType(EventType.CutTree)),
        ("the woodcutter carrying", OfType(EventType.PutGood)),
        ("carriers entering a road", OfType(EventType.EnterRoad)),
        ("carriers walking a road", OfType(EventType.Walked)),
        ("goods fetched", OfType(EventType.Fetch)),
        ("goods delivered", OfType(EventType.Deliver)),
        ("goods handed over", OfType(EventType.Handover)),
        ("the walk home", OfType(EventType.ArrivedHome)),
        ("entering a storage", OfType(EventType.EnteredStorage)),
        ("taking a road down", OfType(EventType.EndOfRoad)),
        ("taking a building down", OfType(EventType.TookBuildingDown)),
        ("taking a flag down", OfType(EventType.TookFlagDown)),
        ("two settlers meeting head-on", e => e.Type == EventType.Stepped && e.Other is not null),
    ];

    [TestMethod]
    public void EveryBehaviourActsSeveralTimesInTheEventLogOfAScenario()
    {
        var logs = Runs().Select(r => Run(r.Scenario, r.EveryPass).Game.Events.All).ToList();

        var problems = Behaviours
            .Select(b => (b.Behaviour, Count: logs.Max(log => log.Count(b.Is))))
            .Where(b => b.Count < 1)
            .Select(b => $"{b.Behaviour}: {b.Count} events");

        Assert.AreEqual("", string.Join("\n", problems));
    }
}
