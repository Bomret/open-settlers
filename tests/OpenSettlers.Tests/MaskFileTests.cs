using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class MaskFileTests
{
    // A mask entry: the header, then each row as (skip, cover) pairs and its end pair (any skip, cover 0).
    public static byte[] Entry(ushort width, ushort height, params byte[] body) =>
        GraphicFileTests.Entry(2, 0, width, height, -1, -2, body);

    static string Rows(Mask mask) =>
        string.Join(
            "/",
            Enumerable
                .Range(0, mask.Height)
                .Select(y =>
                    string.Concat(
                        Enumerable.Range(0, mask.Width).Select(x => mask.Covered[y * mask.Width + x] ? '#' : '.')
                    )
                )
        );

    [TestMethod]
    public void ARowIsRunsOfSkippedAndCoveredPixelsEndedByACoverOfZero()
    {
        var mask = MaskFile.Read(Entry(6, 1, 1, 2, 1, 1, 0, 0));

        Assert.AreEqual(".##.#.", Rows(mask));
    }

    [TestMethod]
    public void TheSkipOfARunCountsFromTheEndOfThePreviousRun()
    {
        var mask = MaskFile.Read(Entry(7, 1, 0, 1, 2, 1, 1, 2, 9, 0));

        Assert.AreEqual("#..#.##", Rows(mask));
    }

    [TestMethod]
    public void TheSkipOfTheEndPairIsIgnored()
    {
        var mask = MaskFile.Read(Entry(3, 1, 0, 1, 200, 0));

        Assert.AreEqual("#..", Rows(mask));
    }

    [TestMethod]
    public void ARowOfOnlyTheEndPairCoversNothing()
    {
        var mask = MaskFile.Read(Entry(2, 2, 2, 0, 0, 2, 0, 0));

        Assert.AreEqual("../##", Rows(mask));
    }

    [TestMethod]
    public void TheHeaderIsReadAndTheSizeIsItsWidthAndHeight()
    {
        var mask = MaskFile.Read(Entry(3, 2, 0, 3, 0, 0, 0, 0));

        Assert.AreEqual(3, mask.Width);
        Assert.AreEqual(2, mask.Height);
        Assert.AreEqual(-1, mask.Header.OffsetX);
        Assert.AreEqual(-2, mask.Header.OffsetY);
        Assert.AreEqual(6, mask.Covered.Length);
    }

    [TestMethod]
    public void ARunThatEndsAtTheWidthIsReadable()
    {
        var mask = MaskFile.Read(Entry(4, 1, 1, 3, 0, 0));

        Assert.AreEqual(".###", Rows(mask));
    }

    [TestMethod]
    public void ARunThatOverrunsTheWidthIsUnreadableWithItsRowAndColumn()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            MaskFile.Read(Entry(4, 2, 0, 4, 0, 0, 2, 3, 0, 0))
        );

        StringAssert.Contains(failure.Message, "row 1");
        StringAssert.Contains(failure.Message, "width 4");
    }

    [TestMethod]
    public void ARunWhoseSkipPushesItPastTheWidthIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => MaskFile.Read(Entry(4, 1, 255, 1, 0, 0)));
    }

    [TestMethod]
    public void ARowWithNoEndPairBeforeTheEntryEndsIsAStreamPastTheEnd()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() => MaskFile.Read(Entry(4, 1, 0, 2)));

        StringAssert.Contains(failure.Message, "row 0");
        StringAssert.Contains(failure.Message, "past the end");
    }

    [TestMethod]
    public void FewerRowsThanTheHeightIsAStreamPastTheEnd()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() => MaskFile.Read(Entry(4, 2, 0, 1, 0, 0)));

        StringAssert.Contains(failure.Message, "row 1");
        StringAssert.Contains(failure.Message, "past the end");
    }

    [TestMethod]
    public void AHalfPairAtTheEndOfTheEntryIsAStreamPastTheEnd()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => MaskFile.Read(Entry(4, 1, 0, 1, 0)));
    }

    [TestMethod]
    public void AnEntryShorterThanItsHeaderIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => MaskFile.Read(new byte[9]));
    }

    [TestMethod]
    public void AWidthOrHeightOfZeroIsUnreadable()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => MaskFile.Read(Entry(0, 1, 0, 0)));
        Assert.ThrowsExactly<InvalidDataException>(() => MaskFile.Read(Entry(1, 0)));
    }

    [TestMethod]
    [DataRow(65535, 65535)]
    [DataRow(40000, 40000)]
    public void AHeightThatNeedsMoreRowsThanTheEntryHoldsIsUnreadableBeforeAnythingIsAllocated(int width, int height)
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            MaskFile.Read(Entry((ushort)width, (ushort)height, 0, 0))
        );

        StringAssert.Contains(failure.Message, $"{width} x {height}");
        StringAssert.Contains(failure.Message, "too few bytes");
    }

    // Every row an end pair, so the entry holds exactly the rows the height needs and only the area bound can refuse it.
    // The bound is the RGBA of the mask: width x height x 4 must fit an array.
    [TestMethod]
    [DataRow(65535, 32769)]
    [DataRow(65535, 8193)]
    public void AMaskWhoseRgbaDoesNotFitAnArrayIsUnreadableBeforeAnythingIsAllocated(int width, int height)
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            MaskFile.Read(Entry((ushort)width, (ushort)height, new byte[height * 2]))
        );

        StringAssert.Contains(failure.Message, $"{width} x {height}");
        StringAssert.Contains(failure.Message, "RGBA");
    }

    [TestMethod]
    public void TheBytesAfterTheLastRowAreIgnored()
    {
        var mask = MaskFile.Read(Entry(2, 1, 0, 2, 0, 0, 7, 7, 7));

        Assert.AreEqual("##", Rows(mask));
    }

    // Color 5 is (63, 0, 0) at 6 bits.
    static Palette Palette()
    {
        var bytes = new byte[768];
        bytes[15] = 0xFC;

        return PaletteFile.Read(bytes);
    }

    [TestMethod]
    public void TheMaskIsDrawnInOneColorWhereItCoversAndWithAlphaZeroWhereItDoesNot()
    {
        var mask = MaskFile.Read(Entry(2, 1, 1, 1, 0, 0));

        CollectionAssert.AreEqual(new byte[] { 252, 0, 0, 0, 252, 0, 0, 255 }, mask.ToRgba(Palette(), 5));
    }
}
