using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The play-script steps for named things (ADR 0011): name, mark, expect <alias>.<field>, expect-event, expect-gone, events.
[TestClass]
public sealed class PlayNamedThingTests
{
    readonly string _out = Path.Combine(Path.GetTempPath(), "play-named-" + Guid.NewGuid().ToString("N"), "stem");

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(Path.GetDirectoryName(_out)))
            Directory.Delete(Path.GetDirectoryName(_out)!, true);
    }

    static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    // The castle at 15,5 (flag#0.1 at 16,6) and a flag#1.1 at 18,6.
    static Game Castle()
    {
        var game = ThingHistoryTests.NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);

        return game;
    }

    static PlayState StateOf(Game game) =>
        new(0, 0, 1f, "1", 1, game.TickCount, 0, new Dictionary<(int X, int Y), int>(), Thing.Of(game, null), game);

    static PlayScript Parse(string text) => PlayScript.Parse(text, "play/t.play");

    PlayRun Run(string text, Game game)
    {
        var run = new PlayRun(Parse(text), () => StateOf(game), _out, _ => null);
        run.Advance(1.0 / 30);

        return run;
    }

    static string Problem(PlayRun run)
    {
        Assert.IsTrue(run.Finished);

        return string.Join("\n", run.Problems);
    }

    [TestMethod]
    [DataRow("expect-event f place-flag nocause x", "'nocause' is not 'cause'")]
    [DataRow("events f a/b", "'a/b' is no file name")]
    [DataRow("events f ..", "'..' is no file name")]
    [DataRow("expect f.x ~ 1", "unknown operator '~', use one of < <= = >= >")]
    [DataRow("expect f.x = abc", "'abc' is not a number")]
    [DataRow("expect f.id < flag#1.1", "'id' takes only '=', not '<'")]
    public void ABadLineOnANamedThingIsRefusedNamingItsLineAndReason(string line, string reason)
    {
        var script = Parse("name f 18 6 flag\n" + line);

        Assert.AreEqual("play/t.play line 2: " + reason, string.Join("\n", script.Problems));
    }

    [TestMethod]
    public void NameSavesTheOneThingAtTheNode()
    {
        var run = Run("name f 18 6 flag\nexpect f.id = flag#1.1", Castle());

        Assert.AreEqual("", string.Join("\n", run.Problems));
        Assert.IsTrue(run.Finished);
    }

    [TestMethod]
    public void ALinkFieldComparesWithAnAliasOrAThingId()
    {
        var run = Run(
            "name f 18 6 flag\nname g 18 6 flag\nname c 15 5 building\nexpect f.id = g\nexpect f.id = flag#1.1\nexpect c.flag-id = flag#0.1",
            Castle()
        );

        Assert.AreEqual("", string.Join("\n", run.Problems));
        Assert.IsTrue(run.Finished);
    }

    [TestMethod]
    public void ALinkFieldThatDiffersFailsWithTheActualId()
    {
        var problem = Problem(Run("name f 18 6 flag\nexpect f.id = flag#9.9", Castle()));

        StringAssert.Contains(problem, "actual flag#1.1");
    }

    [TestMethod]
    public void ALinkFieldIsRefusedAtAMarkAndAtATick()
    {
        var game = Castle();
        var first = game.History.EventsOf(game.History.FlagId(1))[0].Tick;

        StringAssert.Contains(
            Problem(Run("name f 18 6 flag\nmark m\nexpect f.id@m = flag#1.1", game)),
            "shows only now"
        );
        StringAssert.Contains(
            Problem(Run($"name f 18 6 flag\nexpect f.id@{first} = flag#1.1", game)),
            "shows only now"
        );
    }

    [TestMethod]
    public void AMalformedThingIdIsAScriptProblemAtParseTime()
    {
        var problems = Parse(
            string.Join(
                "\n",
                "name f 18 6 flag",
                "expect f.id = foo#",
                "expect f.id = flag#1",
                "expect f.id = flag#1.1"
            )
        ).Problems;

        Assert.HasCount(2, problems);
        Assert.IsTrue(problems.Any(p => p.StartsWith("play/t.play line 2:")));
        Assert.IsTrue(problems.Any(p => p.StartsWith("play/t.play line 3:")));
    }

    [TestMethod]
    public void NameWithNoMatchFailsAndListsTheNode()
    {
        var run = Run("name f 5 5 flag", Castle());

        StringAssert.Contains(Problem(run), "play/t.play line 1");
        StringAssert.Contains(Problem(run), "holds nothing");
    }

    [TestMethod]
    public void NameWithSeveralMatchesFailsAndListsThem()
    {
        var game = Castle();
        var node = game.Settlers.GroupBy(s => (s.X, s.Y)).First(g => g.Count() > 1).Key;

        var run = Run($"name s {node.X} {node.Y} settler", game);

        StringAssert.Contains(Problem(run), "several");
        StringAssert.Contains(Problem(run), "settler#");
    }

    [TestMethod]
    public void ExpectChecksTheFieldNowAtAMarkAndAtATick()
    {
        var game = Castle();
        var type = game.Buildings[0].Type;
        var first = game.History.EventsOf(game.History.BuildingId(0))[0].Tick;
        var run = Run(
            $"name c 15 5 building\nmark start\nexpect c.x = 15\nexpect c.x >= 15\nexpect c.type@start = {type}\nexpect c.type@{first} = {type}\nexpect ticks >= 1",
            game
        );

        Assert.AreEqual("", string.Join("\n", run.Problems));
    }

    [TestMethod]
    public void ExpectFailsByTheEnginesStateNowAtAMarkAndAtATick()
    {
        var game = Castle();
        var first = game.History.EventsOf(game.History.BuildingId(0))[0].Tick;

        StringAssert.Contains(Problem(Run("name c 15 5 building\nexpect c.x = 16", game)), "actual 15");
        StringAssert.Contains(Problem(Run("name c 15 5 building\nmark m\nexpect c.x@m = 16", game)), "actual 15");
        StringAssert.Contains(Problem(Run($"name c 15 5 building\nexpect c.x@{first} = 16", game)), "actual 15");
    }

    [TestMethod]
    public void AMarkDoesNotSeeAThingThatAppearsAfterIt()
    {
        var game = Castle();
        var run = new PlayRun(
            Parse("mark a\nwait 1\nname g 19 4 flag\nexpect g.x = 19\nexpect g.x@a = 19"),
            () => StateOf(game),
            _out,
            _ => null
        );
        run.Advance(0);
        Do(game, CommandKind.PlaceFlag, 19, 4);
        run.Advance(1.0);

        StringAssert.Contains(Problem(run), "line 5");
        StringAssert.Contains(Problem(run), "no event at mark a");
    }

    [TestMethod]
    public void ExpectEventPassesWithAndWithoutACause()
    {
        var run = Run("name f 18 6 flag\nexpect-event f place-flag\nexpect-event f place-flag cause placed", Castle());

        Assert.AreEqual("", string.Join("\n", run.Problems));
    }

    [TestMethod]
    public void ExpectEventFailsNamingTheEventTypesTheThingHas()
    {
        var wrongType = Problem(Run("name f 18 6 flag\nexpect-event f build-road", Castle()));
        var wrongCause = Problem(Run("name f 18 6 flag\nexpect-event f place-flag cause layout", Castle()));

        StringAssert.Contains(wrongType, "place-flag");
        StringAssert.Contains(wrongCause, "place-flag");
    }

    [TestMethod]
    [DataRow("builds")]
    [DataRow("digs")]
    [DataRow("levelled")]
    [DataRow("saws")]
    [DataRow("building-finished")]
    [DataRow("leaves-site")]
    public void ExpectEventAcceptsTheConstructionTypes(string type)
    {
        var problems = Parse($"name f 18 6 flag\nexpect-event f {type}").Problems;

        Assert.AreEqual("", string.Join("\n", problems));
    }

    [TestMethod]
    public void ExpectEventBuildsPassesOnTheBuilderOnceHeHasWorked()
    {
        var (game, _) = BuilderTests.World();
        Settler? builder = null;
        for (var tick = 0; tick < 3000 && builder is null; tick++)
        {
            game.Tick();
            builder = game.Settlers.FirstOrDefault(settler => settler.Activity is Builds);
        }
        Assert.IsNotNull(builder, "the builder works");
        ConstructionWorld.Run(game, 600);

        var text = $"name b {builder.X} {builder.Y} settler\nexpect-event b builds";
        var run = Run(text, game);

        Assert.AreEqual("", string.Join("\n", Parse(text).Problems), "the script reads");
        Assert.IsTrue(run.Finished, "the expect-event step was evaluated");
        Assert.AreEqual("", string.Join("\n", run.Problems));
    }

    [TestMethod]
    public void EveryEventTypeConstantIsListedOnce()
    {
        var declared = typeof(EventType)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        CollectionAssert.AreEquivalent(declared, EventType.All.ToList());
    }

    [TestMethod]
    public void EveryCauseRuleAreaAndKindIsListed()
    {
        var kinds = typeof(Cause)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(
                typeof(Cause)
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Where(property => property.PropertyType == typeof(Cause))
                    .Select(property => ((Cause)property.GetValue(null)!).Kind)
            )
            .Distinct()
            .ToList();

        CollectionAssert.AreEquivalent(kinds, Cause.Kinds.ToList());
    }

    [TestMethod]
    public void ExpectGoneFailsOnALiveThing()
    {
        StringAssert.Contains(Problem(Run("name f 18 6 flag\nexpect-gone f", Castle())), "live");
    }

    [TestMethod]
    public void ACheckOnAGoneThingFailsWithItsTickAndCause()
    {
        var game = Castle();
        var run = new PlayRun(Parse("name f 18 6 flag\nwait 1\nexpect f.x = 18"), () => StateOf(game), _out, _ => null);
        run.Advance(0);
        var id = game.History.FlagId(1);
        Do(game, CommandKind.RemoveFlag, 18, 6);
        var last = game.History.EventsOf(id).Last();
        run.Advance(1.0);

        StringAssert.Contains(Problem(run), $"gone at tick {last.Tick}");
        StringAssert.Contains(Problem(run), last.Cause.ToString());
    }

    [TestMethod]
    public void ExpectEventOnAGoneThingFailsWithItsTickAndCause()
    {
        var game = Castle();
        var run = new PlayRun(
            Parse("name f 18 6 flag\nwait 1\nexpect-event f place-flag"),
            () => StateOf(game),
            _out,
            _ => null
        );
        run.Advance(0);
        var id = game.History.FlagId(1);
        Do(game, CommandKind.RemoveFlag, 18, 6);
        var last = game.History.EventsOf(id).Last();
        run.Advance(1.0);

        StringAssert.Contains(Problem(run), $"gone at tick {last.Tick}");
        StringAssert.Contains(Problem(run), last.Cause.ToString());
    }

    [TestMethod]
    public void EventsOnAGoneThingStillWritesItsHistory()
    {
        var game = Castle();
        var run = new PlayRun(Parse("name f 18 6 flag\nwait 1\nevents f gone"), () => StateOf(game), _out, _ => null);
        run.Advance(0);
        Do(game, CommandKind.RemoveFlag, 18, 6);
        run.Advance(1.0);

        Assert.AreEqual("", string.Join("\n", run.Problems));
        StringAssert.Contains(File.ReadAllText(Path.Combine(_out, "gone.events.txt")), "type=place-flag");
    }

    [TestMethod]
    public void MovedIsNoEventTypeAndExpectEventNamingItIsAScriptProblem()
    {
        var problems = Parse("name f 18 6 flag\nexpect-event f moved").Problems;

        Assert.HasCount(1, problems);
        Assert.IsTrue(problems[0].StartsWith("play/t.play line 2:"));
    }

    [TestMethod]
    public void ExpectGonePassesWhenTheSlotHoldsANewThing()
    {
        var game = Castle();
        var run = new PlayRun(Parse("name f 18 6 flag\nwait 1\nexpect-gone f"), () => StateOf(game), _out, _ => null);
        run.Advance(0);
        Do(game, CommandKind.RemoveFlag, 18, 6);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Assert.AreEqual("flag#1.2", game.History.FlagId(1).ToString());
        run.Advance(1.0);

        Assert.AreEqual("", string.Join("\n", run.Problems));
        Assert.IsTrue(run.Finished);
    }

    [TestMethod]
    public void ScriptProblemsNameTheLine()
    {
        var problems = Parse(
            string.Join(
                "\n",
                "name f 18 6 flag", // 1 ok
                "name f 18 6 flag", // 2 repeated alias
                "mark m", // 3 ok
                "mark m", // 4 repeated mark
                "expect g.x = 1", // 5 unknown alias
                "expect f.x@nope = 1", // 6 unknown mark
                "expect f.bogus = 1", // 7 unknown field
                "expect-event f bogus", // 8 unknown type
                "expect-event f place-flag cause bogus", // 9 unknown cause
                "expect-gone g", // 10 unknown alias
                "events g out", // 11 unknown alias
                "name ticks 1 1 flag" // 12 alias is an expect field
            )
        ).Problems;

        for (var line = 2; line <= 12; line++)
        {
            if (line == 3)
                continue;
            Assert.IsTrue(problems.Any(p => p.StartsWith($"play/t.play line {line}:")), $"line {line}");
        }

        Assert.HasCount(10, problems);
        Assert.IsTrue(problems.Any(p => p.Contains("line 8:") && p.Contains("unknown event type 'bogus'")));
        Assert.IsTrue(problems.Any(p => p.Contains("line 9:") && p.Contains("unknown cause 'bogus'")));
    }

    [TestMethod]
    public void AnExistingExpectLineKeepsItsMeaning()
    {
        var script = Parse("expect ticks >= 1\nexpect camera-x = 0");

        Assert.IsEmpty(script.Problems);
        Assert.IsInstanceOfType<ExpectStep>(script.Steps[0]);
    }

    [TestMethod]
    public void TheMapViewRefusesEveryStepThatNamesAThing()
    {
        var steps = new PlayStep[]
        {
            new NameStep
            {
                Line = 1,
                Alias = "a",
                Column = 1,
                Row = 1,
                What = ThingSelector.Parse("object").Selector!,
            },
            new MarkStep { Line = 1, Label = "m" },
            new ExpectThingStep
            {
                Line = 1,
                Alias = "a",
                Field = "x",
                Op = "=",
                Value = "1",
            },
            new ExpectEventStep
            {
                Line = 1,
                Alias = "a",
                Type = "place-flag",
            },
            new ExpectGoneStep { Line = 1, Alias = "a" },
            new EventsStep
            {
                Line = 1,
                Alias = "a",
                Name = "o",
            },
        };

        foreach (var step in steps)
        {
            var run = new PlayRun(
                new PlayScript
                {
                    Path = "play/t.play",
                    Steps = [step],
                    Problems = [],
                },
                () => new PlayState(0, 0, 1f, "1", 1),
                _out,
                _ => null
            );
            run.Advance(1.0 / 30);

            StringAssert.Contains(Problem(run), "map view", step.GetType().Name);
        }
    }

    [TestMethod]
    public void EventsWritesTheSameFileInTwoRuns()
    {
        var texts = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            Run("name f 18 6 flag\nevents f flag", Castle());
            texts.Add(File.ReadAllText(Path.Combine(_out, "flag.events.txt")));
            File.Delete(Path.Combine(_out, "flag.events.txt"));
        }

        Assert.AreEqual(texts[0], texts[1]);
        StringAssert.Contains(texts[0], "place-flag");
        Assert.HasCount(1, texts[0].TrimEnd('\n').Split('\n'));
    }

    // A state of the game with a map size value the original does not offer.
    static PlayState StateOfSize(Game game, int size, Thing[]? things = null) =>
        new(
            0,
            0,
            1f,
            "1",
            size,
            game.TickCount,
            0,
            new Dictionary<(int X, int Y), int>(),
            things ?? Thing.Of(game, null),
            game
        );

    string RunProblem(string text, Game game) => Problem(Run(text, game));

    [TestMethod]
    public void NameOnAMapSizeTheOriginalDoesNotOfferIsRefused()
    {
        var game = Castle();
        var run = new PlayRun(Parse("name f 18 6 flag"), () => StateOfSize(game, 7), _out, _ => null);
        run.Advance(1.0 / 30);

        StringAssert.Contains(Problem(run), "the map size 7 is not one the original offers");
    }

    [TestMethod]
    public void AnExpectAtStepOnAnUnofferedMapSizeIsRefused()
    {
        var state = StateOfSize(Castle(), 7);
        var what = ThingSelector.Parse("flag").Selector!;

        var problem = state.Check(
            new ExpectAtStep
            {
                Line = 1,
                Column = 18,
                Row = 6,
                What = what,
            }
        );

        StringAssert.Contains(problem, "the map size 7 is not one the original offers");
    }

    [TestMethod]
    [DataRow("name f 32 5 flag")]
    [DataRow("name f 5 32 flag")]
    public void NameOnANodeOnePastTheMapIsRefused(string line)
    {
        StringAssert.Contains(RunProblem(line, Castle()), "the node is outside the 32 by 32 map");
    }

    [TestMethod]
    public void NameAcceptsTheLastColumnAndTheLastRow()
    {
        var game = Castle();

        StringAssert.Contains(RunProblem("name f 31 5 flag", game), "no thing matches");
        StringAssert.Contains(RunProblem("name f 5 31 flag", game), "no thing matches");
        Assert.DoesNotContain("outside", RunProblem("name f 31 31 flag", game));
    }

    [TestMethod]
    public void NameWithNoMatchSaysSoAndSeveralMatchesSaySo()
    {
        var game = Castle();
        var node = game.Settlers.GroupBy(s => (s.X, s.Y)).First(g => g.Count() > 1).Key;

        StringAssert.Contains(RunProblem("name f 5 5 flag", game), "no thing matches");
        StringAssert.Contains(RunProblem($"name s {node.X} {node.Y} settler", game), "several things match");
    }

    [TestMethod]
    public void NameOfAThingWithNoThingIdIsRefusedForARoadNodeAndAMapObject()
    {
        var game = ThingHistoryTests.NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 20, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0, 0, 0]);
        var taken = Thing.Of(game, null).Where(t => t.Kind is "flag" or "building").Select(t => (t.X, t.Y)).ToHashSet();
        var mapObject = game.Map.Objects.First(o => !taken.Contains((o.X, o.Y)));

        var road = RunProblem("name r 18 6 road", game);
        var thing = RunProblem($"name o {mapObject.X} {mapObject.Y} object", game);

        StringAssert.Contains(road, "the road has no thing ID");
        StringAssert.Contains(thing, "the object has no thing ID");
    }

    [TestMethod]
    public void ExpectOnAThingGoneWithNoEventSaysSo()
    {
        var current = Castle();
        var run = new PlayRun(
            Parse("name f 18 6 flag\nwait 1\nexpect f.x = 18"),
            () => StateOf(current),
            _out,
            _ => null
        );
        run.Advance(0);
        current = ThingHistoryTests.NewGame();
        run.Advance(1.0);

        StringAssert.Contains(Problem(run), "is gone, it has no event");
    }

    [TestMethod]
    public void ExpectOnAFieldTheThingDoesNotListNamesTheFieldAndTheLine()
    {
        var problem = RunProblem("name f 18 6 flag\nexpect f.job = 1", Castle());

        StringAssert.Contains(problem, "lists no job");
        StringAssert.Contains(problem, "flag 18,6");
    }

    [TestMethod]
    public void ExpectAtATickThatHasNotRunIsRefusedAndTheLastRunTickAnswers()
    {
        var game = Castle();
        var last = Math.Max(game.TickCount - 1, game.History.Events.LastTick);

        Assert.AreEqual("", RunProblem($"name c 15 5 building\nexpect c.x@{last} = 15", game));
        StringAssert.Contains(
            RunProblem($"name c 15 5 building\nexpect c.x@{last + 1} = 15", game),
            $"tick {last + 1} has not run"
        );
    }

    [TestMethod]
    public void ExpectAtATickBeforeTheFirstEventNamesTheTickItAppears()
    {
        var game = Castle();
        var first = game.History.EventsOf(game.History.FlagId(1))[0].Tick;

        Assert.AreEqual("", RunProblem($"name f 18 6 flag\nexpect f.x@{first} = 18", game));
        StringAssert.Contains(
            RunProblem($"name f 18 6 flag\nexpect f.x@{first - 1} = 18", game),
            $"first appears at tick {first}, after tick {first - 1}"
        );
    }

    [TestMethod]
    public void ExpectAtATickOfAThingWithNoEventSaysSo()
    {
        var current = Castle();
        var run = new PlayRun(
            Parse("name f 18 6 flag\nwait 1\nexpect f.x@0 = 18"),
            () => StateOf(current),
            _out,
            _ => null
        );
        run.Advance(0);
        current = ThingHistoryTests.NewGame();
        current.Tick();
        current.Tick();
        run.Advance(1.0);

        StringAssert.Contains(Problem(run), "flag#1.1 has no event");
    }

    [TestMethod]
    public void ExpectAtATickFailsWhenThePastStateLacksTheField()
    {
        var game = Castle();
        var first = game.History.EventsOf(game.History.FlagId(1))[0].Tick;

        StringAssert.Contains(RunProblem($"name f 18 6 flag\nexpect f.job@{first} = 1", game), "has no job");
    }

    [TestMethod]
    public void ExpectOfAListedValueThatIsNoNumberFailsAndNamesTheValue()
    {
        var game = Castle();
        Thing[] things = [new("settler", 1, 1, [("job", "carrier"), ("id", "settler#0.1")], null)];
        var state = StateOfSize(game, 1, things);
        var run = new PlayRun(Parse("name s 1 1 settler\nexpect s.job = 0"), () => state, _out, _ => null);
        run.Advance(1.0 / 30);

        StringAssert.Contains(Problem(run), "'carrier' is no number");
    }

    [TestMethod]
    [DataRow("true", true, 1.0)]
    [DataRow("false", true, 0.0)]
    [DataRow("0x1f", true, 31.0)]
    [DataRow("0X1F", true, 31.0)]
    [DataRow("-1", true, -1.0)]
    [DataRow("1.5", true, 1.5)]
    [DataRow("0x", false, 0.0)]
    [DataRow("0xzz", false, 0.0)]
    [DataRow("0x80000000", false, 0.0)]
    [DataRow("NaN", false, 0.0)]
    [DataRow("Infinity", false, 0.0)]
    [DataRow("1e309", false, 0.0)]
    [DataRow("True", false, 0.0)]
    public void NumericReadsExpectValuesOneWay(string text, bool ok, double value)
    {
        Assert.AreEqual(ok, NamedThings.Numeric(text, out var read));
        if (ok)
            Assert.AreEqual(value, read);
    }

    [TestMethod]
    public void ALinkValueWithAnAtSignIsRefusedWhenTheScriptIsRead()
    {
        var script = Parse("name a 18 6 flag\nexpect a.flag-id = foo@bar");

        Assert.HasCount(1, script.Problems);
        StringAssert.StartsWith(script.Problems[0], "play/t.play line 2: ");
        StringAssert.Contains(script.Problems[0], "foo@bar");
    }

    [TestMethod]
    public void AMarkLabelThatReadsAsATickIsRefusedWhenTheScriptIsRead()
    {
        var script = Parse("mark 3000000000");

        Assert.HasCount(1, script.Problems);
        StringAssert.StartsWith(script.Problems[0], "play/t.play line 1: ");
        StringAssert.Contains(script.Problems[0], "3000000000");
    }

    [TestMethod]
    public void AnEventsLineOfACarrierNamesTheFlagItWaitsAtAsOther()
    {
        var game = ThingHistoryTests.Laid();
        var carrier = game.History.EventsOf(game.History.SettlerId(0));
        Assert.IsTrue(carrier.Any(e => e.Other is not null), "a carrier event with another thing");

        Run("name c 11 11 settler\nevents c carrier", game);
        var text = File.ReadAllText(Path.Combine(_out, "carrier.events.txt"));

        StringAssert.Contains(text, $" other={carrier.First(e => e.Other is not null).Other} ");
        StringAssert.Contains(text, " other=flag#");
    }

    [TestMethod]
    public void AnEventsLineCarriesOtherOnlyForAnEventWithAnotherThing()
    {
        var game = ThingHistoryTests.NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 20, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0, 0, 0]);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        var slot = game.Flags.Select((flag, i) => (flag, i)).First(e => !e.flag.IsFree && e.flag.X == 18).i;
        var events = game.History.EventsOf(game.History.FlagId(slot));
        Assert.IsTrue(events.Any(e => e.Other is not null), "an event with another thing");
        Assert.IsTrue(events.Any(e => e.Other is null), "an event without one");

        Run("name f 18 6 flag\nevents f split", game);
        var lines = File.ReadAllText(Path.Combine(_out, "split.events.txt")).TrimEnd('\n').Split('\n');

        Assert.HasCount(events.Count, lines);
        for (var i = 0; i < lines.Length; i++)
        {
            if (events[i].Other is { } other)
                StringAssert.Contains(lines[i], $" other={other} ");
            else
                Assert.DoesNotContain(" other=", lines[i]);
        }
    }
}
