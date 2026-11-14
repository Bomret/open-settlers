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

    static FastForwardUntilStep UntilStep(string text) => (FastForwardUntilStep)Step(text);

    // The state a job step is handed: it reads the game alone, so building a state fails the test.
    static PlayState Unread(bool things) => throw new AssertFailedException("a job step builds no play state");

    static string Problem(string text)
    {
        var script = PlayScript.Parse(text, "play/t.play");
        Assert.IsEmpty(script.Steps);

        return script.Problems.Single();
    }

    static bool Cutting(Game game) => game.Settlers.Any(s => s is { Job.Code: Woodcutter.Number, Activity: Cuts });

    // The tick the woodcutter first cuts at, found on a game of its own.
    static long FirstCut()
    {
        var game = WoodcuttingTests.World(true, out _);
        while (!Cutting(game))
        {
            game.Tick();
        }

        return game.TickCount;
    }

    [TestMethod]
    public void AStepReadsItsJobItsStateAndItsLimit()
    {
        var step = Step("\nfast-forward woodcutter state 0x11 max 5000");

        Assert.AreEqual(
            new FastForwardJobStep
            {
                Line = 2,
                Job = new Woodcutter(),
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
            new FastForwardJobStep
            {
                Line = 1,
                Job = new Carrier(),
                State = null,
                MaxTicks = 7,
                Text = "fast-forward carrier carrying max 7",
            },
            step
        );
    }

    [TestMethod]
    public void ABuilderStateStepParsesAndAStonecutterCarryingStepParses()
    {
        Assert.AreEqual(new Builder(), ((FastForwardJobStep)Step("fast-forward builder state 9 max 10")).Job);
        Assert.AreEqual(9, ((FastForwardJobStep)Step("fast-forward builder state 9 max 10")).State);

        var carrying = (FastForwardJobStep)Step("fast-forward stonecutter carrying max 10");

        Assert.AreEqual(new Stonecutter(), carrying.Job);
        Assert.IsNull(carrying.State);
    }

    [TestMethod]
    public void AJobNameTheTableHoldsStandsForItsJob()
    {
        var expected = new (string Name, Job Job)[]
        {
            ("carrier", new Carrier()),
            ("digger", new Digger()),
            ("builder", new Builder()),
            ("woodcutter", new Woodcutter()),
            ("stonecutter", new Stonecutter()),
            ("forester", new Forester()),
        };

        foreach (var (name, job) in expected)
        {
            Assert.AreEqual(job, ((FastForwardJobStep)Step($"fast-forward {name} state 1 max 1")).Job, name);
        }
    }

    static bool CarriesAStone(Game game) =>
        game.Settlers.Any(s =>
            s.Job == new Stonecutter() && s.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: true } }
        );

    // Ticks the reference game until the state holds, and fails where it does not within the ticks the step under test is given.
    static void TickUntil(Game reference, System.Func<Game, bool> holds, int max, string what)
    {
        for (var tick = 0; !holds(reference); tick++)
        {
            if (tick == max)
            {
                Assert.Fail($"the reference game did not reach {what} in {max} ticks");
            }

            reference.Tick();
        }
    }

    [TestMethod]
    public void AStonecutterCarriesTheStoneOnTheWayBackToTheHut()
    {
        var reference = StonecuttingTests.World(0x4d, out _);
        TickUntil(reference, CarriesAStone, 6000, "a stonecutter carrying a stone");
        var game = StonecuttingTests.World(0x4d, out _);

        var problem = Step("fast-forward stonecutter carrying max 6000").Run(game, Unread);

        Assert.IsNull(problem);
        Assert.AreEqual(reference.TickCount, game.TickCount);
        Assert.IsTrue(CarriesAStone(game));
    }

    // The sawmiller holds the plank from the tick he stops sawing: in the door (state 0xb) where the flag has no room, and on the
    // step out of it (state 5 before 0xd).
    static bool CarriesAPlank(Game game) =>
        game.Settlers.Any(s =>
            s.Job == new Sawmiller() && s.Activity is StepsOutWithGood or WaitsOutStep { Then: PutsDownGood }
        );

    // A sawmill with a log laid at the castle's flag, its sawmiller waiting inside (SawmillTests' world).
    static Game SawmillWithALog()
    {
        var (game, _) = SawmillTests.World();
        SawmillTests.SawmillerInside(game);
        game.Goods.LayGood(0, 0, Good.Log.Code);

        return game;
    }

    [TestMethod]
    public void ASawmillerCarriesThePlankOutOfTheSawmill()
    {
        var reference = SawmillWithALog();
        TickUntil(reference, CarriesAPlank, 12000, "a sawmiller carrying a plank");
        var game = SawmillWithALog();

        var problem = Step("fast-forward sawmiller carrying max 12000").Run(game, Unread);

        Assert.IsNull(problem);
        Assert.AreEqual(reference.TickCount, game.TickCount);
        Assert.IsTrue(CarriesAPlank(game));
    }

    [TestMethod]
    public void ABuilderIsInStateNineAtTheFirstTickItBuilds()
    {
        static bool Builds(Game game) => game.Settlers.Any(s => s.Job == new Builder() && s.Activity.Code == 9);
        var (reference, _) = BuilderTests.World();
        TickUntil(reference, Builds, 3000, "a builder in state 9");
        var (game, _) = BuilderTests.World();

        var problem = Step("fast-forward builder state 9 max 3000").Run(game, Unread);

        Assert.IsNull(problem);
        Assert.AreEqual(reference.TickCount, game.TickCount);
        Assert.IsTrue(Builds(game));
    }

    [TestMethod]
    public void AnUnknownJobNameIsRefusedWithTheNamesTheTableHolds()
    {
        var problem = Problem("fast-forward miller state 1 max 5");

        Assert.StartsWith("play/t.play line 1: 'miller' is not a job name, use ", problem);
        foreach (var name in new[] { "carrier", "digger", "builder", "woodcutter", "stonecutter", "forester" })
        {
            Assert.Contains(name, problem);
        }
    }

    [TestMethod]
    public void OnlyACarriersLoadedRoadWalkAndAFreeWalkBackCarryingCarryAGood()
    {
        var road = new Heading { Value = 0 };
        var load = new Load { Good = Good.Log, Destination = 0 };
        Assert.IsTrue(new WalksRoad { Load = load, Heading = road }.Carries);
        Assert.IsFalse(new WalksRoad { Load = null, Heading = road }.Carries);
        Assert.IsFalse(
            new WalksRoad
            {
                Load = new Load { Good = Good.None, Destination = 0 },
                Heading = road,
            }.Carries
        );
        Assert.IsTrue(
            new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = default,
                    Purpose = new BackToHut { Carrying = true },
                },
            }.Carries
        );
        Assert.IsFalse(
            new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = default,
                    Purpose = new BackToHut { Carrying = false },
                },
            }.Carries
        );
        Assert.IsFalse(new WaitsInCastle().Carries);
    }

    [TestMethod]
    [DataRow("17")]
    [DataRow("0x11")]
    [DataRow("0X11")]
    public void AStateTakesDecimalOrHex(string written)
    {
        Assert.AreEqual(17, ((FastForwardJobStep)Step($"fast-forward woodcutter state {written} max 1")).State);
    }

    [TestMethod]
    public void AFailureNamesTheStepAsWritten()
    {
        var game = WoodcuttingTests.World(true, out _);

        var problem = Step("fast-forward woodcutter state 17 max 0").Run(game, Unread);

        Assert.AreEqual("fast-forward woodcutter state 17 max 0: not reached after 0 ticks", problem);
    }

    [TestMethod]
    public void AfterAFastForwardNoTickIsDueUntilTheNextShotIsTaken()
    {
        var game = WoodcuttingTests.World(true, out _);
        var pacer = new TickPacer();

        var problem = Step("fast-forward woodcutter state 0x11 max 5000").Apply(game, true, pacer, Unread);

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
            () =>
                new PlayState
                {
                    CameraX = 5,
                    CameraY = 6,
                    Zoom = 1f,
                    Seed = "1",
                    Size = 1,
                },
            Path.Combine(Path.GetTempPath(), "ff-unused"),
            step => step is FastForwardStep f ? f.Apply(null, false, pacer, Unread) : null
        );

        run.Advance(1);

        Assert.IsTrue(run.Finished);
        Assert.AreEqual("play/t.play line 1: fast-forward needs the game view, use --game-view", run.Problems.Single());
        Assert.AreEqual(25, pacer.Due(2));
    }

    [TestMethod]
    public void AGameViewWithNoGameRefusesTheStep()
    {
        var problem = Step("fast-forward woodcutter carrying max 5").Apply(null, true, new TickPacer(), Unread);

        Assert.AreEqual("fast-forward: no game is running", problem);
    }

    [TestMethod]
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

        var problem = Step("fast-forward woodcutter state 0x11 max 5000").Run(game, Unread);

        Assert.IsNull(problem);
        Assert.AreEqual(first, game.TickCount);
        Assert.IsGreaterThan(start, game.TickCount);
    }

    [TestMethod]
    public void AConditionThatHoldsAlreadyRunsNoTick()
    {
        var game = WoodcuttingTests.World(true, out _);
        Step("fast-forward woodcutter state 0x11 max 5000").Run(game, Unread);
        var at = game.TickCount;

        var problem = Step("fast-forward woodcutter state 0x11 max 5000").Run(game, Unread);

        Assert.IsNull(problem);
        Assert.AreEqual(at, game.TickCount);
    }

    [TestMethod]
    public void ReachingTheLimitFailsNamingTheStepAndTheTicks()
    {
        var game = WoodcuttingTests.World(true, out _);
        var start = game.TickCount;

        var problem = Step("fast-forward woodcutter state 0x11 max 3").Run(game, Unread);

        Assert.AreEqual("fast-forward woodcutter state 0x11 max 3: not reached after 3 ticks", problem);
        Assert.AreEqual(start + 3, game.TickCount);
    }

    [TestMethod]
    public void AWoodcutterCarriesTheLogOnTheWayBackToTheHut()
    {
        var game = WoodcuttingTests.World(true, out _);

        var problem = Step("fast-forward woodcutter carrying max 6000").Run(game, Unread);

        Assert.IsNull(problem);
        Assert.IsTrue(game.Flags[3].Places.All(place => place.IsEmpty), "the log is not on the flag yet");
    }

    [TestMethod]
    public void ACarrierCarriesAGoodOnItsRoad()
    {
        var game = WoodcuttingTests.World(true, out _);

        var problem = Step("fast-forward carrier carrying max 6000").Run(game, Unread);

        Assert.IsNull(problem);
        Assert.IsTrue(
            game.Settlers.Any(s => s is { Job.Code: Carrier.Number, Activity: WalksRoad { Load: not null } })
        );
    }

    // What a play run observes of the game alone: its ticks and the castle's logs.
    static PlayState Observed(Game game) =>
        new()
        {
            CameraX = 0,
            CameraY = 0,
            Zoom = 1,
            Seed = "",
            Size = 1,
            Ticks = game.TickCount,
            Logs = game.CastleStock.Logs,
        };

    [TestMethod]
    public void AnUntilStepReadsAnExpectCondition()
    {
        var step = UntilStep("fast-forward until logs >= 5 max 9000");

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
        Assert.AreEqual(9000, step.MaxTicks);
    }

    [TestMethod]
    public void AnUntilStepReadsACountCondition()
    {
        var step = UntilStep("fast-forward until count carrier = 2 max 9000");

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
    public void AnUntilStepOnAnUnknownFieldListsOnlyTheGameFields()
    {
        Assert.AreEqual(
            "play/t.play line 1: unknown field 'nosuch', use ticks, logs, planks, stones, flag-goods:<column>,<row>",
            Problem("fast-forward until nosuch >= 1 max 5")
        );
    }

    [TestMethod]
    [DataRow("fast-forward until count carrier = max 5", "'expect-count' takes 3 arguments, found 2")]
    [DataRow("fast-forward until count carrier = x max 5", "'x' is not a whole number of zero or more")]
    public void AnUntilStepRefusesAMalformedCondition(string text, string reason)
    {
        Assert.StartsWith("play/t.play line 1: " + reason, Problem(text));
    }

    [TestMethod]
    public void AnUntilStepWithoutItsLimitIsTheUsage()
    {
        Assert.Contains(Usage, Problem("fast-forward until logs >= 5"));
    }

    [TestMethod]
    public void AnUntilStepOnAGameFieldListsNoThingsAndRunsUntilTheLogReachesTheCastle()
    {
        var game = WoodcuttingTests.World(true, out _);
        var logs = game.CastleStock.Logs;
        var asked = new System.Collections.Generic.List<bool>();

        var problem = Step($"fast-forward until logs >= {logs + 1} max 20000")
            .Run(
                game,
                things =>
                {
                    asked.Add(things);

                    return Observed(game);
                }
            );

        Assert.IsNull(problem);
        Assert.AreEqual(logs + 1, game.CastleStock.Logs);
        Assert.IsNotEmpty(asked);
        Assert.IsTrue(asked.All(things => !things), "no tick lists the things");
    }

    [TestMethod]
    public void AnUntilFlagGoodsStepStopsAtTheFirstTickTheFlagHoldsTheCount()
    {
        static bool Holds(Game game) => game.Flags[3].Places.Any(place => !place.IsEmpty);
        var reference = WoodcuttingTests.World(true, out _);
        while (!Holds(reference))
        {
            reference.Tick();
        }

        var game = WoodcuttingTests.World(true, out _);
        var flag = game.Flags[3];

        var problem = Step($"fast-forward until flag-goods:{flag.X},{flag.Y} = 1 max 6000")
            .Run(
                game,
                _ =>
                    Observed(game) with
                    {
                        FlagGoods = ObjectDraws
                            .FlagGoodsOf(game)
                            .ToDictionary(e => e.Key, e => e.Value.Count(good => !good.IsNone)),
                    }
            );

        Assert.IsNull(problem);
        Assert.AreEqual(reference.TickCount, game.TickCount);
    }

    // What a play run observes of the game with its things listed where the condition asks for them.
    static PlayState Listing(Game game, bool things, System.Collections.Generic.List<bool> asked)
    {
        asked.Add(things);

        return Observed(game) with
        {
            Things = things ? Thing.Of(game, null) : null,
        };
    }

    [TestMethod]
    public void AnUntilCountSeesTheThingsAtEachTickAndStopsAtTheFirstTickTheCountHolds()
    {
        var first = FirstCut();
        var game = WoodcuttingTests.World(true, out _);
        var asked = new System.Collections.Generic.List<bool>();

        var problem = Step("fast-forward until count cuts-tree >= 1 max 5000")
            .Run(game, things => Listing(game, things, asked));

        Assert.IsNull(problem);
        Assert.AreEqual(first, game.TickCount);
        Assert.IsTrue(asked.All(things => things), "every tick lists the things");
    }

    [TestMethod]
    public void AnUntilCountBelowStopsWhenTheCountDropsAndNotAtOnce()
    {
        var game = WoodcuttingTests.World(true, out _);
        Step("fast-forward woodcutter state 0x11 max 5000").Run(game, Unread);
        var cutting = game.TickCount;
        var asked = new System.Collections.Generic.List<bool>();

        var problem = Step("fast-forward until count cuts-tree < 1 max 5000")
            .Run(game, things => Listing(game, things, asked));

        Assert.IsNull(problem);
        Assert.IsGreaterThan(cutting, game.TickCount);
        Assert.IsFalse(Cutting(game));
    }

    [TestMethod]
    public void AnExpectReadsNoThingsAndAnExpectCountDoes()
    {
        Assert.IsFalse(UntilStep("fast-forward until logs >= 5 max 1").Until.ReadsThings);
        Assert.IsTrue(UntilStep("fast-forward until count carrier = 2 max 1").Until.ReadsThings);
    }

    [TestMethod]
    public void AnUntilConditionThatHoldsAlreadyRunsNoTickAndBuildsOneState()
    {
        var game = WoodcuttingTests.World(true, out _);
        var start = game.TickCount;
        var asked = new System.Collections.Generic.List<bool>();

        var problem = Step($"fast-forward until logs >= {game.CastleStock.Logs} max 5000")
            .Run(game, things => Listing(game, things, asked));

        Assert.IsNull(problem);
        Assert.AreEqual(start, game.TickCount);
        Assert.HasCount(1, asked);
    }

    [TestMethod]
    public void AnUntilMissNamesTheStepAndTheTicks()
    {
        var game = WoodcuttingTests.World(true, out _);

        var problem = Step("fast-forward until logs >= 99 max 3").Run(game, _ => Observed(game));

        Assert.AreEqual("fast-forward until logs >= 99 max 3: not reached after 3 ticks", problem);
    }

    [TestMethod]
    public void TheMapViewAndAMissingGameRefuseAnUntilStepBeforeAnyStateIsBuilt()
    {
        var step = Step("fast-forward until logs >= 5 max 5");
        var built = 0;
        PlayState State(bool things)
        {
            built++;

            return new PlayState
            {
                CameraX = 0,
                CameraY = 0,
                Zoom = 1,
                Seed = "",
                Size = 1,
            };
        }

        Assert.AreEqual(
            "fast-forward needs the game view, use --game-view",
            step.Apply(WoodcuttingTests.World(true, out _), false, new TickPacer(), State)
        );
        Assert.AreEqual("fast-forward: no game is running", step.Apply(null, true, new TickPacer(), State));
        Assert.AreEqual(0, built);
    }
}
