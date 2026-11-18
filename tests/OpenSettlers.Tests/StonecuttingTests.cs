using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;
using static OpenSettlers.Tests.StonecuttingWorld;

namespace OpenSettlers.Tests;

// Tests of the stonecutter's own logic on made-up tables (no install), in the world of WoodcuttingTests: the castle at 15,5 with
// its flag at 16,6, the stonecutter's hut at 16,2 with its flag at 17,3 (slot 3), and the roads of that world. The generated
// trees and heaps are gone from the map; a heap stands where a test puts it. The castle's stonecutter is the settler of slot 12.
// The values the original gives are in StonecutterTests.
[TestClass]
public sealed class StonecuttingTests
{
    const int HutFlag = 3;
    const int WorkerSlot = 12;
    static readonly Good Stone = Good.Stone;

    static int Heaps(Map map) => Reach().Count(node => map.At(node.X, node.Y).Object.Kind == MapObjectKind.StoneHeap);

    static bool HoldsStone(Game game) => game.Flags[HutFlag].Places.Any(place => place.Good == Stone);

    [TestMethod]
    public void AStonecutterCutsAHeapOfHisReachAndLaysAStoneOnTheHutsFlag()
    {
        var game = World(0x4d, out var map);

        for (var tick = 0; tick < 6000 && !HoldsStone(game); tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(Stone, game.Flags[HutFlag].Places[0].Good, "the stone lies on the first place of the flag");
        var cut = Reach().Where(node => map.At(node.X, node.Y).Object.Code == 0x4e).ToArray();
        Assert.HasCount(1, cut, "one heap is one stage smaller");
        Assert.AreEqual(Heaps(map), Reach().Count(node => map.At(node.X, node.Y).Object.Code is 0x4d or 0x4e));
    }

    // With a turn at every pass the step back (part 2 of the cut) spans turns, and the turns after the first are on the node he
    // stepped back to, so the cut names the heap by the neighbour of that node.
    [TestMethod]
    public void EveryCutStoneEventOfACutNamesTheHeapEvenWhileTheStepBackSpansTurns()
    {
        var game = World(0x4d, out var map, everyPass: true);

        for (var tick = 0; tick < 6000 && !HoldsStone(game); tick++)
        {
            game.Tick();
        }

        var heap = Reach().Single(node => map.At(node.X, node.Y).Object.Code == 0x4e);
        var named = game
            .History.Events.All.Where(e => e.Type == EventType.CutStone)
            .Select(e => e.Other)
            .Distinct()
            .ToArray();

        Assert.HasCount(1, named, "every cut-stone event names the one heap");
        Assert.AreEqual(ThingId.OfObject("stone", heap.X, heap.Y).ToString(), named[0]);
    }

    [TestMethod]
    public void AHeapIsGoneAfterItsLastStoneAndTheNodeIsFreeAgain()
    {
        var game = World(0x4f, out var map);
        var reach = Reach().Where(node => map.At(node.X, node.Y).Object.Kind == MapObjectKind.StoneHeap).ToArray();

        for (var tick = 0; tick < 6000 && !HoldsStone(game); tick++)
        {
            game.Tick();
        }

        var gone = reach.Single(node => map.At(node.X, node.Y).Object.Kind != MapObjectKind.StoneHeap);
        Assert.AreEqual(MapObject.None, map.At(gone.X, gone.Y).Object, "no heap and nothing else stands there");
        Assert.IsFalse(map.At(gone.X, gone.Y).LakeMark, "the node is no longer marked");
        Assert.AreEqual(reach.Length - 1, Heaps(map), "no other heap was cut");
    }

    [TestMethod]
    public void WithNoHeapInReachTheWorkerWaitsInHisHutAndTheGameKeepsTicking()
    {
        var game = World(null, out _);

        for (var tick = 0; tick < 3000; tick++)
        {
            game.Tick();
        }

        var digest = game.Digest();
        var ticks = game.TickCount;
        game.Tick();

        Assert.IsInstanceOfType<LooksForStone>(
            game.Settlers[WorkerSlot].Activity,
            "he looks for a heap inside the hut"
        );
        Assert.AreEqual((HutX, HutY), (game.Settlers[WorkerSlot].X, game.Settlers[WorkerSlot].Y));
        Assert.IsTrue(game.Flags[HutFlag].Places.All(place => place.IsEmpty));
        Assert.AreEqual(ticks + 1, game.TickCount);
        Assert.AreNotEqual(digest, game.Digest(), "the game state moves on");
    }

    // The tables of ConstructionTables with a forester that needs two stones and no plank, the stone asking the class it asks there.
    static RuleTables SiteTables()
    {
        var needs = new byte[2 * TestCatalog.BuildingSlots];
        needs[(2 * TestCatalog.CodeOf(BuildingType.Forester)) + 1] = 2;

        return MadeProgram.MadeTables(
            buildingClass: 4,
            transportPriorities: [.. Enumerable.Range(1, TestCatalog.Names.Length).Select(good => (byte)good)],
            buildingSizes: ConstructionTables.Sizes(),
            buildingNeeds: needs,
            goodClasses: ConstructionTables.Classes(),
            distribution: ConstructionTables.Orders(),
            plankRankBase: 0xff,
            workers: Workers()
        );
    }

    [TestMethod]
    public void AStoneGoesOverTheRoadsToTheCastleWhereNoSiteAsksForIt()
    {
        var game = World(0x4d, out _, SiteTables());
        var stones = game.CastleStock.Stones;

        for (var tick = 0; tick < 12000 && game.CastleStock.Stones == stones; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(stones + 1, game.CastleStock.Stones, "the castle's stone count rises");
        Assert.IsFalse(HoldsStone(game), "the stone has left the flag");
    }

    [TestMethod]
    public void AStoneGoesToASiteThatAsksForStonesInsteadOfTheCastle()
    {
        var game = World(0x4d, out _, SiteTables());
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = 17,
                Y = 5,
                Building = BuildingType.Forester,
            }
        );
        game.Tick();
        var site = game.Buildings.Single(building => building.Type == BuildingType.Forester);
        var stones = game.CastleStock.Stones;

        for (var tick = 0; tick < 20000 && site.Site!.Stones < 2; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(2, site.Site!.Stones, "both stones the site asks for came from the stonecutter");
        Assert.AreEqual(stones, game.CastleStock.Stones, "none of them went to the castle");
    }
}
