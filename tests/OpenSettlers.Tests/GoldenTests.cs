using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A golden test that pins the game as it plays: fixed scenarios (a seed or a checked-in layout, a command script, the
// every-pass setting) run on made-up tables, and at several ticks each records the game's digest (Game.Digest) and a
// short digest of the whole recorded event log (each event's tick, type, thing, raw field=value state, cause, and
// other). The expected lines live in golden.txt: the event counts of the original lines were taken once from the engine as it
// stood before the onion refactor of the simulation (the two site-with-handout lines were taken in the M4 sweep), the event-log hashes were regenerated for the castle settler's renamed event
// (castle-settler-builds), and the digests were regenerated for the flags' request words, on made-up tables
// (LayoutSettlersTests.MadeTables; ConstructionTables.Tables() for site-with-handout), so it needs no install: paths that depend on the
// real program tables' step times or building classes are not pinned (INV-0001: digests, not dumps). A change to any rule outcome, the order of two settlers in a pass, or
// an event field changes a line. INV-0002: a scenario gives the same lines on every run, as the committed lines pin. INV-0007: the digest holds no
// event store, so the digest of a run is the same whether or not anything reads the events: each scenario plays once and
// reads no event until it has ended, and the committed lines, taken from runs that read the events as they went, still match.
[TestClass]
public sealed class GoldenTests
{
    const string Fixture = "golden.txt";
    static readonly MapSeed GeneratedSeed = new(0x1234, 0x5678, 0x9ABC);
    const string GeneratedWhere = "generated seed=1234,5678,9ABC size=1";

    // The kind of a step that stands a finished woodcutter's hut on the node before the tick: no command gives a finished
    // building, so the script places it through the seam a layout's finished-building entry uses.
    const int FinishedHut = -1;

    // A command at a tick: the scripts enqueue every command before the first tick, but a finished hut, which no command gives.
    sealed record Step(long Tick, int Kind, int X, int Y, int[]? Directions = null, BuildingType? Building = null);

    sealed record Scenario(
        string Name,
        string Where,
        Func<bool, Game> Start,
        IReadOnlyList<Step> Script,
        IReadOnlyList<int> Checkpoints
    );

    // The castle at 15,5 puts its flag at 16,6. Flags at 18,6 (slot 1), 19,4 (2), and 17,3 (3) with roads between
    // them, and a woodcutter's hut at 16,2 on its own flag, as ThingHistoryRemovalTests lays them out.
    static readonly Step[] Town =
    [
        new(0, CommandKind.PlaceCastle, 15, 5),
        new(1, CommandKind.PlaceFlag, 18, 6),
        new(2, CommandKind.PlaceFlag, 19, 4),
        new(3, CommandKind.PlaceFlag, 17, 3),
        new(4, CommandKind.BuildRoad, 16, 6, [0, 0]),
        new(5, CommandKind.BuildRoad, 18, 6, [5, 5, 0]),
        new(6, CommandKind.BuildRoad, 17, 3, [0, 1]),
        new(7, FinishedHut, 16, 2),
    ];

    // The class 4 the removal click takes a building for.
    const byte BuildingClass = 4;

    static readonly int[] Loop = [0, 1, 2, 3, 4, 5];

    static readonly IReadOnlyList<int> Checkpoints = [100, 700, 1500, 3000, 6000];

    static Game Generated(bool everyPass)
    {
        var map = MapGenerator.Generate(TestProgram.Made(), GeneratedSeed, MapSize.Offered[0]);

        return GameFactory.Create(LayoutSettlersTests.MadeTables(BuildingClass), map, everyPassTurns: everyPass);
    }

    // The game as the game view runs it (the castle's hand-out on, GameFactory.Create's handsOutGoods), on the made-up tables of the
    // construction rules.
    static Game HandingOut(bool everyPass)
    {
        var map = MapGenerator.Generate(TestProgram.Made(), GeneratedSeed, MapSize.Offered[0]);

        return GameFactory.Create(ConstructionTables.Tables(), map, everyPassTurns: everyPass, handsOutGoods: true);
    }

    static Func<bool, Game> OnLayout(string file) =>
        everyPass =>
        {
            var path = Path.Combine(CheckedInLayoutsTests.LayoutsFolder(), file);
            var read = Layout.Read(File.ReadAllText(path), file);
            Assert.IsNotNull(read.Layout, string.Join("; ", read.Problems));
            var game = GameFactory.Create(
                LayoutSettlersTests.MadeTables(BuildingClass),
                read.Layout.BuildMap(),
                everyPassTurns: everyPass
            );
            Assert.IsNull(read.Layout.Apply(game));

            return game;
        };

    static readonly Scenario[] Scenarios =
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
            "site-with-handout",
            GeneratedWhere + " hands-out-goods=on",
            HandingOut,
            [
                new(0, CommandKind.PlaceCastle, 15, 5),
                new(1, CommandKind.PlaceFlag, 18, 6),
                new(2, CommandKind.BuildRoad, 16, 6, [0, 0]),
                new(3, CommandKind.PlaceBuilding, 17, 5, Building: BuildingType.Woodcutter),
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
            [new(400, CommandKind.RemoveRoad, 15, 11), new(401, FinishedHut, 13, 10)],
            Checkpoints
        ),
    ];

    // The 64-bit FNV-1a of the events as lines, in the order recorded.
    static ulong EventDigest(IEnumerable<ThingEvent> events)
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

    sealed record Outcome(string Line, Game Game);

    // Plays the scenario once and reads no event until it has ended, so the digests at the checkpoints are those of a run
    // that nothing reads the events of (INV-0007). The events as of each checkpoint are the log's events up to its tick.
    // eachTick, where given, runs after every tick.
    static Outcome Run(Scenario scenario, bool everyPass, Action<Game>? eachTick = null)
    {
        var game = scenario.Start(everyPass);
        foreach (var step in scenario.Script.Where(step => step.Kind != FinishedHut))
        {
            game.Enqueue(new Command(step.Tick, 0, step.Kind, step.X, step.Y, step.Directions, step.Building));
        }

        var digests = new List<string>();
        foreach (var checkpoint in scenario.Checkpoints)
        {
            while (game.TickCount < checkpoint)
            {
                foreach (
                    var hut in scenario.Script.Where(step => step.Kind == FinishedHut && step.Tick == game.TickCount)
                )
                {
                    Assert.IsTrue(Huts.Place(game, hut.X, hut.Y), "the scenario's finished hut has its site");
                }

                game.Tick();
                eachTick?.Invoke(game);
            }

            digests.Add($"{checkpoint}:{game.Digest():x16}");
        }

        var events = game.History.Events.All.ToList();
        var points = scenario.Checkpoints.Select(
            (checkpoint, i) =>
            {
                var upTo = events.TakeWhile(e => e.Tick < checkpoint).ToList();

                return $"{digests[i]}:{upTo.Count}:{EventDigest(upTo):x16}";
            }
        );
        var line =
            $"{scenario.Name} | {scenario.Where} every-pass={(everyPass ? "on" : "off")} | {string.Join(" ", points)}";

        return new Outcome(line, game);
    }

    // Each scenario plays once per test run; the tests below assert its properties on this outcome.
    static readonly Lazy<IReadOnlyList<Outcome>> Outcomes = new(() =>
        Scenarios.SelectMany(scenario => new[] { true, false }.Select(everyPass => Run(scenario, everyPass))).ToList()
    );

    [TestMethod]
    public void EveryScenarioPlaysAsTheCommittedLinesRecord()
    {
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, Fixture))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        var actual = Outcomes.Value.Select(o => o.Line).ToArray();

        CollectionAssert.AreEqual(
            expected,
            actual,
            "the game no longer plays as golden.txt records; actual lines:\n" + string.Join("\n", actual)
        );
    }

    // The golden lines of site-with-handout pin a game with a site only where the site at 17,5 is accepted.
    [TestMethod]
    public void TheSiteOfSiteWithHandoutIsAccepted()
    {
        var runs = Outcomes
            .Value.Where(o => o.Line.StartsWith("site-with-handout |", StringComparison.Ordinal))
            .ToList();

        Assert.HasCount(2, runs);
        foreach (var run in runs)
        {
            Assert.AreEqual(1, run.Game.History.Events.All.Count(e => e.Type == EventType.PlaceBuilding), run.Line);
        }
    }

    // INV-0007 with construction under way: site-with-handout, played while after every tick each building's and each
    // settler's events and its state at that tick are read, gives the committed lines, digests and event logs alike.
    [TestMethod]
    public void ReadingEveryThingsEventsEachTickOfSiteWithHandoutGivesTheCommittedLines()
    {
        var scenario = Scenarios.Single(s => s.Name == "site-with-handout");
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, Fixture))
            .Where(line => line.StartsWith("site-with-handout |", StringComparison.Ordinal))
            .ToArray();
        var reads = 0;
        var siteBuilt = false;

        void ReadEveryThing(Game game)
        {
            var tick = game.TickCount - 1;
            for (var slot = 0; slot < game.Buildings.Count; slot++)
            {
                var id = game.History.BuildingId(slot);
                reads += game.History.EventsOf(id).Count;
                _ = game.History.StateAt(id, tick);
                siteBuilt |= game.Buildings[slot].Constructing && game.Buildings[slot].Progress > 0;
            }

            for (var slot = 0; slot < game.Settlers.Count; slot++)
            {
                var id = game.History.SettlerId(slot);
                reads += game.History.EventsOf(id).Count;
                _ = game.History.StateAt(id, tick);
            }
        }

        var actual = new[] { true, false }.Select(everyPass => Run(scenario, everyPass, ReadEveryThing).Line).ToArray();

        Assert.IsTrue(siteBuilt, "the site's construction was under way while the events were read");
        Assert.IsGreaterThan(0, reads);
        CollectionAssert.AreEqual(expected, actual);
    }

    static Func<ThingEvent, bool> OfType(params string[] types) => e => types.Contains(e.Type);

    // Every behaviour the engine has leaves at least its minimum of these events in the log of one scenario: a recurring
    // behaviour at least Recurring times, a behaviour a scenario sets going once (a removal, a split, the loop road, a
    // head-on meeting, the woodcutter's one log) once.
    const int Recurring = 3;

    static readonly (string Behaviour, int Min, Func<ThingEvent, bool> Is)[] Behaviours =
    [
        ("castle", 1, OfType(EventType.PlaceCastle)),
        ("flags", Recurring, OfType(EventType.PlaceFlag)),
        ("roads", Recurring, OfType(EventType.BuildRoad)),
        ("a flag on a road", 1, OfType(EventType.SplitRoads)),
        (
            "the loop road: a road whose two ends are one flag",
            1,
            e => e.Type == EventType.BuildRoad && e.Other == e.Thing.ToString()
        ),
        ("the woodcutter searching a tree", Recurring, OfType(EventType.SearchTree)),
        ("the woodcutter cutting a tree", Recurring, OfType(EventType.CutTree)),
        ("the woodcutter carrying", 1, OfType(EventType.PutGood)),
        ("carriers entering a road", Recurring, OfType(EventType.EnterRoad)),
        ("carriers walking a road", Recurring, OfType(EventType.Walked)),
        ("goods fetched", Recurring, OfType(EventType.Fetch)),
        ("goods delivered", 1, OfType(EventType.Deliver)),
        ("goods handed over", Recurring, OfType(EventType.Handover)),
        ("the walk home", Recurring, OfType(EventType.ArrivedHome)),
        // The castle is the one storage a settler walks home to: the event names it (building slot 0).
        (
            "entering the castle",
            Recurring,
            e => e.Type == EventType.EnteredStorage && e.Other?.StartsWith("building#0.") == true
        ),
        ("taking a road down", 1, OfType(EventType.EndOfRoad)),
        ("taking a building down", 1, OfType(EventType.TookBuildingDown)),
        ("taking a flag down", 1, OfType(EventType.TookFlagDown)),
        ("two settlers meeting head-on", 1, e => e.Type == EventType.Stepped && e.Other is not null),
    ];

    [TestMethod]
    public void EveryBehaviourLeavesItsEventsInTheEventLogOfAScenario()
    {
        var logs = Outcomes.Value.Select(o => o.Game.History.Events.All).ToList();

        var problems = Behaviours
            .Select(b => (b.Behaviour, b.Min, Count: logs.Max(log => log.Count(b.Is))))
            .Where(b => b.Count < b.Min)
            .Select(b => $"{b.Behaviour}: {b.Count} events, at least {b.Min} wanted");

        Assert.AreEqual("", string.Join("\n", problems));
    }
}
