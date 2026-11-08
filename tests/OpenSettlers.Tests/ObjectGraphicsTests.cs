using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectGraphicsTests
{
    const string Archive = "loca/SPAD.PA";

    // A 3 x 1 sprite: pixel 0 is skipped, pixel 1 is drawn with palette index 0, pixel 2 with index 7.
    static byte[] Sprite(short offsetX = -1, short offsetY = -2) =>
        GraphicFileTests.Entry(1, 0, 3, 1, offsetX, offsetY, [1, 2, 0, 7, 0, 0]);

    static Dictionary<int, byte[]> Payloads(bool flag = false)
    {
        var payloads = new Dictionary<int, byte[]>
        {
            [3] = new byte[768],
            [1250] = Sprite(),
            [1251] = GraphicFileTests.Entry(1, 0, 2, 2, 5, 6, [0, 2, 3, 3, 0, 0, 0, 1, 4, 0, 0]),
            [1369] = Sprite(),
            [ObjectSprites.CastleEntry] = GraphicFileTests.Entry(1, 0, 2, 2, 7, 8, [0, 2, 3, 3, 0, 0, 0, 1, 4, 0, 0]),
        };
        if (flag)
        {
            payloads[ObjectSprites.FlagEntry] = GraphicFileTests.Entry(
                1,
                0,
                2,
                2,
                9,
                4,
                [0, 2, 3, 3, 0, 0, 0, 1, 4, 0, 0]
            );
        }

        return payloads;
    }

    static OpenSettlers.Assets.Install Open(FakeInstall fake)
    {
        Assert.IsTrue(
            OpenSettlers.Assets.Install.TryOpen(
                OpenSettlers.Launch.LaunchArgs.Parse(["--game", fake.Root], null),
                out var install,
                out _
            )
        );

        return install;
    }

    static FakeInstall Install(Dictionary<int, byte[]> payloads, int rows = 1500) =>
        new FakeInstall().Put(Archive, ArchiveBytes.Build(rows, payloads));

    // A 2 x 1 shadow mask.
    static byte[] ShadowMask() => MaskFileTests.Entry(2, 1, 1, 1, 0, 0);

    [TestMethod]
    public void TheCoverageOfAnImageIsKeptApartFromItsPaletteIndices()
    {
        var image = ObjectImage.From(SpriteFile.Read(Sprite()));

        CollectionAssert.AreEqual(new byte[] { 0, 0, 7 }, image.Indices);
        CollectionAssert.AreEqual(new byte[] { 0, 255, 255 }, image.Coverage);
        Assert.AreEqual(-1, image.OffsetX);
        Assert.AreEqual(-2, image.OffsetY);
    }

    [TestMethod]
    public void LoadingReadsEverySpriteOfTheObjectsAndPadsThemToOneLayerSize()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.HasCount(ObjectGraphics.SpriteLayerCount, graphics.Images);
        Assert.HasCount(ObjectGraphics.SpriteLayerCount, graphics.ShadowImages);
        Assert.AreEqual(3, graphics.LayerWidth);
        Assert.AreEqual(2, graphics.LayerHeight);
        Assert.AreEqual(3, graphics.Images[ObjectGraphics.Layer(1250)].Width);
        Assert.AreEqual(2, graphics.Images[ObjectGraphics.Layer(1251)].Height);
        Assert.AreEqual(5, graphics.Images[ObjectGraphics.Layer(1251)].OffsetX);
        Assert.AreEqual(3 * 2, graphics.Images[0].Coverage.Length);
        Assert.AreEqual(0, ObjectGraphics.Layer(1250));
        Assert.AreEqual(119, ObjectGraphics.Layer(1369));
    }

    [TestMethod]
    public void TheCastleSpriteIsTheLayerAfterTheObjectSprites()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        // Emulator: peek:44fd2f:4 prints 66B8B200, the stub of the building type 0xe0 loading the object number 0xB2
        // (178); the entry is that plus the 1250 of 0x44da4d (ADR 0008).
        Assert.AreEqual(1428, ObjectSprites.CastleEntry);
        Assert.AreEqual(ObjectGraphics.EntryCount, ObjectGraphics.Layer(ObjectSprites.CastleEntry));
        Assert.AreEqual(7, graphics.Images[ObjectGraphics.Layer(ObjectSprites.CastleEntry)].OffsetX);
    }

    [TestMethod]
    public void TheFlagSpriteIsTheLayerAfterTheCastlesAndItsImageIsRead()
    {
        using var fake = Install(Payloads(flag: true));

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(ObjectGraphics.EntryCount + 1, ObjectGraphics.Layer(ObjectSprites.FlagEntry));
        Assert.AreEqual(9, graphics.Images[ObjectGraphics.Layer(ObjectSprites.FlagEntry)].OffsetX);
    }

    [TestMethod]
    public void TheGoodsSpritesAreTheLayersAfterTheHutsAndTheLogsImageIsRead()
    {
        var payloads = Payloads();
        payloads[ObjectSprites.GoodEntry(new Good(7))] = GraphicFileTests.Entry(
            1,
            0,
            2,
            2,
            3,
            1,
            [0, 2, 3, 3, 0, 0, 0, 1, 4, 0, 0]
        );
        using var fake = Install(payloads);

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(ObjectGraphics.EntryCount + 3, ObjectGraphics.Layer(ObjectSprites.GoodEntry(new Good(0))));
        Assert.AreEqual(ObjectGraphics.EntryCount + 10, ObjectGraphics.Layer(ObjectSprites.GoodEntry(new Good(7))));
        Assert.AreEqual(3, graphics.Images[ObjectGraphics.Layer(ObjectSprites.GoodEntry(new Good(7)))].OffsetX);
        Assert.AreEqual(ObjectGraphics.EntryCount + 3 + 32 + 16 + 3, graphics.Images.Count);
        Assert.AreEqual(ObjectGraphics.EntryCount + 3 + 32 + 16 + 2, ObjectGraphics.Layer(ObjectSprites.FlagEntry + 3));
        Assert.AreEqual(ObjectGraphics.EntryCount + 3 + 32, ObjectGraphics.Layer(ObjectSprites.WaterFirstEntry));
    }

    [TestMethod]
    public void TheHutSpriteIsTheLayerAfterTheFlagsAndItsImageIsRead()
    {
        var payloads = Payloads();
        payloads[ObjectSprites.WoodcutterEntry] = GraphicFileTests.Entry(
            1,
            0,
            2,
            2,
            11,
            3,
            [0, 2, 3, 3, 0, 0, 0, 1, 4, 0, 0]
        );
        using var fake = Install(payloads);

        var graphics = ObjectGraphics.Load(Open(fake));

        // Emulator: peek:44e63b:4 prints 66B8A800, the stub the jump table at 0x44e3aa reaches for the building type 2 (the
        // woodcutter's hut) loading the object number 0xA8 (168); the entry is that plus the 1250 of 0x44da4d (ADR 0008).
        Assert.AreEqual(1418, ObjectSprites.WoodcutterEntry);
        Assert.AreEqual(ObjectGraphics.EntryCount + 2, ObjectGraphics.Layer(ObjectSprites.WoodcutterEntry));
        Assert.AreEqual(11, graphics.Images[ObjectGraphics.Layer(ObjectSprites.WoodcutterEntry)].OffsetX);
    }

    [TestMethod]
    public void AnAbsentEntryIsAnEmptyImage()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(0, graphics.Images[ObjectGraphics.Layer(1300)].Width);
        Assert.IsTrue(Array.TrueForAll(graphics.Images[ObjectGraphics.Layer(1300)].Coverage, c => c == 0));
    }

    [TestMethod]
    public void AnArchiveWithNoObjectSpriteIsRefusedWithAMessage()
    {
        using var fake = Install(new Dictionary<int, byte[]> { [3] = new byte[768] });

        var problem = Assert.ThrowsExactly<InvalidDataException>(() => ObjectGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, "no object sprite");
        StringAssert.Contains(problem.Message, "1250");
    }

    [TestMethod]
    public void AnUnreadableSpriteNamesItsEntry()
    {
        var payloads = Payloads();
        payloads[1260] = [1, 2, 3];
        using var fake = Install(payloads);

        var problem = Assert.ThrowsExactly<InvalidDataException>(() => ObjectGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, "entry 1260");
    }

    [TestMethod]
    public void AMissingArchiveIsAnErrorThatNamesIt()
    {
        using var fake = new FakeInstall();

        var problem = Assert.ThrowsExactly<FileNotFoundException>(() => ObjectGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, Archive);
    }

    [TestMethod]
    public void AQuadHangsFromTheDrawPositionByTheImagesOffsetsAndAnEmptyImageHasNone()
    {
        var images = new[]
        {
            new ObjectImage(3, 1, -1, -2, new byte[3], new byte[] { 0, 255, 255 }),
            new ObjectImage(0, 0, 0, 0, [], []),
        };
        var draws = new[]
        {
            new ObjectDraw(0, 1250, 100, 50),
            new ObjectDraw(0, 1251, 132, 50),
            new ObjectDraw(20, 1250, 100, 450),
        };

        var bands = ObjectQuads.Bands(draws, images, [ObjectImage.Empty, ObjectImage.Empty], 21, 16);

        Assert.HasCount(2, bands);
        CollectionAssert.AreEqual(new[] { new ObjectQuad(99, 48, 3, 1, 0) }, bands[0]);
        Assert.AreEqual(448, bands[1][0].Y);
    }

    [TestMethod]
    public void AShadowIsDrawnFromTheShadowArrayRightBeforeItsSpriteFromItsOwnOffsets()
    {
        var images = new ObjectImage[ObjectGraphics.SpriteLayerCount];
        Array.Fill(images, ObjectImage.Empty);
        images[0] = new ObjectImage(3, 1, -1, -2, new byte[3], new byte[] { 0, 255, 255 });
        images[1] = images[0];
        var shadows = new ObjectImage[ObjectGraphics.SpriteLayerCount];
        Array.Fill(shadows, ObjectImage.Empty);
        shadows[ObjectGraphics.Layer(1250)] = new ObjectImage(4, 2, 1, -1, new byte[8], new byte[8]);

        var bands = ObjectQuads.Bands(
            [new ObjectDraw(0, 1250, 100, 50), new ObjectDraw(0, 1251, 200, 50)],
            images,
            shadows,
            1,
            16
        );

        CollectionAssert.AreEqual(
            new[]
            {
                new ObjectQuad(101, 49, 4, 2, 0, Shadow: true),
                new ObjectQuad(99, 48, 3, 1, 0),
                new ObjectQuad(199, 48, 3, 1, 1),
            },
            bands[0]
        );
    }

    [TestMethod]
    public void NoShadowListDrawsTheSpritesWithoutShadows()
    {
        var images = new ObjectImage[ObjectGraphics.SpriteLayerCount];
        Array.Fill(images, ObjectImage.Empty);
        images[0] = new ObjectImage(3, 1, -1, -2, new byte[3], new byte[] { 0, 255, 255 });

        var bands = ObjectQuads.Bands([new ObjectDraw(0, 1250, 100, 50)], images, [], 1, 16);

        CollectionAssert.AreEqual(new[] { new ObjectQuad(99, 48, 3, 1, 0) }, bands[0]);
    }

    [TestMethod]
    public void AShadowListOfAnotherLengthThanTheSpritesIsRefused()
    {
        var images = new ObjectImage[ObjectGraphics.SpriteLayerCount];
        Array.Fill(images, ObjectImage.Empty);

        Assert.ThrowsExactly<ArgumentException>(() =>
            ObjectQuads.Bands([new ObjectDraw(0, 1250, 100, 50)], images, [ObjectImage.Empty], 1, 16)
        );
    }

    [TestMethod]
    public void TheShadowsSitInTheirOwnLayersPaddedToTheLargestShadowMask()
    {
        var payloads = Payloads();
        payloads[1250 + ObjectGraphics.ShadowOffset] = ShadowMask();
        using var fake = Install(payloads, rows: 1700);

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(2, graphics.ShadowImages[ObjectGraphics.Layer(1250)].Width);
        Assert.AreEqual(2, graphics.ShadowLayerWidth);
        Assert.AreEqual(1, graphics.ShadowLayerHeight);
        Assert.AreEqual(3, graphics.LayerWidth);
        Assert.AreEqual(2, graphics.ShadowImages[ObjectGraphics.Layer(1251)].Coverage.Length);
        Assert.AreEqual(0, graphics.ShadowImages[ObjectGraphics.Layer(ObjectSprites.GoodEntry(7))].Width);
        Assert.AreEqual(0, graphics.ShadowImages[ObjectGraphics.Layer(ObjectSprites.WaterFirstEntry)].Width);
    }

    [TestMethod]
    public void AnArchiveWithNoShadowMaskLoadsAndItsObjectsStillDraw()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.IsTrue(graphics.ShadowImages.All(image => image.Width == 0));
        Assert.IsGreaterThanOrEqualTo(1, graphics.ShadowLayerWidth);
        Assert.IsGreaterThanOrEqualTo(1, graphics.ShadowLayerHeight);
        var bands = ObjectQuads.Bands(
            [new ObjectDraw(0, 1250, 100, 50)],
            graphics.Images,
            graphics.ShadowImages,
            1,
            16
        );
        CollectionAssert.AreEqual(new[] { new ObjectQuad(99, 48, 3, 1, 0) }, bands[0]);
    }

    [TestMethod]
    public void AnUnreadableShadowMaskNamesItAndItsEntry()
    {
        var payloads = Payloads();
        payloads[1250 + ObjectGraphics.ShadowOffset] = [1, 2, 3];
        using var fake = Install(payloads, rows: 1700);

        var problem = Assert.ThrowsExactly<InvalidDataException>(() => ObjectGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, "the shadow mask");
        StringAssert.Contains(problem.Message, "entry 1500");
    }

    [TestMethod]
    public void AShadowMaskCoversItsPixelsInTheShadowColor()
    {
        var mask = new Mask
        {
            Header = new GraphicHeader
            {
                First = 0,
                Second = 0,
                Width = 2,
                Height = 1,
                OffsetX = 3,
                OffsetY = -4,
            },
            Covered = [false, true],
        };

        var image = ObjectImage.Shadow(mask);

        CollectionAssert.AreEqual(new byte[] { 0x80, 0x80 }, image.Indices);
        CollectionAssert.AreEqual(new byte[] { 0, 255 }, image.Coverage);
        Assert.AreEqual(3, image.OffsetX);
        Assert.AreEqual(-4, image.OffsetY);
    }
}
