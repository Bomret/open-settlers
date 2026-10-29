using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SpriteKindTests
{
    static readonly string[] Files = ["loca/SPAD.PA", "loca/SPAE.PA", "loca/SPAF.PA"];

    // Width 2, height 1: one drawn pixel of index 1 on the right, offsets -5 and -6.
    static byte[] Sprite() => GraphicFileTests.Entry(1, 0, 2, 1, -5, -6, [1, 1, 1, 0, 0]);

    static FakeInstall InstallWith(byte[] archive)
    {
        var fake = new FakeInstall();
        foreach (var file in Files)
        {
            fake.Put(file, archive);
        }

        return fake;
    }

    static List<Asset> List(FakeInstall fake)
    {
        Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _);
        var all = AssetKind.Discover().OfType<ArchiveEntryKind>().ToList();

        return new SpriteKind().Enumerate(install!, all).ToList();
    }

    static SpritePreview Preview(int entry, int rows = 4000)
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(rows, new Dictionary<int, byte[]> { [entry] = Sprite(), [3] = Palette() })
        );

        return (SpritePreview)List(fake).First(a => a.File == Files[0] && a.Entry == entry.ToString()).Load();
    }

    // Color 1 is (63, 0, 0) at 6 bits, color 31 (0, 63, 0).
    static byte[] Palette()
    {
        var bytes = new byte[768];
        bytes[3] = 0xFC;
        bytes[31 * 3 + 1] = 0xFC;

        return bytes;
    }

    [TestMethod]
    [DataRow(321, 372)]
    [DataRow(610, 619)]
    [DataRow(750, 793)]
    [DataRow(810, 853)]
    [DataRow(1250, 1273)]
    [DataRow(1314, 1362)]
    [DataRow(1378, 1443)]
    [DataRow(1850, 2390)]
    [DataRow(2500, 2669)]
    [DataRow(2671, 2701)]
    [DataRow(2740, 2965)]
    [DataRow(3000, 3009)]
    [DataRow(3033, 3040)]
    [DataRow(448, 521)]
    [DataRow(630, 645)]
    [DataRow(3150, 3450)]
    [DataRow(3738, 3764)]
    [DataRow(3768, 3779)]
    public void TheSpriteKindClaimsTheEntriesOfAnAnimationOrFontRange(int first, int last)
    {
        var kind = new SpriteKind();
        Assert.IsTrue(Enumerable.Range(first, last - first + 1).All(kind.Claims));
    }

    [TestMethod]
    public void TheSpriteKindClaimsEveryEntryOfTheSixStepRangeOnlyAtItsSources()
    {
        var kind = new SpriteKind();

        Assert.IsTrue(kind.Claims(3456));
        Assert.IsTrue(kind.Claims(3732));
        Assert.IsFalse(kind.Claims(3457));
        Assert.IsFalse(kind.Claims(3461));
    }

    [TestMethod]
    [DataRow(320)]
    [DataRow(1597)]
    [DataRow(1598)]
    [DataRow(1500)]
    [DataRow(2670)]
    [DataRow(3010)]
    [DataRow(3032)]
    [DataRow(3999)]
    [DataRow(3880)]
    [DataRow(3780)]
    [DataRow(260)]
    [DataRow(1363)]
    [DataRow(3451)]
    [DataRow(3765)]
    public void TheSpriteKindClaimsNoEntryNextToItsRangesOrOneThatReadsAsAnEmptyMask(int entry)
    {
        Assert.IsFalse(new SpriteKind().Claims(entry));
    }

    [TestMethod]
    public void TheSpriteKindClaimsNoAliasDestinationAndNoEntryOfAnotherImageKind()
    {
        var kind = new SpriteKind();
        var others = new ArchiveEntryKind[] { new PaletteKind(), new RawImageKind() };
        var destinations = ArchiveFile
            .Read(ArchiveBytes.Build(4000, Enumerable.Range(1, 3999).ToDictionary(n => n, _ => new byte[] { 1 })))
            .Entries.Where(e => e.AliasOf != null);

        Assert.IsFalse(destinations.Any(e => kind.Claims(e.Number)));
        Assert.IsFalse(Enumerable.Range(1, ushort.MaxValue).Any(n => kind.Claims(n) && others.Any(o => o.Claims(n))));
    }

    [TestMethod]
    public void ASpriteIsPreviewedAsItsSizeOffsetsAndRgbaWithTheBackgroundTransparent()
    {
        var preview = Preview(610);

        Assert.AreEqual(2, preview.Width);
        Assert.AreEqual(1, preview.Height);
        Assert.AreEqual(-5, preview.OffsetX);
        Assert.AreEqual(-6, preview.OffsetY);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 252, 0, 0, 255 }, preview.Rgba);
        Assert.IsNull(preview.OneColor);
    }

    [TestMethod]
    public void ASpriteTheProgramDrawsInOneColorIsPreviewedInThatColor()
    {
        var preview = Preview(750);

        Assert.AreEqual(31, preview.OneColor);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0, 252, 0, 255 }, preview.Rgba);
    }

    [TestMethod]
    public void TheShadowFontIsDrawnInColorOne()
    {
        Assert.AreEqual(1, Preview(810).OneColor);
        Assert.AreEqual(1, SpriteKind.OneColorOf(853));
        Assert.IsNull(SpriteKind.OneColorOf(854));
    }

    [TestMethod]
    public void AnAliasOfASpriteIsListedAsTheSpriteItRepeats()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(3800, new Dictionary<int, byte[]> { [3450] = Sprite(), [3] = Palette() })
        );

        var alias = List(fake).First(a => a.File == Files[0] && a.Entry == "3451");

        Assert.IsTrue(alias.IsAlias);
        Assert.AreEqual(2, ((SpritePreview)alias.Load()).Width);
    }

    [TestMethod]
    public void ARowThatOverrunsTheWidthIsUnreadableWithFileEntryAndReasonAndTheNextSpriteStillLoads()
    {
        var overrun = GraphicFileTests.Entry(1, 0, 1, 1, 0, 0, [0, 2, 1, 1, 0, 0]);
        using var fake = InstallWith(
            ArchiveBytes.Build(
                700,
                new Dictionary<int, byte[]>
                {
                    [610] = overrun,
                    [611] = Sprite(),
                    [3] = Palette(),
                }
            )
        );

        var assets = List(fake).Where(a => a.File == Files[0]).ToList();

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => assets.Single(a => a.Entry == "610").Load());
        StringAssert.Contains(failure.Message, "past the width 1");
        Assert.IsInstanceOfType<SpritePreview>(assets.Single(a => a.Entry == "611").Load());
    }

    [TestMethod]
    public void TheCheckReportsAStreamPastTheEndOfTheEntryWithFileEntryAndReasonAndStillReadsTheRest()
    {
        var cut = GraphicFileTests.Entry(1, 0, 4, 1, 0, 0, [0, 3, 1]);
        using var fake = InstallWith(
            ArchiveBytes.Build(
                700,
                new Dictionary<int, byte[]>
                {
                    [610] = cut,
                    [611] = Sprite(),
                    [3] = Palette(),
                }
            )
        );
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, [new PaletteKind(), new SpriteKind()]);

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(
            report.Lines.Any(l => l.StartsWith("FAIL loca/SPAD.PA entry 610: ") && l.Contains("past the end"))
        );
        CollectionAssert.Contains(report.Lines.ToList(), "loca/SPAD.PA: 2 entries (palette, sprite)");
    }

    [TestMethod]
    public void ASpriteWhoseArchiveHasNoPaletteIsUnreadableAndNamesThePalette()
    {
        using var fake = InstallWith(ArchiveBytes.Build(700, new Dictionary<int, byte[]> { [610] = Sprite() }));

        var asset = List(fake).First(a => a.Entry == "610");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
        StringAssert.Contains(failure.Message, "no entry 3");
    }

    [TestMethod]
    public void TheSpriteKindRegistersItselfThroughDiscover()
    {
        CollectionAssert.Contains(AssetKind.Discover().Select(k => k.Name).ToList(), "sprite");
    }
}
