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

    static (Map Map, Game Game) CastleWorld()
    {
        // Emulator: site:1:1234,5678,9ABC lists 15,5 as a castle site of height 15; its flag stands at 16,6 on height 15.
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var game = GameFactory.Create(TestProgram.Made(), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();

        return (map, game);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryGeneratedObjectIsATermInRowMajorOrderNamingItsObjectAndStep()
    {
        var map = Generated();

        var draws = ObjectDraws.Of(map, counter: 40);

        Assert.IsNotEmpty(map.Objects);
        CollectionAssert.AreEqual(
            map.Objects.Select(o => (o.Y, o.Object, (ushort)40)).ToList(),
            draws.Cast<MapObjectDraw>().Select(d => (d.Row, d.Object, d.Step)).ToList()
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void AnObjectIsDrawnAtItsNodeLoweredByItsHeight()
    {
        var draws = ObjectDraws.Of(Generated());

        // Seed 1234567812345678, size 1. Object (3,0) code 13 on height 5: x = 3 * 32, y = 0 - 5 * 4.
        CollectionAssert.Contains(
            draws,
            new MapObjectDraw
            {
                Row = 0,
                X = 96,
                Y = -20,
                Object = new MapObject { Code = 13 },
                Step = 0,
            }
        );
        // Object (9,0) code 82 on height 29: x = 9 * 32, y = 0 - 29 * 4.
        CollectionAssert.Contains(
            draws,
            new MapObjectDraw
            {
                Row = 0,
                X = 288,
                Y = -116,
                Object = new MapObject { Code = 82 },
                Step = 0,
            }
        );
    }

    [TestMethod]
    public void TheCastleIsDrawnAtItsNodeInTheOrderOfItsRowAndLoweredByItsHeight()
    {
        var (map, game) = CastleWorld();

        var draws = ObjectDraws.Of(map, game.CastleStock.Castle);

        // The castle's node 15,5 is levelled to 15: x = 15 * 32 - 5 * 16, y = 5 * 20 - 15 * 4. inferred: the draw at the
        // node is that of the objects, inferred from the picture; no emulator or debugger run of the draw confirms it
        // for the castle.
        var castle = draws.OfType<BuildingDraw>().Single();
        Assert.AreEqual(
            new BuildingDraw
            {
                Row = 5,
                X = 400,
                Y = 40,
                Type = BuildingType.Castle,
            },
            castle
        );
        CollectionAssert.AreEqual(
            draws.Select(draw => draw.Row).Order().ToList(),
            draws.Select(draw => draw.Row).ToList()
        );
    }

    [TestMethod]
    public void AHutIsDrawnLikeTheCastleAtItsNodeAsAWoodcuttersBuilding()
    {
        // The hut at 16,2 stands tied to the flag at 17,3 (the road test world of HutTests); its node's height is read from
        // the map: x = 16 * 32 - 2 * 16, y = 2 * 20 - height * 4.
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var game = GameFactory.Create(new RuleTables(classes, TestProgram.Made().StartStock), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 17,
                Y = 3,
            }
        );
        game.Tick();
        Huts.PlaceAndTick(game, 16, 2);
        var height = map.At(16, 2).Height;

        var draws = ObjectDraws.Of(map, game.CastleStock.Castle, game.Buildings);

        Assert.AreEqual(
            new BuildingDraw
            {
                Row = 2,
                X = 480,
                Y = 40 - height * 4,
                Type = BuildingType.Woodcutter,
            },
            draws.OfType<BuildingDraw>().Single(draw => draw.Type == BuildingType.Woodcutter)
        );
        CollectionAssert.AreEqual(
            draws.Select(draw => draw.Row).Order().ToList(),
            draws.Select(draw => draw.Row).ToList()
        );
    }

    [TestMethod]
    public void ABuildingSiteIsDrawnAtItsNodeWithItsProgressAndAFinishedBuildingWithNone()
    {
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var sizes = Enumerable.Repeat((byte)1, RuleTables.BuildingTypeCount).ToArray();
        sizes[BuildingType.Sawmill.Code] = 2;
        var game = GameFactory.Create(
            new RuleTables(classes, TestProgram.Made().StartStock, buildingSizes: sizes),
            map
        );
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 17,
                Y = 3,
            }
        );
        game.Tick();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = 16,
                Y = 2,
                Building = BuildingType.Stonecutter,
            }
        );
        game.Tick();
        Assert.IsTrue(game.Buildings[1].Constructing, "the command placed a site");

        var draws = ObjectDraws.Of(map, game.CastleStock.Castle, game.Buildings).OfType<BuildingDraw>().ToList();

        var height = map.At(16, 2).Height;
        CollectionAssert.Contains(
            draws,
            new BuildingDraw
            {
                Row = 2,
                X = 480,
                Y = 40 - height * 4,
                Type = BuildingType.Stonecutter,
                SiteProgress = 1,
            }
        );
        Assert.IsTrue(
            draws.Where(draw => draw.Type == BuildingType.Castle).All(draw => draw.SiteProgress is null),
            "the castle is no site"
        );
    }

    [TestMethod]
    public void AFinishedBuildingOfEachTypeIsDrawnWithItsType()
    {
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var game = GameFactory.Create(new RuleTables(classes, TestProgram.Made().StartStock), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();
        Assert.IsTrue(game.PlaceFinishedBuilding(BuildingType.Forester, 16, 2));

        var draws = ObjectDraws.Of(map, game.CastleStock.Castle, game.Buildings).OfType<BuildingDraw>().ToList();

        Assert.IsTrue(draws.Any(draw => draw.Type == BuildingType.Forester && draw.SiteProgress is null));
    }

    [TestMethod]
    public void AFlagIsDrawnLikeAnObjectOfTheFlagKindAtItsAnimationStep()
    {
        var (map, game) = CastleWorld();

        var draws = ObjectDraws.Of(map, game.CastleStock.Castle, counter: 8);

        CollectionAssert.Contains(
            draws,
            new MapObjectDraw
            {
                Row = 6,
                X = 416,
                Y = 60,
                Object = MapObject.Flag,
                Step = 8,
            }
        );
    }

    [TestMethod]
    public void AFlagIsDrawnOnTheNodeWhereItsRoadsMeet()
    {
        // In the original's picture the roads meet at the foot of a flag's pole. Roads and the pick use the node's
        // point, Camera.NodePosition lifted by the node's height, so the flag's draw position is that point.
        var (map, game) = CastleWorld();
        var (x, y) = Camera.NodePosition(16, 6);
        var lift = map.At(16, 6).Height * TerrainTriangles.HeightStep;

        var flag = ObjectDraws
            .Of(map, game.CastleStock.Castle)
            .OfType<MapObjectDraw>()
            .Single(draw => draw.Object == MapObject.Flag);

        Assert.AreEqual((x, y - lift), (flag.X, flag.Y));
    }

    // **Inferred:** the offsets of a good come from the decompile of 0x44db26 and 0x44de9a; no emulator or debugger run
    // of the draw confirms them (docs/rules/woodcutting.md, Drawing). The castle's flag stands at 16,6 on height 15 and
    // is drawn at 416,60.
    static List<ObjectTerm> FlagDrawsWith(params int[] goods)
    {
        var (map, game) = CastleWorld();
        var flagGoods = new Dictionary<(int, int), IReadOnlyList<Good>>
        {
            [(16, 6)] = [.. goods.Select(good => new Good { Code = good })],
        };

        return ObjectDraws.Of(map, game.CastleStock.Castle, null, flagGoods).Where(draw => draw.Row == 6).ToList();
    }

    [TestMethod]
    public void TheGoodsOfAFlagFollowTheOriginalsOrderPlacesZeroToTwoBeforeTheFlagAndTheRestAfterIt()
    {
        var draws = FlagDrawsWith(1, 2, 3, 4, 5, 6, 7, 8);

        var flag = draws.FindIndex(draw => draw is MapObjectDraw { Object.Code: 1 });
        // The places 0 to 2 are drawn before the flag at (+6, -4), (+10, -2), (-4, -4) of the flag's draw position at
        // 416,60; the places 3 to 7 after it at (+10, +2), (-8, -2), (+6, +4), (-8, +2), (-4, +4).
        CollectionAssert.AreEqual(
            new ObjectTerm[]
            {
                new GoodAtFlag
                {
                    Row = 6,
                    X = 422,
                    Y = 56,
                    Good = new Good { Code = 1 },
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 426,
                    Y = 58,
                    Good = new Good { Code = 2 },
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 412,
                    Y = 56,
                    Good = new Good { Code = 3 },
                },
                new MapObjectDraw
                {
                    Row = 6,
                    X = 416,
                    Y = 60,
                    Object = MapObject.Flag,
                    Step = 0,
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 426,
                    Y = 62,
                    Good = new Good { Code = 4 },
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 408,
                    Y = 58,
                    Good = new Good { Code = 5 },
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 422,
                    Y = 64,
                    Good = new Good { Code = 6 },
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 408,
                    Y = 62,
                    Good = new Good { Code = 7 },
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 412,
                    Y = 64,
                    Good = new Good { Code = 8 },
                },
            },
            draws.Skip(flag - 3).Take(9).ToList()
        );
    }

    [TestMethod]
    public void TheCastleAndAHutAreDrawnAfterTheLastTermOfTheirRowAndBeforeTheFirstOfTheNextRow()
    {
        // The castle at 15,5 with its flag at 16,6 (goods on it), and the hut at 16,2 tied to the flag at 17,3.
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var game = GameFactory.Create(new RuleTables(classes, TestProgram.Made().StartStock), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 17,
                Y = 3,
            }
        );
        game.Tick();
        Huts.PlaceAndTick(game, 16, 2);
        var flagGoods = new Dictionary<(int, int), IReadOnlyList<Good>> { [(16, 6)] = [new Good { Code = 1 }] };

        var draws = ObjectDraws.Of(map, game.CastleStock.Castle, game.Buildings, flagGoods);

        // Map of the seed 1234,5678,9ABC: the objects of rows 2 to 6 in row-major order, the
        // hut (code 2 at 16,2) and the castle (code 4 at 15,5) among them. Each building follows the last object of its
        // row, where the next row's first object follows; the flag's good (place 0) precedes the flag.
        CollectionAssert.AreEqual(
            new ObjectTerm[]
            {
                new MapObjectDraw
                {
                    Row = 2,
                    X = 480,
                    Y = 8,
                    Object = new MapObject { Code = 2 },
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 2,
                    X = 576,
                    Y = 40,
                    Object = new MapObject { Code = 31 },
                    Step = 0,
                },
                new BuildingDraw
                {
                    Row = 2,
                    X = 480,
                    Y = 8,
                    Type = BuildingType.Woodcutter,
                },
                new MapObjectDraw
                {
                    Row = 3,
                    X = 496,
                    Y = 40,
                    Object = MapObject.Flag,
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 3,
                    X = 656,
                    Y = 60,
                    Object = new MapObject { Code = 89 },
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 3,
                    X = 720,
                    Y = 60,
                    Object = new MapObject { Code = 88 },
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 4,
                    X = 416,
                    Y = 20,
                    Object = new MapObject { Code = 16 },
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 4,
                    X = 448,
                    Y = 36,
                    Object = new MapObject { Code = 9 },
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 5,
                    X = 400,
                    Y = 40,
                    Object = new MapObject { Code = 4 },
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 5,
                    X = 752,
                    Y = 100,
                    Object = new MapObject { Code = 89 },
                    Step = 0,
                },
                new BuildingDraw
                {
                    Row = 5,
                    X = 400,
                    Y = 40,
                    Type = BuildingType.Castle,
                },
                new MapObjectDraw
                {
                    Row = 6,
                    X = 224,
                    Y = 60,
                    Object = new MapObject { Code = 9 },
                    Step = 0,
                },
                new MapObjectDraw
                {
                    Row = 6,
                    X = 256,
                    Y = 60,
                    Object = new MapObject { Code = 8 },
                    Step = 0,
                },
                new GoodAtFlag
                {
                    Row = 6,
                    X = 422,
                    Y = 56,
                    Good = new Good { Code = 1 },
                },
                new MapObjectDraw
                {
                    Row = 6,
                    X = 416,
                    Y = 60,
                    Object = MapObject.Flag,
                    Step = 0,
                },
            },
            draws.Where(draw => draw.Row is >= 2 and <= 6).ToList()
        );
    }

    [TestMethod]
    public void AnEmptyPlaceDrawsNoTermAndAGoodIsNamedByItsGoodWhateverItsBits()
    {
        var empty = FlagDrawsWith(0, 0, 0, 0, 0, 0, 0, 0);
        var one = FlagDrawsWith(0x27, 0, 0, 0, 0, 0, 0, 0);

        Assert.IsEmpty(empty.OfType<GoodAtFlag>());
        Assert.AreEqual(empty.Count + 1, one.Count);
        Assert.Contains(
            new GoodAtFlag
            {
                Row = 6,
                X = 422,
                Y = 56,
                Good = new Good { Code = 0x27 },
            },
            one
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void AResourceIsNeverDrawn()
    {
        var map = Generated();

        Assert.IsNotEmpty(map.Deposits);
        Assert.HasCount(map.Objects.Count, ObjectDraws.Of(map));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFlagGeometryIsTheBoxOfTheHistoryEditionsFlagAtRest()
    {
        var image = HistoryEditionSource
            .Load(TestProgram.OpenInstall())
            .Draw(
                new MapObjectDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Object = MapObject.Flag,
                    Step = 0,
                }
            )!
            .Image!;

        Assert.AreEqual(
            (
                ObjectDraws.FlagGeometry.Width,
                ObjectDraws.FlagGeometry.Height,
                ObjectDraws.FlagGeometry.OffsetX,
                ObjectDraws.FlagGeometry.OffsetY
            ),
            (image.Width, image.Height, image.OffsetX, image.OffsetY)
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryWaterTileIsARippleSixteenPixelsWestOfItsNodeWithNoHeight()
    {
        var map = Generated();
        var width = map.Size.Width;
        var tiles = Enumerable.Range(0, map.Count).Where(tile => map.At(tile).WaterTriangle).ToList();

        var draws = ObjectDraws.WaterOf(map, 40);

        Assert.IsNotEmpty(tiles);
        CollectionAssert.AreEqual(
            tiles
                .Select(tile => new WaterRipple
                {
                    Row = tile / width,
                    X = Camera.NodePosition(tile % width, tile / width).X - 16,
                    Y = Camera.NodePosition(tile % width, tile / width).Y,
                    Tile = tile,
                    Step = 40,
                })
                .ToList(),
            draws
        );
    }
}
