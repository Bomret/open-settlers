using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The public operations of a game that the adapters and the host use in place of its behavior objects: the values the digest
// reads and the setup of a layout. The map is flat grass: the castle stands at 10,10 with its flag at 11,11; a flag at 14,11 is
// three steps east of it, and the hut at 13,10 ties to that flag. The layout mode, the removal query, and the supply's state
// against the parts they read are in the class of the same name in src/Application.Tests.
[TestClass]
public sealed class GameOperationsTests
{
    const string Setup = """
        "castle": {"x": 10, "y": 10},
        "flags": [{"x": 14, "y": 11}],
        "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
        "buildings": [{"type": "woodcutter-hut", "x": 13, "y": 10}],
        "settlers": [{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}]
        """;

    static Layout Read(string extra)
    {
        var read = Layout.Read(LayoutText.Text(extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!;
    }

    static Game NewGame(Layout layout, bool supply = false)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var tables = new RuleTables(
            classes,
            MadeProgram.MadeStartStock(),
            catalog: TestCatalog.StandIn(TestCatalog.Buildings(workers: MadeProgram.MadeWorkers(stonecutter: true)))
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
    public void TheDigestValuesMoveWithTheTicksAndTheAppliedCommandsAreReadOnly()
    {
        // Each value is a pass-through of a behavior's state, and the golden digests (GoldenTests) pin that state; this
        // test pins that the values are live, so a getter that returned a constant or a stale copy fails.
        var game = Built();
        var random = new System.Collections.Generic.HashSet<RandomGenerator> { game.RandomState };
        var phases = new System.Collections.Generic.HashSet<int> { game.PassPhase };
        var occupied = false;

        for (var tick = 0; tick < 300; tick++)
        {
            game.Tick();

            random.Add(game.RandomState);
            phases.Add(game.PassPhase);
            occupied |= game.OccupiedMovementSlots.Count > 0;
        }

        Assert.IsTrue(random.Count > 1, "the generator draws in the passes");
        Assert.IsTrue(phases.Count > 1, "the main pass runs through its phases");
        Assert.IsTrue(occupied, "the walkers occupy slots at some tick, so the list is not trivially empty");
        Assert.AreEqual(game.Revision, game.AppliedCommands.Count);
        Assert.IsTrue(game.AppliedCommands.Count > 0);
        Assert.IsNotInstanceOfType<System.Collections.Generic.List<Command>>(game.AppliedCommands);
    }

    // The cause the game records for the flag its command places on the node.
    static Cause CauseOfAFlagPlacedAt(Game game, int x, int y)
    {
        var events = game.History.Events.All.Count();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = x,
                Y = y,
            }
        );
        game.Tick();

        return game.History.Events.All.Skip(events).Last(e => e.Type == EventType.PlaceFlag).Cause;
    }

    [TestMethod]
    public void ALayoutWhoseSetupStopsOnARefusalLeavesTheGameOutOfLayoutMode()
    {
        var layout = Read(
            """
            "castle": {"x": 10, "y": 10},
            "buildings": [{"type": "woodcutter-hut", "x": -5, "y": 10}]
            """
        );
        var game = NewGame(layout);

        Assert.IsNotNull(layout.Apply(game), "the building's node is no site");

        Assert.AreNotEqual(Cause.Layout, CauseOfAFlagPlacedAt(game, 14, 11));
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
}
