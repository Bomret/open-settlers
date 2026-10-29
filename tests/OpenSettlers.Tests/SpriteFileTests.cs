using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SpriteFileTests
{
    // Colors 1 and 2 are red and green at 6 bits; color 0 is black.
    public static Palette Colors()
    {
        var bytes = new byte[768];
        bytes[3] = 0xFC;
        bytes[7] = 0xFC;

        return PaletteFile.Read(bytes);
    }

    static byte[] Sprite(ushort width, ushort height, short x, short y, params byte[] body) =>
        GraphicFileTests.Entry(1, 0, width, height, x, y, body);

    [TestMethod]
    public void ARowIsRunsOfSkippedAndDrawnPixelsEndedByAZeroCount()
    {
        // Row 0: skip 1, draw 2 (5, 6), end. Row 1: draw 1 (7), skip 1, draw 1 (8), end.
        var entry = Sprite(4, 2, 0, 0, 1, 2, 5, 6, 0, 0, 0, 1, 7, 1, 1, 8, 0, 0);

        var sprite = SpriteFile.Read(entry);

        CollectionAssert.AreEqual(new byte[] { 0, 5, 6, 0, 7, 0, 8, 0 }, sprite.Indices);
        CollectionAssert.AreEqual(new[] { false, true, true, false, true, false, true, false }, sprite.Drawn);
    }

    [TestMethod]
    public void TheSkipOfTheEndingPairIsIgnored()
    {
        var sprite = SpriteFile.Read(Sprite(2, 1, 0, 0, 0, 1, 9, 200, 0));

        CollectionAssert.AreEqual(new[] { true, false }, sprite.Drawn);
    }

    [TestMethod]
    public void ARowOfNothingIsDrawnAsTransparent()
    {
        var sprite = SpriteFile.Read(Sprite(3, 2, 0, 0, 0, 0, 0, 0));

        Assert.IsFalse(sprite.Drawn.Any(d => d));
    }

    [TestMethod]
    public void ThePixelsAreDrawnInTheGamesPaletteOverATransparentBackground()
    {
        var sprite = SpriteFile.Read(Sprite(2, 1, 0, 0, 1, 1, 2, 0, 0));

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0, 252, 0, 255 }, sprite.ToRgba(Colors()));
    }

    [TestMethod]
    public void ADrawnPixelOfIndexZeroIsOpaqueBlack()
    {
        var sprite = SpriteFile.Read(Sprite(2, 1, 0, 0, 1, 1, 0, 0, 0));

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0, 0, 0, 255 }, sprite.ToRgba(Colors()));
    }

    [TestMethod]
    public void AOneColorSpriteDrawsEveryDrawnPixelInThatColorAndIgnoresItsStoredPixels()
    {
        var sprite = SpriteFile.Read(Sprite(4, 1, 0, 0, 0, 2, 2, 2, 1, 1, 2, 0, 0));

        CollectionAssert.AreEqual(
            new byte[] { 252, 0, 0, 255, 252, 0, 0, 255, 0, 0, 0, 0, 252, 0, 0, 255 },
            sprite.ToRgba(Colors(), 1)
        );
    }

    [TestMethod]
    public void TheSignedOffsetsAreKeptAndOnlyDescribeWhereTheSpriteSits()
    {
        var sprite = SpriteFile.Read(Sprite(1, 1, -32768, 32767, 0, 1, 1, 0, 0));

        Assert.AreEqual(-32768, sprite.Header.OffsetX);
        Assert.AreEqual(32767, sprite.Header.OffsetY);
        Assert.AreEqual(1, sprite.Indices.Length);
    }

    [TestMethod]
    public void ARowWhoseRunsOverrunTheWidthIsUnreadableAndNamesTheRow()
    {
        // Row 1: skip 1, draw 2 in a width of 2.
        var entry = Sprite(2, 2, 0, 0, 0, 1, 1, 0, 0, 1, 2, 1, 2, 0, 0);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => SpriteFile.Read(entry));

        StringAssert.Contains(failure.Message, "row 1");
        StringAssert.Contains(failure.Message, "width 2");
    }

    [TestMethod]
    public void ARunThatEndsExactlyAtTheWidthIsReadable()
    {
        var sprite = SpriteFile.Read(Sprite(2, 1, 0, 0, 1, 1, 7, 0, 0));

        Assert.IsTrue(sprite.Drawn[1]);
    }

    [TestMethod]
    public void AStreamThatEndsInsideARunIsUnreadable()
    {
        // The run draws 3 pixels, the entry holds 2.
        var entry = Sprite(4, 1, 0, 0, 0, 3, 1, 2);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => SpriteFile.Read(entry));

        StringAssert.Contains(failure.Message, "past the end");
    }

    [TestMethod]
    public void AStreamWithFewerRowsThanTheHeightIsUnreadable()
    {
        var entry = Sprite(2, 3, 0, 0, 0, 0, 0, 0);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => SpriteFile.Read(entry));

        StringAssert.Contains(failure.Message, "3 rows");
    }

    [TestMethod]
    public void AnEntryShorterThanItsHeaderIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => SpriteFile.Read(new byte[9]));
    }

    [TestMethod]
    public void ASpriteWithNoWidthOrHeightHasNothingToPreview()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => SpriteFile.Read(Sprite(0, 1, 0, 0, 0, 0)));
        Assert.ThrowsExactly<InvalidDataException>(() => SpriteFile.Read(Sprite(1, 0, 0, 0)));
    }

    [TestMethod]
    [DataRow((ushort)65535, (ushort)65535)]
    [DataRow((ushort)40000, (ushort)40000)]
    public void AHeightThatTheStreamCannotHoldIsUnreadableWithAReasonBeforeAnythingIsAllocated(
        ushort width,
        ushort height
    )
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            SpriteFile.Read(Sprite(width, height, 0, 0, 0, 0, 0, 0))
        );

        StringAssert.Contains(failure.Message, $"{height} rows");
    }
}
