using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class GraphicFileTests
{
    /// <summary>A graphic entry: the 10-byte header, then the body.</summary>
    public static byte[] Entry(sbyte first, sbyte second, ushort width, ushort height, short x, short y, byte[] body)
    {
        var bytes = new byte[10 + body.Length];
        bytes[0] = (byte)first;
        bytes[1] = (byte)second;
        BitConverter.TryWriteBytes(bytes.AsSpan(2), width);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), height);
        BitConverter.TryWriteBytes(bytes.AsSpan(6), x);
        BitConverter.TryWriteBytes(bytes.AsSpan(8), y);
        body.CopyTo(bytes, 10);

        return bytes;
    }

    [TestMethod]
    public void TheHeaderHoldsTwoSignedBytesTheSizeAndTheSignedOffsets()
    {
        var graphic = GraphicFile.Read(Entry(-1, -8, 640, 200, -3, -4, [9, 9, 9]));

        Assert.AreEqual(-1, graphic.Header.First);
        Assert.AreEqual(-8, graphic.Header.Second);
        Assert.AreEqual(640, graphic.Header.Width);
        Assert.AreEqual(200, graphic.Header.Height);
        Assert.AreEqual(-3, graphic.Header.OffsetX);
        Assert.AreEqual(-4, graphic.Header.OffsetY);
    }

    [TestMethod]
    public void EverythingAfterTheHeaderIsTheBody()
    {
        var graphic = GraphicFile.Read(Entry(1, 0, 2, 1, 0, 0, [7, 8]));

        CollectionAssert.AreEqual(new byte[] { 7, 8 }, graphic.Body.ToArray());
    }

    [TestMethod]
    public void AnEntryShorterThanItsHeaderIsUnreadableWithItsSize()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() => GraphicFile.Read(new byte[9]));

        StringAssert.Contains(failure.Message, "9");
        StringAssert.Contains(failure.Message, "10");
    }

    [TestMethod]
    public void ARawImageHoldsOneIndexPerPixelRowByRow()
    {
        var image = GraphicFile.ReadRaw(Entry(1, 0, 3, 2, 0, 0, [1, 2, 3, 4, 5, 6]));

        Assert.AreEqual(3, image.Header.Width);
        Assert.AreEqual(2, image.Header.Height);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, image.Pixels.ToArray());
    }

    [TestMethod]
    public void ARawImageWithTooFewPixelsIsUnreadableAndNamesTheExpectedSize()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            GraphicFile.ReadRaw(Entry(1, 0, 3, 2, 0, 0, [1, 2, 3, 4, 5]))
        );

        StringAssert.Contains(failure.Message, "3 x 2");
        StringAssert.Contains(failure.Message, "16");
        StringAssert.Contains(failure.Message, "15");
    }

    [TestMethod]
    public void ARawImageWithTooManyPixelsIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            GraphicFile.ReadRaw(Entry(1, 0, 3, 2, 0, 0, [1, 2, 3, 4, 5, 6, 7]))
        );
    }

    [TestMethod]
    public void TheLargestRawImageHeaderNamesAPositiveExpectedSize()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            GraphicFile.ReadRaw(Entry(1, 0, 65535, 65535, 0, 0, [1]))
        );

        StringAssert.Contains(failure.Message, "4294836235");
    }

    [TestMethod]
    public void ARawImageOfWidthZeroIsUnreadable()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            GraphicFile.ReadRaw(Entry(1, 0, 0, 2, 0, 0, []))
        );

        StringAssert.Contains(failure.Message, "width or height is 0");
    }

    [TestMethod]
    public void ARawImageOfHeightZeroIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => GraphicFile.ReadRaw(Entry(1, 0, 4, 0, 0, 0, [])));
    }

    [TestMethod]
    public void ARawImageShorterThanItsHeaderIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => GraphicFile.ReadRaw(new byte[4]));
    }

    [TestMethod]
    public void TheFirstByteDoesNotChangeHowARawImageReads()
    {
        var tags = new sbyte[] { -1, 0, 1, 2 };

        var pixels = tags.Select(t => GraphicFile.ReadRaw(Entry(t, 0, 1, 1, 0, 0, [5])).Pixels.ToArray()).ToList();

        Assert.IsTrue(pixels.All(p => p.SequenceEqual(new byte[] { 5 })));
    }
}
