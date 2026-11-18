using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class HistoryEditionSourceTests
{
    const string Archive = "loca/SPAD.PA";

    // A mask of width x height whose rows each cover all but the first pixel.
    static byte[] Mask(int width, int height, short offsetX, short offsetY)
    {
        var bytes = new List<byte> { 0, 0 };
        bytes.AddRange(BitConverter.GetBytes((ushort)width));
        bytes.AddRange(BitConverter.GetBytes((ushort)height));
        bytes.AddRange(BitConverter.GetBytes(offsetX));
        bytes.AddRange(BitConverter.GetBytes(offsetY));
        for (var row = 0; row < height; row++)
        {
            bytes.AddRange([1, (byte)(width - 1), 0, 0]);
        }

        return [.. bytes];
    }

    // A 32 x 20 texture whose row y holds the palette index first + 4 y.
    static byte[] Texture(byte first)
    {
        var bytes = new List<byte> { 0, 0 };
        bytes.AddRange(BitConverter.GetBytes((ushort)32));
        bytes.AddRange(BitConverter.GetBytes((ushort)20));
        bytes.AddRange(new byte[4]);
        for (var y = 0; y < 20; y++)
        {
            bytes.AddRange(new byte[32]);
            for (var x = 0; x < 32; x++)
            {
                bytes[^(x + 1)] = (byte)(first + 4 * y);
            }
        }

        return [.. bytes];
    }

    // Palette index i holds the red component i (the palette keeps its upper six bits, so every index here is a
    // multiple of 4). A terrain mask n is 16 wide, 25 high for entry 100 and 9 otherwise, offset 3 across and n - 100
    // down; a terrain texture n starts at index 4 (n - 260). A road mask n is 16 x 5, offset 1 across and n - 230
    // down; a road texture n starts at index 100 + 4 (n - 300).
    internal static Dictionary<int, byte[]> Payloads()
    {
        var palette = new byte[768];
        for (var i = 0; i < 256; i++)
        {
            palette[3 * i] = (byte)i;
        }

        var payloads = new Dictionary<int, byte[]> { [3] = palette };
        for (var n = 60; n <= 221; n++)
        {
            payloads[n] = Mask(16, n == 100 ? 25 : 9, 3, (short)(n - 100));
        }

        for (var n = 260; n <= 292; n++)
        {
            payloads[n] = Texture((byte)((n - 260) * 4));
        }

        for (var n = 230; n <= 256; n++)
        {
            payloads[n] = Mask(16, 5, 1, (short)(n - 230));
        }

        for (var n = 300; n <= 309; n++)
        {
            payloads[n] = Texture((byte)(100 + (n - 300) * 4));
        }

        return payloads;
    }

    static HistoryEditionSource Open(FakeInstall fake)
    {
        Assert.IsTrue(Install.TryOpen(fake.Root, out var install, out _));

        return HistoryEditionSource.Load(install);
    }

    static FakeInstall Fake(Dictionary<int, byte[]>? payloads = null) =>
        new FakeInstall().Put(Archive, ArchiveBytes.Build(311, payloads ?? Payloads()));

    static TerrainTriangle Flat(Terrain terrain, bool upper = true) =>
        new()
        {
            Column = 0,
            Row = 0,
            Upper = upper,
            Terrain = terrain,
            Apex = 5,
            Left = 5,
            Right = 5,
            X = 100,
            Y = 50,
        };

    static byte[] Pixel(ObjectImage image, int x, int y) => image.Rgba[((y * image.Width + x) * 4)..][..4];

    [TestMethod]
    public void AFlatUpperTriangleOfGrassIsDrawnWithMask100AndTexture264()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(Flat(new Terrain { Code = 5 }))!;

        Assert.AreEqual(16, image.Width);
        Assert.AreEqual(25, image.Height);
        Assert.AreEqual(3 - 16, image.OffsetX);
        Assert.AreEqual(0, image.OffsetY);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, Pixel(image, 0, 0));
        CollectionAssert.AreEqual(new byte[] { 4 * 4, 0, 0, 255 }, Pixel(image, 1, 0));
    }

    [TestMethod]
    public void AFlatLowerTriangleOfGrassIsDrawnWithMask181()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(Flat(new Terrain { Code = 5 }, upper: false))!;

        Assert.AreEqual(181 - 100, image.OffsetY);
    }

    [TestMethod]
    public void TheTextureRepeatsDownEveryTwentyRowsFromTheTopOfTheMask()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(Flat(new Terrain { Code = 5 }))!;

        CollectionAssert.AreEqual(new byte[] { 4 * 4 + 4, 0, 0, 255 }, Pixel(image, 1, 21));
    }

    [TestMethod]
    public void WaterIsDrawnWithTheLastTexture()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(Flat(new Terrain { Code = 0 }))!;

        CollectionAssert.AreEqual(new byte[] { 32 * 4, 0, 0, 255 }, Pixel(image, 1, 0));
    }

    [TestMethod]
    public void AHeightStepBeyondFourDrawsNothing()
    {
        using var fake = Fake();
        var source = Open(fake);

        Assert.IsNull(
            source.Draw(
                new TerrainTriangle
                {
                    Column = 0,
                    Row = 0,
                    Upper = true,
                    Terrain = new Terrain { Code = 5 },
                    Apex = 5,
                    Left = 0,
                    Right = 5,
                    X = 100,
                    Y = 50,
                }
            )
        );
        Assert.IsNull(
            source.Draw(
                new TerrainTriangle
                {
                    Column = 0,
                    Row = 0,
                    Upper = false,
                    Terrain = new Terrain { Code = 5 },
                    Apex = 5,
                    Left = 5,
                    Right = 0,
                    X = 100,
                    Y = 50,
                }
            )
        );
    }

    [TestMethod]
    public void AHeightIsItsFiveLowBits()
    {
        using var fake = Fake();
        var source = Open(fake);

        var low = source.Draw(
            new TerrainTriangle
            {
                Column = 0,
                Row = 0,
                Upper = true,
                Terrain = new Terrain { Code = 5 },
                Apex = 1,
                Left = 1,
                Right = 1,
                X = 100,
                Y = 50,
            }
        );
        var high = source.Draw(
            new TerrainTriangle
            {
                Column = 0,
                Row = 0,
                Upper = true,
                Terrain = new Terrain { Code = 5 },
                Apex = 33,
                Left = 1,
                Right = 1,
                X = 100,
                Y = 50,
            }
        );

        Assert.AreSame(low, high);
    }

    [TestMethod]
    public void TermsThatPickTheSameMaskAndTextureShareOneImage()
    {
        using var fake = Fake();
        var source = Open(fake);

        var a = source.Draw(Flat(new Terrain { Code = 5 }));
        var b = source.Draw(Flat(new Terrain { Code = 5 }) with { X = 300, Y = 7 });
        var other = source.Draw(Flat(new Terrain { Code = 12 }));

        Assert.AreSame(a, b);
        Assert.AreNotSame(a, other);
    }

    [TestMethod]
    public void ALevelEastStripOfPlainLandIsDrawnWithMask234AndTexture301()
    {
        using var fake = Fake();

        var image = Open(fake)
            .Draw(
                new RoadStrip
                {
                    X = 100,
                    Y = 200,
                    Direction = RoadDirection.East,
                    Difference = 0,
                    Slope = 0,
                    Ground = new Terrain { Code = 5 },
                }
            )!;

        Assert.AreEqual(234 - 230, image.OffsetY);
        Assert.AreEqual(1, image.OffsetX);
        CollectionAssert.AreEqual(new byte[] { 100 + 4, 0, 0, 255 }, Pixel(image, 1, 0));
    }

    [TestMethod]
    public void AStripBeyondAHeightDifferenceOfFourDrawsNothing()
    {
        using var fake = Fake();

        Assert.IsNull(
            Open(fake)
                .Draw(
                    new RoadStrip
                    {
                        X = 100,
                        Y = 200,
                        Direction = RoadDirection.South,
                        Difference = 5,
                        Slope = 0,
                        Ground = new Terrain { Code = 5 },
                    }
                )
        );
        Assert.IsNull(
            Open(fake)
                .Draw(
                    new RoadStrip
                    {
                        X = 100,
                        Y = 200,
                        Direction = RoadDirection.South,
                        Difference = -5,
                        Slope = 0,
                        Ground = new Terrain { Code = 5 },
                    }
                )
        );
    }

    [TestMethod]
    public void TheMaskOfAStripFollowsItsDirectionAndDifference()
    {
        using var fake = Fake();
        var source = Open(fake);

        Assert.AreEqual(
            243 - 230 + 2,
            source
                .Draw(
                    new RoadStrip
                    {
                        X = 0,
                        Y = 0,
                        Direction = RoadDirection.SouthEast,
                        Difference = 2,
                        Slope = 0,
                        Ground = new Terrain { Code = 5 },
                    }
                )!
                .OffsetY
        );
        Assert.AreEqual(
            252 - 230 - 3, // the south masks start at 248 for -4
            source
                .Draw(
                    new RoadStrip
                    {
                        X = 0,
                        Y = 0,
                        Direction = RoadDirection.South,
                        Difference = -3,
                        Slope = 0,
                        Ground = new Terrain { Code = 5 },
                    }
                )!
                .OffsetY
        );
    }

    [TestMethod]
    public void TheTextureOfAStripFollowsSlopeWaterMountainAndSnow()
    {
        using var fake = Fake();
        var source = Open(fake);
        byte Red(int slope, int ground) =>
            Pixel(
                source.Draw(
                    new RoadStrip
                    {
                        X = 0,
                        Y = 0,
                        Direction = RoadDirection.East,
                        Difference = 0,
                        Slope = slope,
                        Ground = new Terrain { Code = ground },
                    }
                )!,
                1,
                0
            )[0];

        Assert.AreEqual(100, Red(5, 5));
        Assert.AreEqual(100 + 4, Red(0, 5));
        Assert.AreEqual(100 + 8, Red(-6, 5));
        Assert.AreEqual(100 + 9 * 4, Red(0, 0));
        Assert.AreEqual(100 + 4 * 4, Red(0, 9));
        Assert.AreEqual(100 + 7 * 4, Red(0, 15));
    }

    [TestMethod]
    public void AnUnreadableRoadMaskLeavesTheRoadsUndrawnAndTheTerrainDrawn()
    {
        var payloads = Payloads();
        payloads.Remove(240);
        using var fake = Fake(payloads);

        var source = Open(fake);

        StringAssert.Contains(source.RoadsProblem!, "entry 240");
        Assert.IsNull(
            source.Draw(
                new RoadStrip
                {
                    X = 0,
                    Y = 0,
                    Direction = RoadDirection.East,
                    Difference = 0,
                    Slope = 0,
                    Ground = new Terrain { Code = 5 },
                }
            )
        );
        Assert.IsNotNull(source.Draw(Flat(new Terrain { Code = 5 })));
    }

    [TestMethod]
    public void AMissingArchiveNamesItself()
    {
        using var fake = new FakeInstall();

        var problem = Assert.ThrowsExactly<FileNotFoundException>(() => Open(fake));

        StringAssert.Contains(problem.Message, Archive);
    }

    // The terrain and road entries and an object catalog: the sprites of object entries 1250 (3 wide), 1251 (2 wide),
    // the castle, the flag and its frame 1, a log, and the water entry 635, with the shadow mask of 1250 (entry 1500).
    // Palette index 8 holds the color 40, 12, 0.
    static FakeInstall ObjectFake(Action<Dictionary<int, byte[]>>? change = null)
    {
        var payloads = Payloads();
        foreach (var (entry, bytes) in ObjectPayloads.Payloads(flag: true))
        {
            payloads[entry] = bytes;
        }

        payloads[3][3 * 8] = 40;
        payloads[3][3 * 8 + 1] = 12;
        payloads[ObjectSprites.FlagEntry + 1] = GraphicEntries.Square(10, 5);
        payloads[ObjectSprites.GoodEntry(Good.Log)!.Value] = GraphicEntries.Square(3, 1);
        payloads[ObjectSprites.WaterEntry(0, 0)] = GraphicEntries.Square(4, 4);
        payloads[1250 + ObjectGraphics.ShadowOffset] = GraphicEntries.ShadowMask();
        change?.Invoke(payloads);

        return new FakeInstall().Put(Archive, ArchiveBytes.Build(1700, payloads));
    }

    static MapObjectDraw Object(int code, ushort step = 0) =>
        new()
        {
            Row = 0,
            X = 10,
            Y = 20,
            Object = new MapObject { Code = (byte)code },
            Step = step,
        };

    [TestMethod]
    public void AMapObjectDrawsTheSpriteOfItsCodeWithItsShadow()
    {
        using var fake = ObjectFake();

        var drawing = Open(fake).Draw(Object(8))!;

        Assert.AreEqual(3, drawing.Image!.Width);
        Assert.AreEqual(2, drawing.Shadow!.Width);
    }

    [TestMethod]
    public void TheAnimationStepFoldsThePhaseIntoTheEntry()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        // Code 8 is entry 1250 for 16 steps and entry 1251 at step 16 (docs/rules/map-objects.md).
        Assert.AreEqual(3, source.Draw(Object(8, 15))!.Image!.Width);
        Assert.AreEqual(2, source.Draw(Object(8, 16))!.Image!.Width);
        Assert.AreEqual(
            10,
            source
                .Draw(
                    new MapObjectDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Object = MapObject.Flag,
                        Step = 8,
                    }
                )!
                .Image!.OffsetX
        );
        Assert.AreEqual(
            9,
            source
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
                .Image!.OffsetX
        );
    }

    [TestMethod]
    public void TermsThatDrawTheSameImageGetTheSameDrawing()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        Assert.AreSame(source.Draw(Object(8)), source.Draw(Object(15)));
    }

    [TestMethod]
    public void AnObjectCodeWithNoImageDrawsNothingWithoutError()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        Assert.IsNull(source.Draw(Object(40)));
        Assert.IsNull(source.Draw(Object(5)));
        Assert.IsNull(source.Draw(Object(0)));
    }

    [TestMethod]
    public void TheCastleAndTheHutDrawTheirOwnSpritesAndAnotherBuildingDrawsNothing()
    {
        using var fake = ObjectFake(payloads => payloads[ObjectSprites.WoodcutterEntry] = GraphicEntries.Square(11, 3));
        var source = Open(fake);

        Assert.AreEqual(
            7,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Castle,
                    }
                )!
                .Image!.OffsetX
        );
        Assert.AreEqual(
            11,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Woodcutter,
                    }
                )!
                .Image!.OffsetX
        );
        Assert.IsNull(
            source.Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = BuildingType.Storehouse,
                }
            )
        );
    }

    // The sprites of the finished buildings of M4 and of the sites that have not started (docs/rules/build-sites.md): each entry
    // holds a sprite of its own offset, so the draw names the entry that the routine at 0x44e329 stores for the building.
    [TestMethod]
    public void EachFinishedBuildingOfM4AndEachSiteDrawsTheSpriteOfItsOwnEntry()
    {
        using var fake = ObjectFake(payloads =>
        {
            payloads[1250 + 169] = GraphicEntries.Square(21, 3);
            payloads[1250 + 170] = GraphicEntries.Square(22, 3);
            payloads[1250 + 160] = GraphicEntries.Square(23, 3);
            payloads[1250 + 144] = GraphicEntries.Square(24, 3);
            payloads[1250 + 145] = GraphicEntries.Square(25, 3);
        });
        var source = Open(fake);

        Assert.AreEqual(
            21,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Stonecutter,
                    }
                )!
                .Image!.OffsetX
        );
        Assert.AreEqual(
            22,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Forester,
                    }
                )!
                .Image!.OffsetX
        );
        Assert.AreEqual(
            23,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Sawmill,
                    }
                )!
                .Image!.OffsetX
        );
        Assert.AreEqual(
            24,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Sawmill,
                        SiteProgress = 0,
                    }
                )!
                .Image!.OffsetX
        );
        Assert.AreEqual(
            25,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Woodcutter,
                        SiteProgress = 1,
                    }
                )!
                .Image!.OffsetX
        );
    }

    // A site's stages (docs/rules/construction.md): the base (object 145) with the scaffold of the type (object 186 for the small types)
    // grown over it below the progress word 0x8000, then the whole scaffold with the finished building (the forester's object 170) grown.
    [TestMethod]
    public void ASiteDrawsItsScaffoldGrownOverItsBaseAndThenTheFinishedBuildingGrownOverTheScaffold()
    {
        using var fake = ObjectFake(payloads =>
        {
            payloads[1250 + 145] = GraphicEntries.Square(25, 3);
            payloads[1250 + 186] = GraphicEntries.Square(26, 3);
            payloads[1250 + 170] = GraphicEntries.Square(22, 3);
        });
        var source = Open(fake);

        var first = source
            .Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = BuildingType.Forester,
                    SiteProgress = 0x4000,
                }
            )!
            .Image!;
        var second = source
            .Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = BuildingType.Forester,
                    SiteProgress = 0x8001,
                }
            )!
            .Image!;

        // The union of the images that draw starts at the left edge of the leftmost: 25 where the base and the scaffold draw, 22 where
        // the finished building grows over the scaffold.
        Assert.AreEqual(25, first.OffsetX);
        Assert.AreEqual(22, second.OffsetX);
        Assert.AreSame(
            second,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 5,
                        Y = 5,
                        Type = BuildingType.Forester,
                        SiteProgress = 0x8001,
                    }
                )!
                .Image
        );
    }

    // The install's program holds a flame list for every type the game can take down and for an early site, and its archive the
    // flames' sprites (docs/rules/removal.md, "The burning draw").
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheInstallGivesEveryBuildingTheGameCanTakeDownItsFlamesAndTheirSprites()
    {
        var source = HistoryEditionSource.Load(TestProgram.OpenInstall(), TestProgram.Tables());

        Assert.IsNull(source.ObjectsProblem);
        // The types a placement builds: the construction milestone's and the six food buildings.
        BuildingType[] placed =
        [
            BuildingType.Woodcutter,
            BuildingType.Stonecutter,
            BuildingType.Forester,
            BuildingType.Sawmill,
            BuildingType.Fisher,
            BuildingType.Farm,
            BuildingType.Butcher,
            BuildingType.PigFarm,
            BuildingType.Mill,
            BuildingType.Baker,
        ];
        foreach (var (type, earlySite) in placed.Select(type => (type, false)).Append((BuildingType.Woodcutter, true)))
        {
            var flames = source.Flames(type, earlySite);
            Assert.IsNotEmpty(flames, $"{type} early site {earlySite}");
            foreach (var flame in flames)
            {
                Assert.IsNotNull(
                    source.Draw(
                        new FlameDraw
                        {
                            Row = 0,
                            X = 0,
                            Y = 0,
                            Size = flame.Size,
                            Phase = 7,
                        }
                    )
                );
            }
        }
    }

    // The castle under construction (0x44fd2f, 0x44fee2): the finished castle's sprite grown by the progress word itself over its whole shadow;
    // once it stands, the castle whole.
    [TestMethod]
    public void TheCastleUnderConstructionGrowsTheFinishedCastleOverItsWholeShadow()
    {
        using var fake = ObjectFake(payloads =>
        {
            payloads[ObjectSprites.CastleEntry] = GraphicEntries.Square(30, 3);
            payloads[ObjectSprites.CastleEntry + 250] = GraphicEntries.ShadowMask();
        });
        var source = Open(fake);
        BuildingDraw Castle(ushort? progress) =>
            new()
            {
                Row = 0,
                X = 0,
                Y = 0,
                Type = BuildingType.Castle,
                SiteProgress = progress,
            };

        var placed = source.Draw(Castle(0))!;
        var whole = source.Draw(Castle(null))!;

        // (0 * 2 >> 16) + 1 = 1 row of the castle's 2 at the placement (0x406350).
        Assert.AreEqual((1, 2), (placed.Image!.Height, whole.Image!.Height));
        Assert.AreEqual(30, placed.Image.OffsetX, "the castle's own sprite, with no site layer under it");
        Assert.AreSame(whole.Shadow, placed.Shadow, "the castle's whole shadow");
        Assert.IsNotNull(placed.Shadow);
    }

    // The install's own archive holds the scaffold and the finished building of each type, so a site draws its stages as it grows: the
    // drawing of a larger progress word covers at least the rows of a smaller one's, and the last one covers the finished building.
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheInstallsArchiveGivesEachTypeItsGrowingSite()
    {
        var source = HistoryEditionSource.Load(TestProgram.OpenInstall(), TestProgram.Tables());

        foreach (
            var type in new[]
            {
                BuildingType.Woodcutter,
                BuildingType.Stonecutter,
                BuildingType.Forester,
                BuildingType.Sawmill,
            }
        )
        {
            var heights = new[] { 0x0400, 0x2000, 0x7fff, 0x8100, 0xc000, 0xff00 }
                .Select(progress =>
                    source
                        .Draw(
                            new BuildingDraw
                            {
                                Row = 0,
                                X = 0,
                                Y = 0,
                                Type = type,
                                SiteProgress = (ushort)progress,
                            }
                        )
                        ?.Image
                )
                .ToArray();

            Assert.IsTrue(heights.All(image => image is not null), $"{type} draws a stage at each progress word");
            // Within each half of the word the drawing grows; the second half draws no base, so it may start lower than the first ended.
            foreach (var i in new[] { 1, 2, 4, 5 })
            {
                Assert.IsTrue(
                    heights[i]!.Height >= heights[i - 1]!.Height,
                    $"{type}: the drawing grows as the progress rises"
                );
            }

            Assert.IsTrue(
                heights[^1]!.Height
                    >= source
                        .Draw(
                            new BuildingDraw
                            {
                                Row = 0,
                                X = 0,
                                Y = 0,
                                Type = type,
                            }
                        )!
                        .Image!.Height - 1
            );
        }
    }

    [TestMethod]
    public void AGoodDrawsItsSpriteByItsStableNameAndNoShadow()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        var log = source.Draw(AtFlag(Good.Log))!;

        Assert.AreEqual(3, log.Image!.OffsetX);
        Assert.IsNull(log.Shadow);
    }

    [TestMethod]
    public void AGoodWithNoOriginalCodeDrawsNothingAndNoMaskLetsOneDrawAnothersSprite()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        Assert.IsNull(source.Draw(AtFlag(new Good { Name = "wine" })));
        Assert.IsNull(source.Draw(AtFlag(Good.None)));
    }

    static GoodAtFlag AtFlag(Good good) =>
        new()
        {
            Row = 0,
            X = 0,
            Y = 0,
            Good = good,
        };

    [TestMethod]
    public void EachBuildMarkerDrawsItsOwnSpriteAndNoShadow()
    {
        // Entries 367 to 370 are the flag, mine, small, and large markers (docs/rules/build-sites.md, "The build-site
        // overlay"). The large marker has a shadow mask at its entry plus 250 here, which the original never draws for it;
        // the entries 617 to 619 hold border posts.
        using var fake = ObjectFake(payloads =>
        {
            for (var entry = 367; entry <= 370; entry++)
            {
                payloads[entry] = GraphicEntries.Square((short)(entry - 360), 2);
            }

            payloads[370 + 250] = GraphicEntries.ShadowMask();
        });
        var source = Open(fake);

        BuildMarker[] markers = [BuildMarker.Flag, BuildMarker.Mine, BuildMarker.Small, BuildMarker.Large];
        int[] offsets = [.. markers.Select(marker => source.Draw(marker)!.OffsetX)];

        CollectionAssert.AreEqual(new[] { 7, 8, 9, 10 }, offsets);
        Assert.AreSame(source.Draw(BuildMarker.Mine), source.Draw(BuildMarker.Mine));
    }

    // The road cursor draws its word plus 320: the end mark 352, the blocked mark 364, the way-back mark 365, and the slope
    // 359 plus the height difference, with no bound, so 5 draws the blocked image and -5 draws 354 (docs/rules/roads.md, "The
    // road markers"). Each fake sprite's horizontal offset is its entry less 340.
    [TestMethod]
    [DataRow(RoadMarkerKind.End, 0, 352)]
    [DataRow(RoadMarkerKind.Blocked, 0, 364)]
    [DataRow(RoadMarkerKind.WayBack, 0, 365)]
    [DataRow(RoadMarkerKind.Slope, -5, 354)]
    [DataRow(RoadMarkerKind.Slope, -4, 355)]
    [DataRow(RoadMarkerKind.Slope, 0, 359)]
    [DataRow(RoadMarkerKind.Slope, 4, 363)]
    [DataRow(RoadMarkerKind.Slope, 5, 364)]
    public void EachRoadMarkerDrawsTheOriginalsSpriteAndNoShadow(RoadMarkerKind kind, int difference, int entry)
    {
        using var fake = ObjectFake(payloads =>
        {
            for (var each = 352; each <= 365; each++)
            {
                payloads[each] = GraphicEntries.Square((short)(each - 340), 2);
            }
        });
        var source = Open(fake);
        var marker = new RoadMarker { Kind = kind, Difference = difference };

        Assert.AreEqual(entry - 340, source.Draw(marker)!.OffsetX);
        Assert.AreSame(source.Draw(marker), source.Draw(marker));
    }

    // A difference whose entry lies outside the archive's sprites (321 to 372) draws nothing, even where the archive holds an
    // entry there, as does an entry the archive leaves out (366).
    [TestMethod]
    [DataRow(7)]
    [DataRow(14)]
    [DataRow(-31)]
    public void ARoadMarkerOutsideThePresentSpritesDrawsNothing(int difference)
    {
        using var fake = ObjectFake(payloads => payloads[373] = GraphicEntries.Square(1, 1));

        Assert.IsNull(Open(fake).Draw(new RoadMarker { Kind = RoadMarkerKind.Slope, Difference = difference }));
    }

    static BorderPost Post(int slope, int ground) =>
        new()
        {
            Column = 0,
            Row = 0,
            X = 0,
            Y = 0,
            Direction = RoadDirection.East,
            Slope = slope,
            Ground = new Terrain { Code = ground },
        };

    [TestMethod]
    [DataRow(2, 5, 610)]
    [DataRow(1, 5, 611)]
    [DataRow(-8, 5, 611)]
    [DataRow(-9, 5, 612)]
    [DataRow(2, 4, 610)]
    [DataRow(2, 10, 610)]
    [DataRow(2, 11, 613)]
    [DataRow(-8, 14, 614)]
    [DataRow(-9, 15, 618)]
    [DataRow(30, 3, 619)]
    [DataRow(-30, 0, 619)]
    public void ABorderPostTakesTheImageOfItsSlopeBandRaisedByItsGroundOrTheWaterPost(int slope, int ground, int entry)
    {
        // The bands of 0x428389, 0x42846d, and 0x428528 (docs/rules/roads.md, "How a border is drawn").
        Assert.AreEqual(entry, BorderRules.Entry(Post(slope, ground)));
    }

    [TestMethod]
    public void EachBorderPostDrawsItsArchiveSpriteAndTheSameInstanceForEqualPicks()
    {
        // Entries 610 to 619 each hang by their own offset across.
        using var fake = ObjectFake(payloads =>
        {
            for (var entry = 610; entry <= 619; entry++)
            {
                payloads[entry] = GraphicEntries.Square((short)(entry - 600), 0);
            }
        });
        var source = Open(fake);

        Assert.AreEqual(11, source.Draw(Post(0, 5))!.OffsetX);
        Assert.AreEqual(19, source.Draw(Post(5, 2))!.OffsetX);
        Assert.AreEqual(15, source.Draw(Post(-20, 12))!.OffsetX);
        Assert.AreSame(source.Draw(Post(0, 5)), source.Draw(Post(-3, 6)));
    }

    [TestMethod]
    public void ABorderPostTheArchiveLeavesOutDrawsNothingAndNoneWithoutObjectGraphics()
    {
        using var objects = ObjectFake();
        using var none = Fake();

        Assert.IsNull(Open(objects).Draw(Post(0, 5)));
        Assert.IsNull(Open(none).Draw(Post(0, 5)));
    }

    [TestMethod]
    public void ABuildMarkerTheArchiveLeavesOutDrawsNothing()
    {
        using var fake = ObjectFake();

        Assert.IsNull(Open(fake).Draw(BuildMarker.Large));
    }

    [TestMethod]
    public void ATileWithWaterDrawsTheRippleOfItsTileAndStepOverTheSurfaceColor()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        var ripple = source.Draw(
            new WaterRipple
            {
                Row = 0,
                X = 0,
                Y = 0,
                Tile = 0,
                Step = 0,
            }
        )!;

        Assert.AreEqual(4, ripple.Image.OffsetX);
        CollectionAssert.AreEqual(new byte[] { 40, 12, 0, 255 }, ripple.Surface);
        // The tile 1 draws the entry 634, which this install does not hold.
        Assert.IsNull(
            source.Draw(
                new WaterRipple
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Tile = 1,
                    Step = 0,
                }
            )
        );
    }

    [TestMethod]
    public void TheObjectHeadroomIsTheTallestObjectImage()
    {
        using var fake = ObjectFake();

        Assert.AreEqual(2, Open(fake).ObjectHeadroom);
    }

    [TestMethod]
    public void AnInstallWithNoObjectSpritesDrawsNoObjectAndNamesTheProblem()
    {
        using var fake = Fake();

        var source = Open(fake);

        StringAssert.Contains(source.ObjectsProblem!, "no object sprite");
        Assert.IsNull(source.Draw(Object(8)));
        Assert.IsNull(
            source.Draw(
                new WaterRipple
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Tile = 0,
                    Step = 0,
                }
            )
        );
        Assert.AreEqual(0, source.ObjectHeadroom);
        Assert.IsNotNull(source.Draw(Flat(new Terrain { Code = 5 })));
    }

    [TestMethod]
    public void AnUnreadableObjectSpriteLeavesTheObjectsUndrawnAndTheTerrainDrawn()
    {
        using var fake = ObjectFake(payloads => payloads[1260] = [1, 2, 3]);

        var source = Open(fake);

        StringAssert.Contains(source.ObjectsProblem!, "entry 1260");
        Assert.IsNull(source.Draw(Object(8)));
        Assert.IsNotNull(source.Draw(Flat(new Terrain { Code = 5 })));
    }

    // A program whose flame lists' header points past its image: list 3 starts at 0x7000.
    static byte[] BadFlameListsProgram() =>
        ProgramImage.Executable(exe =>
        {
            FlameListImage.Lists(exe);
            BitConverter.GetBytes((short)0x7000).CopyTo(exe, ProgramImage.At(FlameLists.Address) + 2 * 3);
        });

    [TestMethod]
    public void UnreadableFlameListsPlaceNoFlamesAndNameTheProblem()
    {
        using var fake = ObjectFake().Put(Install.Program, BadFlameListsProgram());

        var source = OpenWithSettlers(fake);

        StringAssert.Contains(source.ObjectsProblem!, "flame lists");
        Assert.IsEmpty(source.Flames(BuildingType.Woodcutter, earlySite: false));
    }

    [TestMethod]
    public void UnreadableFlameListsStayNamedWhereTheObjectsFailToo()
    {
        using var fake = Fake().Put(Install.Program, BadFlameListsProgram());

        var source = OpenWithSettlers(fake);

        StringAssert.Contains(source.ObjectsProblem!, "no object sprite");
        StringAssert.Contains(source.ObjectsProblem!, "flame lists");
    }

    // The settlers: tables whose row 0 holds the figure base 100 and the second base 50 and whose offsets are all 0, so a
    // carrier's figure is the entry 1950, its overlay 2600, and its second figure 3200 whatever its figure byte. The
    // sequences are those of SettlerFiguresTests (the frame 7 holds the figure byte 3).
    static RuleTables SettlerTables(sbyte[]? idleOffsets = null)
    {
        var bases = new short[2 * RuleTables.FigureBaseRowCount];
        bases[0] = 100;
        bases[1] = 50;

        return new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            MadeProgram.MadeStartStock(),
            figureBases: bases,
            idleOffsets: idleOffsets,
            jobData: MadeProgram.MadeJobData()
        );
    }

    // The figure 1950 (3 x 1, header bytes 1 and 0, offsets -1, -2), its overlay 2600, the second figure 3200 (offsets 5, 6),
    // the sequences in entry 2, and the shadow mask in entry 4 (2 x 1, offsets -1, -2).
    static FakeInstall SettlerFake(Action<Dictionary<int, byte[]>>? change = null)
    {
        var payloads = Payloads();
        payloads[2] = GraphicEntries.Sequences();
        payloads[4] = GraphicEntries.ShadowMask();
        payloads[1950] = GraphicEntries.Sprite();
        payloads[2600] = GraphicEntries.Sprite();
        payloads[3200] = GraphicEntries.Square(5, 6);
        change?.Invoke(payloads);

        return new FakeInstall().Put(Archive, ArchiveBytes.Build(3300, payloads));
    }

    static HistoryEditionSource OpenWithSettlers(FakeInstall fake, RuleTables? tables = null)
    {
        Assert.IsTrue(Install.TryOpen(fake.Root, out var install, out _));

        return HistoryEditionSource.Load(install, tables ?? SettlerTables());
    }

    static SettlerDraw Term(
        SettlerPart part,
        int player = 0,
        int frame = 7,
        WaitingNode? node = null,
        Activity? activity = null
    ) =>
        new()
        {
            Row = 3,
            X = 100,
            Y = 50,
            Part = part,
            Job = new Carrier(),
            Activity =
                activity
                ?? new WalksRoad
                {
                    Load = null,
                    Heading = new Heading { Value = 0 },
                },
            Heading = null,
            Frame = frame,
            Step = 0,
            Player = player,
            Carried = Good.None,
            Node = node,
        };

    [TestMethod]
    public void AFigureDrawsItsSpriteWithTheSettlersShadow()
    {
        using var fake = SettlerFake();

        var drawing = OpenWithSettlers(fake).Draw(Term(SettlerPart.Figure))!;

        Assert.AreEqual((3, -1, -2), (drawing.Image!.Width, drawing.Image.OffsetX, drawing.Image.OffsetY));
        Assert.AreEqual((2, -1, -2), (drawing.Shadow!.Width, drawing.Shadow.OffsetX, drawing.Shadow.OffsetY));
    }

    [TestMethod]
    public void TheOverlayIsFilledWithThePlayersColourAndHasNoShadow()
    {
        using var fake = SettlerFake();
        var source = OpenWithSettlers(fake);
        var palette = Formats.PaletteFile.Read(Payloads()[3]);

        var first = source.Draw(Term(SettlerPart.Overlay))!;
        var second = source.Draw(Term(SettlerPart.Overlay, player: 1))!;

        Assert.IsNull(first.Shadow);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, Pixel(first.Image!, 0, 0));
        CollectionAssert.AreEqual(palette.Rgba(0x40).ToArray(), Pixel(first.Image!, 1, 0));
        CollectionAssert.AreEqual(palette.Rgba(0x44).ToArray(), Pixel(second.Image!, 2, 0));
    }

    // The second figure hangs from the settler's position by its own offsets moved by the header bytes 0 and 1 of the figure
    // (here 1 and 0), and only the second figure is moved.
    [TestMethod]
    public void TheSecondFigureIsAnchoredByTheShiftOfItsFiguresHeader()
    {
        using var fake = SettlerFake();

        var second = OpenWithSettlers(fake).Draw(Term(SettlerPart.SecondFigure))!;

        Assert.AreEqual((5 + 1, 6 + 0), (second.Image!.OffsetX, second.Image.OffsetY));
        Assert.IsNull(second.Shadow);
    }

    [TestMethod]
    public void ASecondFigureWhoseFigureEntryTheArchiveLacksIsAnchoredByItsOwnOffsets()
    {
        using var fake = SettlerFake(payloads => payloads.Remove(1950));

        var second = OpenWithSettlers(fake).Draw(Term(SettlerPart.SecondFigure))!;

        Assert.AreEqual((5, 6), (second.Image!.OffsetX, second.Image.OffsetY));
    }

    // The figure bytes 3 and 4 of the carrier's walk (steps 0 and 8) pick the figures 1950 and 1951, whose headers carry the
    // same bytes: both second figures hang at the same place, so they answer one image and take one layer of the view.
    [TestMethod]
    public void SecondFiguresThatHangAtTheSamePlaceAnswerOneImageInstance()
    {
        var offsets = new short[512];
        offsets[2 * 4] = 1;
        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            MadeProgram.MadeStartStock(),
            figureBases: [.. SettlerTables().Figures.Bases],
            figureOffsets: offsets,
            jobData: MadeProgram.MadeJobData()
        );
        using var fake = SettlerFake(payloads => payloads[1951] = GraphicEntries.Sprite());
        var source = OpenWithSettlers(fake, tables);

        var first = source.Draw(Term(SettlerPart.SecondFigure))!;
        var second = source.Draw(Term(SettlerPart.SecondFigure) with { Step = 8 })!;

        Assert.AreSame(first.Image, second.Image);
    }

    [TestMethod]
    public void ASecondFigureWhoseFigureEntryIsNoSpriteIsAnchoredByItsOwnOffsets()
    {
        using var fake = SettlerFake(payloads => payloads[1950] = [1, 2, 3]);

        var second = OpenWithSettlers(fake).Draw(Term(SettlerPart.SecondFigure))!;

        Assert.AreEqual((5, 6), (second.Image!.OffsetX, second.Image.OffsetY));
    }

    [TestMethod]
    public void ASettlerThatIsDrawnTwiceGivesTheSameImageInstance()
    {
        using var fake = SettlerFake();
        var source = OpenWithSettlers(fake);

        var a = source.Draw(Term(SettlerPart.Figure))!;
        var b = source.Draw(Term(SettlerPart.Figure))!;

        Assert.AreSame(a.Image, b.Image);
        Assert.AreSame(a.Shadow, b.Shadow);
    }

    [TestMethod]
    public void ATermWhoseSpriteTheArchiveLacksDrawsNothingAndDoesNotThrow()
    {
        using var fake = SettlerFake(payloads => payloads.Remove(1950));
        var source = OpenWithSettlers(fake);

        Assert.IsNull(source.Draw(Term(SettlerPart.Figure)));
        Assert.IsNotNull(source.Draw(Term(SettlerPart.Overlay)));
    }

    [TestMethod]
    public void ATermWhoseSpriteIsUnreadableDrawsNothingAndDoesNotThrow()
    {
        using var fake = SettlerFake(payloads => payloads[2600] = [1, 2, 3]);

        Assert.IsNull(OpenWithSettlers(fake).Draw(Term(SettlerPart.Overlay)));
    }

    [TestMethod]
    public void ATermWithNoStepInTheSequencesDrawsNothing()
    {
        using var fake = SettlerFake();

        Assert.IsNull(OpenWithSettlers(fake).Draw(Term(SettlerPart.Figure, frame: 9)));
    }

    [TestMethod]
    public void ASourceLoadedWithNoTablesDrawsNoSettlerAndHasNoProblem()
    {
        using var fake = SettlerFake();
        var source = Open(fake);

        Assert.IsNull(source.Draw(Term(SettlerPart.Figure)));
        Assert.IsNull(source.SettlersProblem);
    }

    [TestMethod]
    public void AnAbsentShadowMaskLeavesTheFigureWithNoShadow()
    {
        using var fake = SettlerFake(payloads => payloads.Remove(4));

        var drawing = OpenWithSettlers(fake).Draw(Term(SettlerPart.Figure))!;

        Assert.IsNotNull(drawing.Image);
        Assert.IsNull(drawing.Shadow);
    }

    [TestMethod]
    public void AMalformedShadowMaskLeavesTheSettlersUndrawnAndNamesTheMaskAndItsEntry()
    {
        using var fake = SettlerFake(payloads => payloads[4] = [1, 2, 3]);

        var source = OpenWithSettlers(fake);

        StringAssert.Contains(source.SettlersProblem!, "the shadow mask");
        StringAssert.Contains(source.SettlersProblem!, "entry 4");
        Assert.IsNull(source.Draw(Term(SettlerPart.Figure)));
        Assert.IsNotNull(source.Draw(Flat(new Terrain { Code = 5 })));
    }

    // A waiting carrier's road directions move its image, and its shadow, by the table's offsets: the roads 0x09 move them 3
    // across and 1 up.
    [TestMethod]
    public void AWaitingCarriersRoadDirectionsMoveItsImageAndShadow()
    {
        var offsets = new sbyte[RuleTables.IdleOffsetCount];
        offsets[2 * 0x09] = 3;
        offsets[2 * 0x09 + 1] = -1;
        using var fake = SettlerFake();
        var waiting = new WaitingNode
        {
            Column = 4,
            LakeMark = false,
            Roads = new RoadSet { Bits = 0x09 },
        };

        var drawing = OpenWithSettlers(fake, SettlerTables(offsets))
            .Draw(
                Term(
                    SettlerPart.Figure,
                    node: waiting,
                    activity: new WaitsAtFlag
                    {
                        Flag = 0,
                        Direction = 0,
                        Approach = new Heading { Value = 0 }.Retried,
                    }
                )
            )!;

        Assert.AreEqual((-1 + 3, -2 - 1), (drawing.Image!.OffsetX, drawing.Image.OffsetY));
        Assert.AreEqual((-1 + 3, -2 - 1), (drawing.Shadow!.OffsetX, drawing.Shadow.OffsetY));
    }
}
