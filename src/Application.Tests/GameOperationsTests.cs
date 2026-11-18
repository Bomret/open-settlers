using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The public operations of a game against the parts they read: the layout mode against the causes' flag, the removal query against
// the removal's own answer, and the supply's state against the supply. The bench's game on its flat map with the tables of the class of
// the same name in src/Adapters.Tests: the castle stands at 10,10 with its flag at 11,11; a flag at 14,11 is three steps east of it, a
// road joins them with its carrier on it, and the woodcutter's hut at 13,10 ties to that flag. The rest of the class, which builds its
// game from a layout, is in src/Adapters.Tests.
[TestClass]
public sealed class GameOperationsTests
{
    static Game NewGame(bool supply = false)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var tables = new RuleTables(
            classes,
            MadeProgram.MadeStartStock(),
            catalog: TestCatalog.StandIn(TestCatalog.Buildings(workers: MadeProgram.MadeWorkers(stonecutter: true)))
        );

        return GameBench.Create(tables, GameBench.Flat(), settlerSupply: supply, settlerRate: 40);
    }

    // The setup the layout of the class's other half builds, step by step, in the layout mode.
    static Game Built(bool supply = false)
    {
        var game = NewGame(supply);
        game.SetLayoutMode(true);
        Assert.IsTrue(GameBench.Apply(game, CommandKind.PlaceCastle, 10, 10));
        Assert.IsTrue(GameBench.Apply(game, CommandKind.PlaceFlag, 14, 11));
        Assert.IsTrue(GameBench.Apply(game, CommandKind.BuildRoad, 11, 11, [0, 0, 0]));
        Assert.IsTrue(game.PlaceFinishedBuilding(BuildingType.Woodcutter, 13, 10));
        Assert.IsNull(game.StandWorkerInFinishedHut(BuildingType.Woodcutter, 13, 10));
        Assert.IsNull(game.PlaceCarrierOnRoad(11, 11, 0));
        game.SetLayoutMode(false);

        return game;
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
    public void TheRemovalQueryAnswersWhatTheRemovalGivesAtEveryNode()
    {
        var game = Built();

        Assert.IsNull(game.RemovalCommandAt(11, 11), "the castle's flag is refused");
        var answers = new HashSet<int?>();
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

        var game = Built(supply: true);
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
