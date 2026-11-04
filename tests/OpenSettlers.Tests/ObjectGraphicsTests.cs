using System;
using System.Collections.Generic;
using System.IO;
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

    static FakeInstall Install(Dictionary<int, byte[]> payloads) =>
        new FakeInstall().Put(Archive, ArchiveBytes.Build(1500, payloads));

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

        Assert.HasCount(
            ObjectGraphics.EntryCount + 3 + ObjectSprites.GoodsCount + ObjectSprites.WaterCount,
            graphics.Images
        );
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
        payloads[ObjectSprites.GoodEntry(7)] = GraphicFileTests.Entry(
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

        Assert.AreEqual(ObjectGraphics.EntryCount + 3, ObjectGraphics.Layer(ObjectSprites.GoodEntry(0)));
        Assert.AreEqual(ObjectGraphics.EntryCount + 10, ObjectGraphics.Layer(ObjectSprites.GoodEntry(7)));
        Assert.AreEqual(3, graphics.Images[ObjectGraphics.Layer(ObjectSprites.GoodEntry(7))].OffsetX);
        Assert.AreEqual(ObjectGraphics.EntryCount + 3 + 32 + 16, graphics.Images.Count);
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

        var bands = ObjectQuads.Bands(draws, images, 21, 16);

        Assert.HasCount(2, bands);
        CollectionAssert.AreEqual(new[] { new ObjectQuad(99, 48, 3, 1, 0) }, bands[0]);
        Assert.AreEqual(448, bands[1][0].Y);
    }
}
