using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class IndexedImageTests
{
    // Color 1 is (63, 32, 1) at 6 bits, color 2 is (0, 63, 0), color 0 is black.
    static Palette Palette()
    {
        var bytes = new byte[768];
        bytes[3] = 0xFC;
        bytes[4] = 0x80;
        bytes[5] = 0x04;
        bytes[7] = 0xFC;

        return PaletteFile.Read(bytes);
    }

    [TestMethod]
    public void EachIndexBecomesItsPaletteColorAsFourBytes()
    {
        var rgba = IndexedImage.ToRgba([1, 2], Palette());

        CollectionAssert.AreEqual(new byte[] { 252, 128, 4, 255, 0, 252, 0, 255 }, rgba);
    }

    [TestMethod]
    public void IndexZeroIsOpaqueBlack()
    {
        var rgba = IndexedImage.ToRgba([0], Palette());

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 255 }, rgba);
    }

    [TestMethod]
    public void TheResultHoldsFourBytesPerPixelInPixelOrder()
    {
        var rgba = IndexedImage.ToRgba([2, 1, 2], Palette());

        Assert.AreEqual(12, rgba.Length);
        Assert.AreEqual(252, rgba[1]);
        Assert.AreEqual(252, rgba[4]);
        Assert.AreEqual(252, rgba[9]);
    }

    [TestMethod]
    public void AnEmptyImageConvertsToNoBytes()
    {
        Assert.AreEqual(0, IndexedImage.ToRgba([], Palette()).Length);
    }

    // The size is tested through RgbaLength: an input of this many pixels would need gigabytes to build.
    [TestMethod]
    public void TheRgbaOfAnInputIsFourBytesPerIndex()
    {
        Assert.AreEqual(12, IndexedImage.RgbaLength(3));
    }

    [TestMethod]
    public void AnInputWhoseRgbaDoesNotFitAnArrayIsRefusedInsteadOfWrapping()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IndexedImage.RgbaLength(int.MaxValue / 2));
    }

    [TestMethod]
    public void ARawImageConvertsThroughItsPixels()
    {
        var image = GraphicFile.ReadRaw(GraphicFileTests.Entry(1, 0, 2, 1, 0, 0, [1, 2]));

        var rgba = IndexedImage.ToRgba(image.Pixels.Span, Palette());

        Assert.AreEqual(8, rgba.Length);
    }
}
