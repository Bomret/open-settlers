using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A settler is one object for its whole life: its job changes, its identity does not (CONTEXT.md, Job). The map is the seed
// 1234,5678,9ABC with the castle at 15,5, flags at 18,6 (slot 1), 19,4 (2), and 17,3 (3), and a hut at 16,2.
[TestClass]
public sealed class SettlerJobTests
{
    private static RuleTables Made()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return new RuleTables(
            classes,
            Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount).ToArray()
        );
    }

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    private static Game Town()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var game = GameFactory.Create(Made(), map);
        Do(game, CommandKind.PlaceCastle, 15, 5);
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            Do(game, CommandKind.PlaceFlag, x, y);
        }

        return game;
    }

    [TestMethod]
    public void ASettlerWithoutAJobThatTakesARoadBecomesACarrierAndKeepsItsThingId()
    {
        var game = Town();
        var jobless = game.Settlers.Where(settler => settler.Job == Job.Jobless).ToList();
        var ids = jobless.ToDictionary(
            settler => settler,
            settler => game.History.SettlerId(game.Settlers.ToList().IndexOf(settler))
        );

        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        for (var tick = 0; tick < 600; tick++)
        {
            game.Tick();
        }

        var carrier = game.Settlers.Single(settler => settler.Job == Job.Carrier);
        Assert.IsTrue(jobless.Contains(carrier), "the carrier is one of the settlers that had no job");
        Assert.AreEqual(ids[carrier], game.History.SettlerId(game.Settlers.ToList().IndexOf(carrier)));
    }

    [TestMethod]
    public void ABuilderWhoseBuildingIsRemovedBecomesACarrierAndKeepsItsThingId()
    {
        var game = Town();
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        Do(game, CommandKind.BuildRoad, 18, 6, [5, 5, 0]);
        Do(game, CommandKind.BuildRoad, 17, 3, [0, 1]);
        Do(game, CommandKind.PlaceBuilding, 16, 2);
        for (var tick = 0; tick < 400; tick++)
        {
            game.Tick();
        }

        var slot = game.Buildings[1].Worker;
        var worker = game.Settlers[slot];
        var id = game.History.SettlerId(slot);
        // The engine has no builder outside the castle, so the hut's woodcutter is given the builder's job: the test shows
        // that the removal of a building turns a builder worker into a carrier of the same settler, not a builder's path.
        worker.Job = Job.Builder;

        Do(game, CommandKind.RemoveBuilding, 16, 2);

        Assert.AreEqual(Job.Carrier, worker.Job);
        Assert.AreSame(worker, game.Settlers[slot]);
        Assert.AreEqual(id, game.History.SettlerId(slot));
    }
}
