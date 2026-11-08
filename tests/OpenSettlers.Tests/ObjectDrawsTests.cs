using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectDrawsTests
{
    static Map Generated() => MapViewState.Open(TestProgram.Tables(), "1234567812345678", 1).Map!;

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
    public void AnObjectIsDrawnAtItsNodeLoweredByItsHeight()
    {
        var draws = ObjectDraws.Of(Generated());

        // Seed 1234567812345678, size 1. Object (3,0) code 13 on height 5: x = 3 * 32, y = 0 - 5 * 4.
        CollectionAssert.Contains(draws, new ObjectDraw(0, 1250, 96, -20));
        // Object (9,0) code 82 on height 29: x = 9 * 32, y = 0 - 29 * 4.
        CollectionAssert.Contains(draws, new ObjectDraw(0, 1324, 288, -116));
    }

    [TestMethod]
    public void TheCastleIsDrawnAtItsNodeInTheOrderOfItsRowAndLoweredByItsHeight()
    {
        // Emulator: site:1:1234,5678,9ABC lists 15,5 as a castle site of height 15.
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(TestProgram.Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        var draws = ObjectDraws.Of(map, game.Castle);

        // The castle's node 15,5 is levelled to 15: x = 15 * 32 - 5 * 16, y = 5 * 20 - 15 * 4. inferred: the
        // entry is the emulator's (peek:44fd2f:4, see ObjectGraphicsTests), but the draw at the node is that of the
        // objects, inferred from the picture; no emulator or debugger run of the draw confirms it for the castle.
        var castle = draws.Single(draw => draw.Entry == ObjectSprites.CastleEntry);
        Assert.AreEqual(new ObjectDraw(5, ObjectSprites.CastleEntry, 400, 40), castle);
        CollectionAssert.AreEqual(
            draws.Select(draw => draw.Row).Order().ToList(),
            draws.Select(draw => draw.Row).ToList()
        );
    }

    [TestMethod]
    public void AHutIsDrawnLikeTheCastleAtItsNodeWithTheHutEntry()
    {
        // The hut at 16,2 stands tied to the flag at 17,3 (the road test world of HutTests); its node's height is read from
        // the map: x = 16 * 32 - 2 * 16, y = 2 * 20 - height * 4.
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var game = new Game(new RuleTables(classes, TestProgram.Made().StartStock), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 17, 3));
        game.Tick();
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 16, 2));
        game.Tick();
        var height = map.At(16, 2).Height;

        var draws = ObjectDraws.Of(map, game.Castle, game.Buildings);

        Assert.AreEqual(
            new ObjectDraw(2, ObjectSprites.WoodcutterEntry, 480, 40 - height * 4),
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
        // x = 16 * 32 - 6 * 16, y = 6 * 20 - 15 * 4.
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(TestProgram.Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        var draws = ObjectDraws.Of(map, game.Castle);

        CollectionAssert.Contains(draws, new ObjectDraw(6, ObjectSprites.FlagEntry, 416, 60));
    }

    [TestMethod]
    public void AFlagIsDrawnOnTheNodeWhereItsRoadsMeet()
    {
        // In the original's picture the roads meet at the foot of a flag's pole. Roads and the pick use the node's
        // point, Camera.NodePosition lifted by the node's height, so the flag's draw position is that point.
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(TestProgram.Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        var (x, y) = Camera.NodePosition(16, 6);
        var lift = map.At(16, 6).Height * TerrainTriangles.HeightStep;

        var flag = ObjectDraws.Of(map, game.Castle).Single(draw => draw.Entry == ObjectSprites.FlagEntry);

        Assert.AreEqual((x, y - lift), (flag.X, flag.Y));
    }

    // **Inferred:** the offsets and the sprite of a good come from the decompile of 0x44db26 and 0x44de9a; no emulator or
    // debugger run of the draw confirms them (docs/rules/woodcutting.md, Drawing).
    // The castle's flag stands at 16,6 on height 15 and is drawn at 416,60 (AFlagIsDrawnLikeAnObjectWithTheFlagEntry).
    static List<ObjectDraw> FlagDrawsWith(params int[] goods)
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(TestProgram.Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        var flagGoods = new Dictionary<(int, int), IReadOnlyList<int>> { [(16, 6)] = goods };

        return ObjectDraws.Of(map, game.Castle, null, flagGoods).Where(draw => draw.Row == 6).ToList();
    }

    [TestMethod]
    public void AGoodOnTheFirstPlaceOfAFlagIsDrawnBeforeTheFlagSixEastAndFourNorthOfIt()
    {
        // 0x44db26: the places 0 to 2 are drawn before the flag; place 0 at (+6, -4) of the flag's draw, with the sprite
        // 320 plus the good (0x44de9a). The flag's draw is at 416,60 (the test above).
        var draws = FlagDrawsWith(7, 0, 0, 0, 0, 0, 0, 0);

        var log = new ObjectDraw(6, ObjectSprites.GoodEntry(7), 422, 56);
        var flag = draws.FindIndex(draw => draw.Entry == ObjectSprites.FlagEntry);
        Assert.AreEqual(327, ObjectSprites.GoodEntry(7));
        Assert.AreEqual(log, draws[flag - 1]);
    }

    [TestMethod]
    public void AGoodOnTheFourthPlaceOfAFlagIsDrawnAfterTheFlagTenEastAndTwoSouthOfIt()
    {
        var draws = FlagDrawsWith(0, 0, 0, 7, 0, 0, 0, 0);

        var flag = draws.FindIndex(draw => draw.Entry == ObjectSprites.FlagEntry);
        Assert.AreEqual(new ObjectDraw(6, 327, 426, 62), draws[flag + 1]);
    }

    [TestMethod]
    public void AnEmptyPlaceDrawsNothingAndTheGoodsBitsAboveTheFifthAreDropped()
    {
        var empty = FlagDrawsWith(0, 0, 0, 0, 0, 0, 0, 0);
        var masked = FlagDrawsWith(0x27, 0, 0, 0, 0, 0, 0, 0);

        Assert.AreEqual(1, empty.Count(draw => draw.Entry == ObjectSprites.FlagEntry));
        Assert.DoesNotContain(draw => draw.Entry == ObjectSprites.GoodEntry(7), empty);
        Assert.AreEqual(empty.Count + 1, masked.Count);
        Assert.Contains(new ObjectDraw(6, 327, 422, 56), masked);
    }

    [TestMethod]
    public void AResourceIsNeverDrawn()
    {
        var map = Generated();

        Assert.IsNotEmpty(map.Deposits);
        Assert.HasCount(map.Objects.Count, ObjectDraws.Of(map));
    }

    [TestMethod]
    public void EveryWaterTileDrawsItsWaterFrame16PixelsWestOfItsNodeWithNoHeight()
    {
        var map = Generated();
        var width = map.Size.Width;
        var tiles = Enumerable.Range(0, map.Count).Where(tile => map.At(tile).WaterTriangle).ToList();

        var draws = ObjectDraws.WaterOf(map, 40);

        Assert.IsNotEmpty(tiles);
        CollectionAssert.AreEqual(
            tiles
                .Select(tile => new ObjectDraw(
                    tile / width,
                    ObjectSprites.WaterEntry(tile, 40),
                    Camera.NodePosition(tile % width, tile / width).X - 16,
                    Camera.NodePosition(tile % width, tile / width).Y
                ))
                .ToList(),
            draws
        );
    }
}
