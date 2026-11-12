using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The map view's click-to-command decision: each branch, with the command it issues. The game is the one of
// RoadsTests: the castle's flag at 16,6 and a second flag at 18,6, two ticks in.
[TestClass]
public sealed class ClickDecisionTests
{
    static readonly System.Func<int, int, int?> Refuses = (_, _) => null;

    static Command? Decide(
        ClickMode mode,
        Game? game,
        RoadDrawing? drawing = null,
        System.Func<int, int, int?>? removal = null
    )
    {
        return ClickDecision.Decide(mode, game, game?.Map, drawing, removal ?? Refuses, 20, 8);
    }

    [TestMethod]
    public void WithNoGameACastleClickIssuesNothing()
    {
        Assert.IsNull(Decide(ClickMode.None, null));
    }

    [TestMethod]
    public void WithNoMapACastleClickIssuesNothing()
    {
        var game = RoadsTests.TwoFlags();

        Assert.IsNull(ClickDecision.Decide(ClickMode.None, game, null, null, Refuses, 20, 8));
    }

    [TestMethod]
    public void WithNoButtonPressedAClickPlacesTheCastleAtTheCurrentTickForPlayerZero()
    {
        var game = RoadsTests.TwoFlags();

        var command = Decide(ClickMode.None, game);

        Assert.AreEqual(new Command(game.TickCount, 0, CommandKind.PlaceCastle, 20, 8), command);
        Assert.AreNotEqual(0, game.TickCount);
    }

    [TestMethod]
    public void WithTheFlagButtonPressedAClickPlacesAFlag()
    {
        var game = RoadsTests.TwoFlags();

        var command = Decide(ClickMode.None with { Flag = true }, game);

        Assert.AreEqual(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 20, 8), command);
    }

    [TestMethod]
    public void WithABuildingButtonPressedAClickPlacesThatBuildingInsteadOfAFlag()
    {
        var game = RoadsTests.TwoFlags();
        var mode = ClickMode.None with { Flag = true, Buildings = [BuildingType.Sawmill] };

        var command = Decide(mode, game);

        Assert.AreEqual(
            new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 20, 8, Building: BuildingType.Sawmill),
            command
        );
    }

    [TestMethod]
    public void WithTwoBuildingButtonsPressedAClickPlacesTheFirstOne()
    {
        var game = RoadsTests.TwoFlags();
        var mode = ClickMode.None with { Buildings = [BuildingType.Woodcutter, BuildingType.Sawmill] };

        var command = Decide(mode, game);

        Assert.AreEqual(BuildingType.Woodcutter, command!.Value.Building);
    }

    [TestMethod]
    public void InRemoveModeAClickIssuesTheRemovalTheCheckGivesAtTheCurrentTick()
    {
        var game = RoadsTests.TwoFlags();
        var mode = ClickMode.None with { Remove = true, Flag = true };

        var command = Decide(mode, game, removal: (_, _) => CommandKind.RemoveRoad);

        Assert.AreEqual(new Command(game.TickCount, 0, CommandKind.RemoveRoad, 20, 8), command);
    }

    [TestMethod]
    public void InRemoveModeARefusedRemovalIssuesNothingAndDoesNotFallThroughToPlacing()
    {
        var game = RoadsTests.TwoFlags();
        var mode = ClickMode.None with { Remove = true, Flag = true, Buildings = [BuildingType.Sawmill] };

        Assert.IsNull(Decide(mode, game));
    }

    [TestMethod]
    public void InRoadModeADrawingThatDoesNotReachAFlagIssuesNothing()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        var mode = ClickMode.None with { Road = true, Flag = true };

        Assert.IsNull(ClickDecision.Decide(mode, game, game.Map, drawing, Refuses, 16, 6));
        Assert.IsNull(ClickDecision.Decide(mode, game, game.Map, drawing, Refuses, 17, 6));
        Assert.IsTrue(drawing.Active);
    }

    [TestMethod]
    public void InRoadModeADrawingThatReachesAFlagIssuesTheBuildRoadCommandAtTheCurrentTick()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        var mode = ClickMode.None with { Road = true };
        ClickDecision.Decide(mode, game, game.Map, drawing, Refuses, 16, 6);
        ClickDecision.Decide(mode, game, game.Map, drawing, Refuses, 17, 6);

        var command = ClickDecision.Decide(mode, game, game.Map, drawing, Refuses, 18, 6);

        Assert.AreEqual(new Command(game.TickCount, 0, CommandKind.BuildRoad, 16, 6, [0, 0]), command);
    }

    [TestMethod]
    public void InRoadModeWithNoRoadBeingDrawnTheClickFallsThroughToTheLaterChoices()
    {
        var game = RoadsTests.TwoFlags();

        var flag = Decide(ClickMode.None with { Road = true, Flag = true }, game);
        var castle = Decide(ClickMode.None with { Road = true }, game);
        var building = Decide(ClickMode.None with { Road = true, Buildings = [BuildingType.Sawmill] }, game);
        var removal = Decide(
            ClickMode.None with
            {
                Road = true,
                Remove = true,
            },
            game,
            removal: (_, _) => CommandKind.RemoveFlag
        );

        Assert.AreEqual(CommandKind.PlaceFlag, flag!.Value.Kind);
        Assert.AreEqual(CommandKind.PlaceCastle, castle!.Value.Kind);
        Assert.AreEqual(BuildingType.Sawmill, building!.Value.Building);
        Assert.AreEqual(CommandKind.RemoveFlag, removal!.Value.Kind);
    }
}
