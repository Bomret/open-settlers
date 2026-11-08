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
}
