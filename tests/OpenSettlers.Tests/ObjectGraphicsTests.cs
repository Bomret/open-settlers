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
    internal static byte[] Sprite(short offsetX = -1, short offsetY = -2) =>
        GraphicFileTests.Entry(1, 0, 3, 1, offsetX, offsetY, [1, 2, 0, 7, 0, 0]);

    // A 2 x 2 sprite with the given offsets.
    internal static byte[] Square(short offsetX, short offsetY) =>
        GraphicFileTests.Entry(1, 0, 2, 2, offsetX, offsetY, [0, 2, 3, 3, 0, 0, 0, 1, 4, 0, 0]);

    // The red components of index 7 and the shadow color 0x80 are 28 and 36. Index 0 reads as black.
    internal static byte[] Palette()
    {
        var palette = new byte[768];
        palette[3 * 7] = 28;
        palette[3 * 0x80] = 36;

        return palette;
    }

    internal static Dictionary<int, byte[]> Payloads(bool flag = false)
    {
        var payloads = new Dictionary<int, byte[]>
        {
            [3] = Palette(),
            [1250] = Sprite(),
            [1251] = Square(5, 6),
            [1369] = Sprite(),
            [ObjectSprites.CastleEntry] = Square(7, 8),
        };
        if (flag)
        {
            payloads[ObjectSprites.FlagEntry] = Square(9, 4);
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
    internal static byte[] ShadowMask() => MaskFileTests.Entry(2, 1, 1, 1, 0, 0);

    [TestMethod]
    public void ACoveredPixelIsOpaqueEvenAtPaletteIndexZeroAndAnUncoveredPixelIsTransparent()
    {
        var image = ObjectImage.From(SpriteFile.Read(Sprite()), PaletteFile.Read(Palette()));

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0, 0, 0, 255, 28, 0, 0, 255 }, image.Rgba);
        Assert.AreEqual(-1, image.OffsetX);
        Assert.AreEqual(-2, image.OffsetY);
    }

    [TestMethod]
    public void APaddedImageKeepsItsSizeAndOffsetsAndItsRowsMoveToTheLayersStride()
    {
        var image = new ObjectImage(2, 2, -1, -2, [1, 1, 1, 255, 2, 2, 2, 255, 3, 3, 3, 255, 4, 4, 4, 255]);

        var padded = image.Padded(3, 3);

        Assert.AreEqual((2, 2, -1, -2), (padded.Width, padded.Height, padded.OffsetX, padded.OffsetY));
        CollectionAssert.AreEqual(
            new byte[]
            {
                1,
                1,
                1,
                255,
                2,
                2,
                2,
                255,
                0,
                0,
                0,
                0,
                3,
                3,
                3,
                255,
                4,
                4,
                4,
                255,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
            },
            padded.Rgba
        );
    }

    [TestMethod]
    public void LoadingReadsEverySpriteOfTheObjectsByItsEntryAtItsOwnSize()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(3, graphics.Sprites[1250].Width);
        Assert.AreEqual(2, graphics.Sprites[1251].Height);
        Assert.AreEqual(5, graphics.Sprites[1251].OffsetX);
        Assert.AreEqual(3 * 1, graphics.Sprites[1250].Rgba.Length / 4);
        Assert.AreEqual(3, graphics.Sprites[1369].Width);
    }

    [TestMethod]
    public void TheTallestSpriteIsTheHeadroom()
    {
        using var fake = Install(Payloads());

        Assert.AreEqual(2, ObjectGraphics.Load(Open(fake)).Headroom);
    }

    [TestMethod]
    public void TheCastleSpriteIsEntry1428()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        // Emulator: peek:44fd2f:4 prints 66B8B200, the stub of the building type 0xe0 loading the object number 0xB2
        // (178); the entry is that plus the 1250 of 0x44da4d (ADR 0008).
#pragma warning disable MSTEST0032 // The assertion pins the constant to its value.
        Assert.AreEqual(1428, ObjectSprites.CastleEntry);
