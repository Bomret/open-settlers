using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectDrawsTests
{
    static Map Generated() => MapViewState.Open("1234567812345678", 1).Map!;

    [TestMethod]
    public void EveryGeneratedObjectIsDrawnInRowMajorOrderWithTheEntryOfItsCode()
    {
        var map = Generated();

        var draws = ObjectDraws.Of(map);

        Assert.IsNotEmpty(map.Objects);
        CollectionAssert.AreEqual(
            map.Objects.Select(o => (o.Y, ObjectSprites.Entry(o.Code)!.Value)).ToList(),
            draws.Select(d => (d.Row, d.Entry)).ToList()
        );
    }

    [TestMethod]
    public void AnObjectIsDrawnSixteenEastAndEightSouthOfItsNodeLoweredByItsHeight()
    {
        var draws = ObjectDraws.Of(Generated());

        // Seed 1234567812345678, size 1. Object (3,0) code 13 on height 5: x = 3 * 32 + 16, y = 0 - 5 * 4 + 8.
        CollectionAssert.Contains(draws, new ObjectDraw(0, 1250, 112, -12));
        // Object (9,0) code 82 on height 29: x = 9 * 32 + 16, y = 0 - 29 * 4 + 8.
        CollectionAssert.Contains(draws, new ObjectDraw(0, 1324, 304, -108));
    }

    [TestMethod]
    public void TheCastleIsDrawnAtItsNodeInTheOrderOfItsRowAndLoweredByItsHeight()
    {
        // Emulator: site:1:1234,5678,9ABC lists 15,5 as a castle site of height 15.
        var map = MapGenerator.Generate(new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(TestProgram.Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        var draws = ObjectDraws.Of(map, game.Castle);

        // The castle's node 15,5 is levelled to 15: x = 15 * 32 - 5 * 16 + 16, y = 5 * 20 - 15 * 4 + 8. inferred: the
        // entry is the emulator's (peek:44fd2f:4, see ObjectGraphicsTests), but the offsets 16 and 8 are those of
        // the objects, inferred from the picture; no emulator or debugger run of the draw confirms them for the castle.
        var castle = draws.Single(draw => draw.Entry == ObjectSprites.CastleEntry);
        Assert.AreEqual(new ObjectDraw(5, ObjectSprites.CastleEntry, 416, 48), castle);
        CollectionAssert.AreEqual(
            draws.Select(draw => draw.Row).Order().ToList(),
            draws.Select(draw => draw.Row).ToList()
        );
    }

    [TestMethod]
    public void AHutIsDrawnLikeTheCastleAtItsNodeWithTheHutEntry()
    {
        // The hut at 16,2 stands tied to the flag at 17,3 (the road test world of HutTests); its node's height is read from
        // the map: x = 16 * 32 - 2 * 16 + 16, y = 2 * 20 - height * 4 + 8.
        var map = MapGenerator.Generate(new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var classes = new byte[ProgramTables.ClassCount];
        classes[1] = 3;
        var game = new Game(new ProgramTables(classes, TestProgram.Made().StartStock), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 17, 3));
        game.Tick();
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 16, 2));
        game.Tick();
        var height = map.Layer(MapLayer.Height)[map.Index(16, 2)] & Map.HeightMask;

        var draws = ObjectDraws.Of(map, game.Castle, game.Buildings);

        Assert.AreEqual(
            new ObjectDraw(2, ObjectSprites.WoodcutterEntry, 496, 48 - height * 4),
            draws.Single(draw => draw.Entry == ObjectSprites.WoodcutterEntry)
        );
        CollectionAssert.AreEqual(
            draws.Select(draw => draw.Row).Order().ToList(),
            draws.Select(draw => draw.Row).ToList()
        );
    }

    [TestMethod]
    public void AFlagIsDrawnLikeAnObjectWithTheFlagEntry()
    {
        // Emulator: site:1:1234,5678,9ABC lists 15,5 as a castle site of height 15; its flag stands at 16,6 on height 15:
        // x = 16 * 32 - 6 * 16 + 16, y = 6 * 20 - 15 * 4 + 8.
        var map = MapGenerator.Generate(new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(TestProgram.Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        var draws = ObjectDraws.Of(map, game.Castle);

        CollectionAssert.Contains(draws, new ObjectDraw(6, ObjectSprites.FlagEntry, 432, 68));
    }

    [TestMethod]
    public void AResourceIsNeverDrawn()
    {
        var map = Generated();

        Assert.IsNotEmpty(map.Deposits);
        Assert.HasCount(map.Objects.Count, ObjectDraws.Of(map));
    }
}
