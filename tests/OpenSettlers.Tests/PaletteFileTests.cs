using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PaletteFileTests
{
    static byte[] Triples(byte red, byte green, byte blue)
    {
        var bytes = new byte[768];
        for (var color = 0; color < 256; color++)
        {
            bytes[color * 3] = red;
            bytes[color * 3 + 1] = green;
            bytes[color * 3 + 2] = blue;
        }

        return bytes;
    }

    [TestMethod]
    public void EachComponentIsReducedToSixBits()
    {
        var palette = PaletteFile.Read(Triples(0xFF, 0x80, 0x07));

        Assert.AreEqual(new Rgb(63, 32, 1), palette[1]);
    }

    [TestMethod]
    public void ColorZeroIsBlackWhateverTheFileHolds()
    {
        var palette = PaletteFile.Read(Triples(0xFF, 0xFF, 0xFF));

        Assert.AreEqual(new Rgb(0, 0, 0), palette[0]);
    }

    [TestMethod]
    public void ColorOneKeepsItsOwnComponentsNextToTheBlackColorZero()
    {
        var palette = PaletteFile.Read(Triples(0xFC, 0x04, 0x08));

        Assert.AreEqual(new Rgb(63, 1, 2), palette[1]);
    }

    [TestMethod]
    public void ThePaletteHolds256Colors()
    {
        Assert.AreEqual(256, PaletteFile.Read(Triples(0, 0, 0)).Colors.Count);
    }

    [TestMethod]
    public void TheGameShowsEachSixBitComponentTimesFour()
    {
        var palette = PaletteFile.Read(Triples(0xFF, 0x80, 0x07));

        CollectionAssert.AreEqual(new byte[] { 252, 128, 4, 255 }, palette.Rgba(1).ToArray());
    }

    // Source: graphics-images.md, "The palette": 256 colors, indexes 0 to 255. An index outside it throws
    // ArgumentOutOfRangeException: the engine's chosen API contract (no rules document states an outcome).
    [TestMethod]
    public void TheLastColorIsIndex255()
    {
        var palette = PaletteFile.Read(Triples(0xFF, 0x80, 0x07));

        Assert.AreEqual(new Rgb(63, 32, 1), palette[255]);
        CollectionAssert.AreEqual(new byte[] { 252, 128, 4, 255 }, palette.Rgba(255).ToArray());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(256)]
    public void AnIndexOutsideZeroTo255IsOutOfRange(int index)
    {
        var palette = PaletteFile.Read(Triples(0, 0, 0));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => palette[index]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
        {
            _ = palette.Rgba(index);
        });
    }

    [TestMethod]
    public void APaletteShorterThan768BytesIsUnreadableWithItsSize()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() => PaletteFile.Read(new byte[767]));

        StringAssert.Contains(failure.Message, "767");
        StringAssert.Contains(failure.Message, "768");
    }

    [TestMethod]
    public void APaletteLongerThan768BytesIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => PaletteFile.Read(new byte[769]));
    }
}
