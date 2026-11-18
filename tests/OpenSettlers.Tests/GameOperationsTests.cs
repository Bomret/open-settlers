using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The public operations of a game that the adapters and the host use in place of its behavior objects: the values the digest
// reads, the setup of a layout, and the removal query. The map is flat grass: the castle stands at 10,10 with its flag at
// 11,11; a flag at 14,11 is three steps east of it, and the hut at 13,10 ties to that flag.
[TestClass]
public sealed class GameOperationsTests
{
    const string Setup = """
        "castle": {"x": 10, "y": 10},
        "flags": [{"x": 14, "y": 11}],
        "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
        "buildings": [{"type": "woodcutter", "x": 13, "y": 10}],
        "settlers": [{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}]
        """;

    static Layout Read(string extra)
    {
        var read = Layout.Read(LayoutTests.Text(extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!;
    }

    static Game NewGame(Layout layout, bool supply = false)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var tables = new RuleTables(
            classes,
            TestProgram.MadeStartStock(),
            workers: TestProgram.MadeWorkers(stonecutter: true)
        );

        return GameFactory.Create(tables, layout.BuildMap(), settlerSupply: supply, settlerRate: 40);
    }

    static Game Built()
    {
        var layout = Read(Setup);
        var game = NewGame(layout);
        Assert.IsNull(layout.Apply(game));

        return game;
    }

    [TestMethod]
    public void TheDigestValuesEqualWhatTheBehaviorsHoldAtEveryTick()
    {
        var game = Built();
        var occupied = false;

        for (var tick = 0; tick < 300; tick++)
        {
            game.Tick();

            Assert.AreEqual(game.Carriers.FlagCursor, game.CarrierFlagCursor);
            CollectionAssert.AreEqual(game.Applied.ToList(), game.AppliedCommands.ToList());
            Assert.AreEqual(game.Draws.Generator, game.RandomState);
            Assert.AreEqual(game.Hut.ObjectPassState, game.HutPassState);
            Assert.AreEqual(game.Pass.Phase, game.PassPhase);
            Assert.AreEqual(game.Pass.ScanCursor, game.PassScanCursor);
            Assert.AreEqual(game.Roster.LeavingCount, game.CastleLeavingCount);
            CollectionAssert.AreEqual(game.Roster.WaitingList.ToList(), game.CastleWaitingList.ToList());
            CollectionAssert.AreEqual(game.Movement.Occupied.ToList(), game.OccupiedMovementSlots.ToList());
            occupied |= game.OccupiedMovementSlots.Count > 0;
        }

        Assert.IsTrue(occupied, "the walkers occupy slots at some tick, so the list is not trivially empty");

        Assert.IsTrue(game.AppliedCommands.Count > 0);
    }

    [TestMethod]
    public void SettingLayoutModeSetsAndClearsTheCauseOfTheEvents()
    {
        var game = Built();

        game.SetLayoutMode(true);
        Assert.IsTrue(game.Causes.InLayout);

        game.SetLayoutMode(false);
        Assert.IsFalse(game.Causes.InLayout);
    }

    [TestMethod]
    public void ALayoutWhoseSetupStopsOnARefusalLeavesTheGameOutOfLayoutMode()
    {
        var layout = Read(
            """
            "castle": {"x": 10, "y": 10},
            "buildings": [{"type": "woodcutter", "x": -5, "y": 10}]
            """
        );
        var game = NewGame(layout);

        Assert.IsNotNull(layout.Apply(game), "the building's node is no site");

        Assert.IsFalse(game.Causes.InLayout);
    }

    [TestMethod]
    public void ThePlacingOperationsReturnTheReasonTheyRefuseAndNullWhenTheyPlace()
    {
        var game = Built();

        Assert.IsFalse(game.PlaceFinishedBuilding(BuildingType.Woodcutter, 10, 10), "the castle's node is no site");
        Assert.IsNotNull(game.PlaceCarrierOnRoad(0, 0, 0), "no road leaves a node without a flag");
        Assert.IsNull(
            game.StandWorkerInFinishedHut(BuildingType.Sawmill, 13, 10),
            "a sawmill's worker does not stand in it"
        );
    }

    [TestMethod]
    public void TheRemovalQueryAnswersWhatTheRemovalGivesAtEveryNode()
    {
        var game = Built();

        Assert.IsNull(game.RemovalCommandAt(11, 11), "the castle's flag is refused");
        var answers = new System.Collections.Generic.HashSet<int?>();
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                Assert.AreEqual(game.Removal.CommandAt(x, y), game.RemovalCommandAt(x, y));
                answers.Add(game.RemovalCommandAt(x, y));
            }
        }

        Assert.IsTrue(answers.Any(answer => answer is not null), "some node holds something to remove");
    }

    [TestMethod]
    public void TheSupplyStateEqualsWhatTheSupplyHoldsAndIsNullWithoutTheSupply()
    {
        Assert.IsNull(Built().SupplyState, "a game built without the supply has none");

        var layout = Read(Setup);
        var game = NewGame(layout, supply: true);
        Assert.IsNull(layout.Apply(game));
        for (var tick = 0; tick < 200; tick++)
        {
            game.Tick();
            var supply = game.Supply!;
            var state = game.SupplyState!;

            Assert.AreEqual(
                (supply.Period, supply.Countdown, supply.LastClock, supply.KnightAccumulator, supply.KnightsOwed),
                (state.Period, state.Countdown, state.LastClock, state.KnightAccumulator, state.KnightsOwed)
            );
            Assert.AreEqual((supply.Running, supply.Cap, supply.MayMake), (state.Running, state.Cap, state.MayMake));
        }
    }
}
