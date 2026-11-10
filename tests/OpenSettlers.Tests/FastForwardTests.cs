using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The play script's fast-forward step: how it reads, and how it runs on a game (castle 15,5 with a woodcutter's hut at
// 16,2, as WoodcuttingTests builds it).
[TestClass]
public sealed class FastForwardTests
{
    const string Usage =
        "'fast-forward' takes <job> state <n> max <ticks>, <job> carrying max <ticks>, or until <condition> max <ticks>";

    static FastForwardStep Step(string text)
    {
        var script = PlayScript.Parse(text, "play/t.play");
        Assert.IsEmpty(script.Problems);

        return (FastForwardStep)script.Steps.Single();
    }

    static string Problem(string text)
    {
        var script = PlayScript.Parse(text, "play/t.play");
        Assert.IsEmpty(script.Steps);

        return script.Problems.Single();
    }

    static bool Cutting(Game game) => game.Settlers.Any(s => s is { Job.Code: Job.WoodcutterCode, Activity: Cuts });

    // The tick the woodcutter first cuts at, found on a game of its own.
    static long FirstCut()
    {
        var game = WoodcuttingTests.World(true, out _);
        while (!Cutting(game))
            game.Tick();

        return game.TickCount;
    }

    [TestMethod]
    public void AStepReadsItsJobItsStateAndItsLimit()
    {
        var step = Step("\nfast-forward woodcutter state 0x11 max 5000");

        Assert.AreEqual(
            new FastForwardStep
            {
                Line = 2,
                Job = Job.Woodcutter,
                State = 0x11,
                MaxTicks = 5000,
                Text = "fast-forward woodcutter state 0x11 max 5000",
            },
            step
        );
    }

    [TestMethod]
    public void ACarryingStepTakesNoState()
    {
        var step = Step("fast-forward carrier carrying max 7");

        Assert.AreEqual(
            new FastForwardStep
            {
                Line = 1,
                Job = Job.Carrier,
                State = null,
                MaxTicks = 7,
                Text = "fast-forward carrier carrying max 7",
            },
            step
        );
    }

    [TestMethod]
    [DataRow("17")]
    [DataRow("0x11")]
    [DataRow("0X11")]
    public void AStateTakesDecimalOrHex(string written)
    {
        Assert.AreEqual(17, Step($"fast-forward woodcutter state {written} max 1").State);
    }

    [TestMethod]
    public void AFailureNamesTheStepAsWritten()
    {
        var game = WoodcuttingTests.World(true, out _);

        var problem = Step("fast-forward woodcutter state 17 max 0").Run(game);

        Assert.AreEqual("fast-forward woodcutter state 17 max 0: not reached after 0 ticks", problem);
    }

    [TestMethod]
    public void AfterAFastForwardNoTickIsDueUntilTheNextShotIsTaken()
    {
        var game = WoodcuttingTests.World(true, out _);
        var pacer = new TickPacer();

        var problem = Step("fast-forward woodcutter state 0x11 max 5000").Apply(game, true, pacer);

        Assert.IsNull(problem);
        Assert.AreEqual(0, pacer.Due(2), "the held game ticks no frame");
        Assert.AreEqual(0, pacer.Due(2), "and no later frame either");
        pacer.ShotTaken();
        Assert.AreEqual(25, pacer.Due(2), "after the shot the game runs on, the held time not counted");
    }

    [TestMethod]
    public void TheMapViewRefusesTheStepAndHoldsNothing()
    {
        var pacer = new TickPacer();
        var run = new PlayRun(
            PlayScript.Parse("fast-forward woodcutter carrying max 5", "play/t.play"),
            () => new PlayState(5, 6, 1f, "1", 1),
            Path.Combine(Path.GetTempPath(), "ff-unused"),
            step => step is FastForwardStep f ? f.Apply(null, false, pacer) : null
        );

        run.Advance(1);

        Assert.IsTrue(run.Finished);
        Assert.AreEqual("play/t.play line 1: fast-forward needs the game view, use --game-view", run.Problems.Single());
        Assert.AreEqual(25, pacer.Due(2));
    }

    [TestMethod]
    public void AGameViewWithNoGameRefusesTheStep()
    {
        var problem = Step("fast-forward woodcutter carrying max 5").Apply(null, true, new TickPacer());

        Assert.AreEqual("fast-forward: no game is running", problem);
    }

