using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The causes of the woodcutter's work events (ADR 0011) where the walk home is begun or a command cause is carried, through members
// internal to the application: the bench's game on the made-up tables of LayoutGames and its flat map, laid as layouts/woodcutter.jsonc
// lays it. The woodcutter is in his hut at 13,10 with trees to the east (16,10, 17,11, 18,10); the castle stands at 10,10 and a road
// runs three steps east from its flag to the hut's flag at 14,11. The rest of the class is in src/Adapters.Tests.
[TestClass]
public sealed class WoodcuttingHistoryTests
{
    static Game Laid()
    {
        var map = GameBench.Flat();
        foreach (var (x, y) in new[] { (16, 10), (17, 11), (18, 10) })
        {
            map.At(x, y).Object = new MapObject { Code = 8 };
        }

        var game = GameBench.Create(MadeProgram.MadeTables(), map);
        // The layout's entries, as Layout.Apply builds them: each command applied at once, in the layout mode.
        game.SetLayoutMode(true);
        Assert.IsTrue(GameBench.Apply(game, CommandKind.PlaceCastle, 10, 10));
        Assert.IsTrue(GameBench.Apply(game, CommandKind.PlaceFlag, 14, 11));
        Assert.IsTrue(GameBench.Apply(game, CommandKind.BuildRoad, 11, 11, [0, 0, 0]));
        Assert.IsTrue(game.PlaceFinishedBuilding(BuildingType.Woodcutter, 13, 10));
        Assert.IsNull(game.StandWorkerInFinishedHut(BuildingType.Woodcutter, 13, 10));
        game.SetLayoutMode(false);

        return game;
    }

    static Settler Woodcutter(Game game) => game.Settlers.Single(s => s.Job == new Woodcutter());

    static void RunTrip(Game game)
    {
        var laid = false;
        for (var i = 0; i < 4000 && !(laid && Woodcutter(game).Activity is Door); i++)
        {
            game.Tick();
            laid |= game.History.Events.All.Any(e => e.Type == EventType.PutGood);
        }
    }

    [TestMethod]
    public void AWoodcutterOnTheWalkHomeRecordsItsWorkEventsWithTheCauseWalkHome()
    {
        var game = Laid();
        var slot = game.Settlers.ToList().FindIndex(s => s.Job == new Woodcutter());
        var id = game.History.SettlerId(slot);
        for (var i = 0; i < 4000 && Woodcutter(game).Activity is not LooksForTree; i++)
        {
            game.Tick();
        }

        Assert.IsInstanceOfType<LooksForTree>(Woodcutter(game).Activity);
        // No command or removal rule sends a woodcutter home without a carried cause, so the walk home is begun directly.
        game.WalkHome.Begin(slot);
        var before = game.History.EventsOf(id).Count;
        for (var i = 0; i < 30; i++)
        {
            game.Tick();
        }

        var recorded = game.History.EventsOf(id).Skip(before).ToList();
        Assert.IsNotEmpty(recorded);
        Assert.IsTrue(
            recorded.All(e => e.Cause == Cause.WalkHome),
            string.Join(", ", recorded.Select(e => $"{e.Type}/{e.Cause}"))
        );
    }

    [TestMethod]
    public void AWoodcutterCarryingACommandCauseRecordsItsWorkEventsWithIt()
    {
        var game = Laid();
        var slot = game.Settlers.ToList().FindIndex(s => s.Job == new Woodcutter());
        var id = game.History.SettlerId(slot);
        var command = Cause.Placed(game.History.BuildingId(1));
        game.History.CarryCause(slot, command);
        RunTrip(game);

        var work = game
            .History.EventsOf(id)
            .Where(e => e.Type is EventType.SearchTree or EventType.StepOut or EventType.CutTree)
            .ToList();
        Assert.IsNotEmpty(work);
        Assert.IsTrue(work.All(e => e.Cause == command));
    }
}