#pragma warning restore MSTEST0032
        Assert.AreEqual(7, graphics.Sprites[ObjectSprites.CastleEntry].OffsetX);
    }

    [TestMethod]
    public void TheFlagSpriteIsRead()
    {
        using var fake = Install(Payloads(flag: true));

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(9, graphics.Sprites[ObjectSprites.FlagEntry].OffsetX);
    }

    [TestMethod]
    public void TheGoodsAndTheFlagsFurtherFramesAndTheWaterAreRead()
    {
        var payloads = Payloads();
        payloads[ObjectSprites.GoodEntry(new Good { Code = 7 })] = Square(3, 1);
        payloads[ObjectSprites.FlagEntry + 3] = Square(2, 2);
        payloads[ObjectSprites.WaterFirstEntry] = Square(4, 4);
        using var fake = Install(payloads);

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(3, graphics.Sprites[ObjectSprites.GoodEntry(new Good { Code = 7 })].OffsetX);
        Assert.AreEqual(2, graphics.Sprites[ObjectSprites.FlagEntry + 3].OffsetX);
        Assert.AreEqual(4, graphics.Sprites[ObjectSprites.WaterFirstEntry].OffsetX);
    }

    [TestMethod]
    public void TheHutSpriteIsRead()
    {
        var payloads = Payloads();
        payloads[ObjectSprites.WoodcutterEntry] = Square(11, 3);
        using var fake = Install(payloads);

        var graphics = ObjectGraphics.Load(Open(fake));

        // Emulator: peek:44e63b:4 prints 66B8A800, the stub the jump table at 0x44e3aa reaches for the building type 2 (the
        // woodcutter's hut) loading the object number 0xA8 (168); the entry is that plus the 1250 of 0x44da4d (ADR 0008).
#pragma warning disable MSTEST0032 // The assertion pins the constant to its value.
        Assert.AreEqual(1418, ObjectSprites.WoodcutterEntry);
#pragma warning restore MSTEST0032
        Assert.AreEqual(11, graphics.Sprites[ObjectSprites.WoodcutterEntry].OffsetX);
    }

    // A site draws the scaffold of its type (docs/rules/construction.md, "Drawing a site"), so both scaffolds are read with their shadows.
    [TestMethod]
    public void TheScaffoldSpritesAreRead()
    {
        var payloads = Payloads();
        payloads[ObjectSprites.ScaffoldSmallEntry] = Square(6, 2);
        payloads[ObjectSprites.ScaffoldLargeEntry] = Square(8, 2);
        payloads[ObjectSprites.ScaffoldSmallEntry + ObjectGraphics.ShadowOffset] = ShadowMask();
        using var fake = Install(payloads, rows: 1700);

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(6, graphics.Sprites[ObjectSprites.ScaffoldSmallEntry].OffsetX);
        Assert.AreEqual(8, graphics.Sprites[ObjectSprites.ScaffoldLargeEntry].OffsetX);
        Assert.IsTrue(graphics.Shadows.ContainsKey(ObjectSprites.ScaffoldSmallEntry));
    }

    [TestMethod]
    public void AnAbsentEntryHasNoSprite()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.IsFalse(graphics.Sprites.ContainsKey(1300));
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
    public void TheShadowOfAnObjectIsTheMaskAtItsEntryPlus250AndTheGoodsAndWaterHaveNone()
    {
        var payloads = Payloads();
        payloads[1250 + ObjectGraphics.ShadowOffset] = ShadowMask();
        payloads[ObjectSprites.GoodEntry(new Good { Code = 7 }) + ObjectGraphics.ShadowOffset] = ShadowMask();
        using var fake = Install(payloads, rows: 1700);

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.AreEqual(2, graphics.Shadows[1250].Width);
        Assert.IsFalse(graphics.Shadows.ContainsKey(1251));
        Assert.IsFalse(graphics.Shadows.ContainsKey(ObjectSprites.GoodEntry(new Good { Code = 7 })));
        Assert.IsFalse(graphics.Shadows.ContainsKey(ObjectSprites.WaterFirstEntry));
    }

    [TestMethod]
    public void AnArchiveWithNoShadowMaskLoadsWithNoShadows()
    {
        using var fake = Install(Payloads());

        var graphics = ObjectGraphics.Load(Open(fake));

        Assert.IsEmpty(graphics.Shadows);
        Assert.IsNotEmpty(graphics.Sprites);
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

        var image = ObjectImage.Shadow(mask, PaletteFile.Read(Palette()));

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 36, 0, 0, 255 }, image.Rgba);
        Assert.AreEqual(3, image.OffsetX);
        Assert.AreEqual(-4, image.OffsetY);
    }
}