    [TestMethod]
    [DataRow("fast-forward baker state 1 max 5", "'baker' is not woodcutter or carrier")]
    [DataRow("fast-forward woodcutter state zz max 5", "'zz' is not a whole number of zero or more")]
    [DataRow("fast-forward woodcutter state -1 max 5", "'-1' is not a whole number of zero or more")]
    [DataRow("fast-forward woodcutter state 0x max 5", "'0x' is not a whole number of zero or more")]
    [DataRow("fast-forward woodcutter carrying max -3", "'-3' is not a whole number of zero or more")]
    [DataRow("fast-forward woodcutter state 1", Usage)]
    [DataRow("fast-forward woodcutter state 1 max", Usage)]
    [DataRow("fast-forward woodcutter carrying", Usage)]
    [DataRow("fast-forward woodcutter sleeping 3 max 2", Usage)]
    [DataRow("fast-forward", Usage)]
    public void AMalformedStepIsAScriptProblemAndNeverAThrow(string text, string reason)
    {
        Assert.AreEqual("play/t.play line 1: " + reason, Problem(text));
    }

    [TestMethod]
    public void TheGameStopsAtTheFirstTickTheConditionHolds()
    {
        var first = FirstCut();
        var game = WoodcuttingTests.World(true, out _);
        var start = game.TickCount;

        var problem = Step("fast-forward woodcutter state 0x11 max 5000").Run(game);

        Assert.IsNull(problem);
        Assert.AreEqual(first, game.TickCount);
        Assert.IsGreaterThan(start, game.TickCount);
    }

    [TestMethod]
    public void AConditionThatHoldsAlreadyRunsNoTick()
    {
        var game = WoodcuttingTests.World(true, out _);
        Step("fast-forward woodcutter state 0x11 max 5000").Run(game);
        var at = game.TickCount;

        var problem = Step("fast-forward woodcutter state 0x11 max 5000").Run(game);

        Assert.IsNull(problem);
        Assert.AreEqual(at, game.TickCount);
    }

    [TestMethod]
    public void ReachingTheLimitFailsNamingTheStepAndTheTicks()
    {
        var game = WoodcuttingTests.World(true, out _);
        var start = game.TickCount;

        var problem = Step("fast-forward woodcutter state 0x11 max 3").Run(game);

        Assert.AreEqual("fast-forward woodcutter state 0x11 max 3: not reached after 3 ticks", problem);
        Assert.AreEqual(start + 3, game.TickCount);
    }

    [TestMethod]
    public void AWoodcutterCarriesTheLogOnTheWayBackToTheHut()
    {
        var game = WoodcuttingTests.World(true, out _);

        var problem = Step("fast-forward woodcutter carrying max 6000").Run(game);

        Assert.IsNull(problem);
        Assert.IsTrue(game.Flags[3].Places.All(place => place.IsEmpty), "the log is not on the flag yet");
    }

    [TestMethod]
    public void ACarrierCarriesAGoodOnItsRoad()
    {
        var game = WoodcuttingTests.World(true, out _);

        var problem = Step("fast-forward carrier carrying max 6000").Run(game);

        Assert.IsNull(problem);
        Assert.IsTrue(
            game.Settlers.Any(s => s is { Job.Code: Job.CarrierCode, Activity: WalksRoad { Load: not null } })
        );
    }

    // What a play run observes of the game alone: its ticks and the castle's logs.
    static PlayState Observed(Game game) => new(0, 0, 1, "", 1, game.TickCount, game.CastleStock.Logs);

    [TestMethod]
    public void AnUntilStepReadsAnExpectCondition()
    {
        var step = Step("fast-forward until logs >= 5 max 9000");

        Assert.AreEqual(
            new ExpectStep
            {
                Line = 1,
                Field = "logs",
                Op = ">=",
                Value = "5",
            },
            step.Until
        );
        Assert.IsNull(step.Job);
        Assert.AreEqual(9000, step.MaxTicks);
    }

    [TestMethod]
    public void AnUntilStepReadsACountCondition()
    {
        var step = Step("fast-forward until count carrier = 2 max 9000");

        Assert.IsInstanceOfType<ExpectCountStep>(step.Until);
        Assert.AreEqual(2, ((ExpectCountStep)step.Until).Count);
    }

    [TestMethod]
    public void AnUntilStepRefusesAFieldTheTicksDoNotChange()
    {
        Assert.AreEqual(
            "play/t.play line 1: 'zoom' does not change with the ticks, use a game field",
            Problem("fast-forward until zoom = 2 max 10")
        );
    }

    [TestMethod]
    public void AnUntilStepWithoutItsLimitIsTheUsage()
    {
        Assert.Contains(Usage, Problem("fast-forward until logs >= 5"));
    }

    [TestMethod]
    public void AnUntilStepRunsUntilTheLogReachesTheCastle()
    {
        var game = WoodcuttingTests.World(true, out _);
        var logs = game.CastleStock.Logs;

        var problem = Step($"fast-forward until logs > {logs} max 20000").Run(game, () => Observed(game));

        Assert.IsNull(problem);
        Assert.AreEqual(logs + 1, game.CastleStock.Logs);
    }
}
