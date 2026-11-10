using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

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
        Assert.IsTrue(
            Install.TryOpen(OpenSettlers.Launch.LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _)
        );

        return HistoryEditionSource.Load(install);
    }

    static FakeInstall Fake(Dictionary<int, byte[]>? payloads = null) =>
        new FakeInstall().Put(Archive, ArchiveBytes.Build(311, payloads ?? Payloads()));

    static TerrainTriangle Flat(Terrain terrain, bool upper = true) => new(0, 0, upper, terrain, 5, 5, 5, 100, 50);

    static byte[] Pixel(ObjectImage image, int x, int y) => image.Rgba[((y * image.Width + x) * 4)..][..4];

    [TestMethod]
    public void AFlatUpperTriangleOfGrassIsDrawnWithMask100AndTexture264()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(Flat(new Terrain(5)))!;

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

        var image = Open(fake).Draw(Flat(new Terrain(5), upper: false))!;

        Assert.AreEqual(181 - 100, image.OffsetY);
    }

    [TestMethod]
    public void TheTextureRepeatsDownEveryTwentyRowsFromTheTopOfTheMask()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(Flat(new Terrain(5)))!;

        CollectionAssert.AreEqual(new byte[] { 4 * 4 + 4, 0, 0, 255 }, Pixel(image, 1, 21));
    }

    [TestMethod]
    public void WaterIsDrawnWithTheLastTexture()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(Flat(new Terrain(0)))!;

        CollectionAssert.AreEqual(new byte[] { 32 * 4, 0, 0, 255 }, Pixel(image, 1, 0));
    }

    [TestMethod]
    public void AHeightStepBeyondFourDrawsNothing()
    {
        using var fake = Fake();
        var source = Open(fake);

        Assert.IsNull(source.Draw(new TerrainTriangle(0, 0, true, new Terrain(5), 5, 0, 5, 100, 50)));
        Assert.IsNull(source.Draw(new TerrainTriangle(0, 0, false, new Terrain(5), 5, 5, 0, 100, 50)));
    }

    [TestMethod]
    public void AHeightIsItsFiveLowBits()
    {
        using var fake = Fake();
        var source = Open(fake);

        var low = source.Draw(new TerrainTriangle(0, 0, true, new Terrain(5), 1, 1, 1, 100, 50));
        var high = source.Draw(new TerrainTriangle(0, 0, true, new Terrain(5), 33, 1, 1, 100, 50));

        Assert.AreSame(low, high);
    }

    [TestMethod]
    public void TermsThatPickTheSameMaskAndTextureShareOneImage()
    {
        using var fake = Fake();
        var source = Open(fake);

        var a = source.Draw(Flat(new Terrain(5)));
        var b = source.Draw(Flat(new Terrain(5)) with { X = 300, Y = 7 });
        var other = source.Draw(Flat(new Terrain(12)));

        Assert.AreSame(a, b);
        Assert.AreNotSame(a, other);
    }

    [TestMethod]
    public void ALevelEastStripOfPlainLandIsDrawnWithMask234AndTexture301()
    {
        using var fake = Fake();

        var image = Open(fake).Draw(new RoadStrip(100, 200, RoadDirection.East, 0, 0, new Terrain(5)))!;

        Assert.AreEqual(234 - 230, image.OffsetY);
        Assert.AreEqual(1, image.OffsetX);
        CollectionAssert.AreEqual(new byte[] { 100 + 4, 0, 0, 255 }, Pixel(image, 1, 0));
    }

    [TestMethod]
    public void AStripBeyondAHeightDifferenceOfFourDrawsNothing()
    {
        using var fake = Fake();

        Assert.IsNull(Open(fake).Draw(new RoadStrip(100, 200, RoadDirection.South, 5, 0, new Terrain(5))));
        Assert.IsNull(Open(fake).Draw(new RoadStrip(100, 200, RoadDirection.South, -5, 0, new Terrain(5))));
    }

    [TestMethod]
    public void TheMaskOfAStripFollowsItsDirectionAndDifference()
    {
        using var fake = Fake();
        var source = Open(fake);

        Assert.AreEqual(
            243 - 230 + 2,
            source.Draw(new RoadStrip(0, 0, RoadDirection.SouthEast, 2, 0, new Terrain(5)))!.OffsetY
        );
        Assert.AreEqual(
            252 - 230 - 3, // the south masks start at 248 for -4
            source.Draw(new RoadStrip(0, 0, RoadDirection.South, -3, 0, new Terrain(5)))!.OffsetY
        );
    }

    [TestMethod]
    public void TheTextureOfAStripFollowsSlopeWaterMountainAndSnow()
    {
        using var fake = Fake();
        var source = Open(fake);
        byte Red(int slope, int ground) =>
            Pixel(source.Draw(new RoadStrip(0, 0, RoadDirection.East, 0, slope, new Terrain(ground)))!, 1, 0)[0];

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
        Assert.IsNull(source.Draw(new RoadStrip(0, 0, RoadDirection.East, 0, 0, new Terrain(5))));
        Assert.IsNotNull(source.Draw(Flat(new Terrain(5))));
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
        foreach (var (entry, bytes) in ObjectGraphicsTests.Payloads(flag: true))
        {
            payloads[entry] = bytes;
        }

        payloads[3][3 * 8] = 40;
        payloads[3][3 * 8 + 1] = 12;
        payloads[ObjectSprites.FlagEntry + 1] = ObjectGraphicsTests.Square(10, 5);
        payloads[ObjectSprites.GoodEntry(new Good(7))] = ObjectGraphicsTests.Square(3, 1);
        payloads[ObjectSprites.WaterEntry(0, 0)] = ObjectGraphicsTests.Square(4, 4);
        payloads[1250 + ObjectGraphics.ShadowOffset] = ObjectGraphicsTests.ShadowMask();
        change?.Invoke(payloads);

        return new FakeInstall().Put(Archive, ArchiveBytes.Build(1700, payloads));
    }

    static MapObjectDraw Object(int code, ushort step = 0) => new(0, 10, 20, new MapObject((byte)code), step);

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
        Assert.AreEqual(10, source.Draw(new MapObjectDraw(0, 0, 0, MapObject.Flag, 8))!.Image!.OffsetX);
        Assert.AreEqual(9, source.Draw(new MapObjectDraw(0, 0, 0, MapObject.Flag, 0))!.Image!.OffsetX);
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
        using var fake = ObjectFake(payloads =>
            payloads[ObjectSprites.WoodcutterEntry] = ObjectGraphicsTests.Square(11, 3)
        );
        var source = Open(fake);

        Assert.AreEqual(7, source.Draw(new BuildingDraw(0, 0, 0, BuildingType.Castle))!.Image!.OffsetX);
        Assert.AreEqual(11, source.Draw(new BuildingDraw(0, 0, 0, BuildingType.Woodcutter))!.Image!.OffsetX);
        Assert.IsNull(source.Draw(new BuildingDraw(0, 0, 0, BuildingType.Storehouse)));
    }

    [TestMethod]
    public void AGoodDrawsItsSpriteByItsLowFiveBitsAndNoShadow()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        var log = source.Draw(new GoodAtFlag(0, 0, 0, new Good(7)))!;

        Assert.AreEqual(3, log.Image!.OffsetX);
        Assert.IsNull(log.Shadow);
        Assert.AreSame(log, source.Draw(new GoodAtFlag(0, 0, 0, new Good(0x27))));
    }

    [TestMethod]
    public void ATileWithWaterDrawsTheRippleOfItsTileAndStepOverTheSurfaceColor()
    {
        using var fake = ObjectFake();
        var source = Open(fake);

        var ripple = source.Draw(new WaterRipple(0, 0, 0, 0, 0))!;

        Assert.AreEqual(4, ripple.Image.OffsetX);
        CollectionAssert.AreEqual(new byte[] { 40, 12, 0, 255 }, ripple.Surface);
        // The tile 1 draws the entry 634, which this install does not hold.
        Assert.IsNull(source.Draw(new WaterRipple(0, 0, 0, 1, 0)));
    }

    [TestMethod]
    public void TheObjectHeadroomIsTheTallestObjectImage()
    {
        using var fake = ObjectFake();

        Assert.AreEqual(2, Open(fake).ObjectHeadroom);
    }

    [TestMethod]
    public void TheFlagAtRestGivesTheFlagsGeometry()
    {
        using var fake = ObjectFake();

        var image = ObjectDraws.FlagImage(Open(fake))!;

        Assert.AreEqual((9, 4), (image.OffsetX, image.OffsetY));
        Assert.IsNull(ObjectDraws.FlagImage(null));
    }

    [TestMethod]
    public void AnInstallWithNoObjectSpritesDrawsNoObjectAndNamesTheProblem()
    {
        using var fake = Fake();

        var source = Open(fake);

        StringAssert.Contains(source.ObjectsProblem!, "no object sprite");
        Assert.IsNull(source.Draw(Object(8)));
        Assert.IsNull(source.Draw(new WaterRipple(0, 0, 0, 0, 0)));
        Assert.AreEqual(0, source.ObjectHeadroom);
        Assert.IsNotNull(source.Draw(Flat(new Terrain(5))));
    }

    [TestMethod]
    public void AnUnreadableObjectSpriteLeavesTheObjectsUndrawnAndTheTerrainDrawn()
    {
        using var fake = ObjectFake(payloads => payloads[1260] = [1, 2, 3]);

        var source = Open(fake);

        StringAssert.Contains(source.ObjectsProblem!, "entry 1260");
        Assert.IsNull(source.Draw(Object(8)));
        Assert.IsNotNull(source.Draw(Flat(new Terrain(5))));
    }
}
