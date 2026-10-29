using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class MaskKindTests
{
    static readonly string[] Files = ["loca/SPAD.PA", "loca/SPAE.PA", "loca/SPAF.PA"];

    static FakeInstall InstallWith(byte[] archive)
    {
        var fake = new FakeInstall();
        foreach (var file in Files)
        {
            fake.Put(file, archive);
        }

        return fake;
    }

    static List<Asset> List<T>(FakeInstall fake)
        where T : ArchiveEntryKind, new()
    {
        Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _);
        var all = AssetKind.Discover().OfType<ArchiveEntryKind>().ToList();

        return new T().Enumerate(install!, all).ToList();
    }

    // Colors 0x40 and 0x80 are red and green, color 1 is blue.
    static byte[] Palette()
    {
        var bytes = new byte[768];
        bytes[0x40 * 3] = 0xFC;
        bytes[0x80 * 3 + 1] = 0xFC;
        bytes[3 + 2] = 0xFC;

        return bytes;
    }

    // A 2 x 1 mask that covers its second pixel.
    static byte[] Mask() => MaskFileTests.Entry(2, 1, 1, 1, 0, 0);

    static MaskKind Kind => new();

    [TestMethod]
    public void TheMaskKindClaimsTheEntriesOfTheShadowsAndOfTheTexturedFill()
    {
        var claimed = Enumerable.Range(1, ushort.MaxValue).Where(Kind.Claims).ToList();

        CollectionAssert.AreEqual(
            new[] { 4 }
                .Concat(Enumerable.Range(60, 162))
                .Concat(Enumerable.Range(230, 27))
                .Concat(Enumerable.Range(1500, 113))
                .Concat([1619])
                .Concat(Enumerable.Range(1628, 18))
                .Concat(Enumerable.Range(1652, 20))
                .Concat(Enumerable.Range(1674, 20))
                .ToList(),
            claimed
        );
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(59)]
    [DataRow(222)]
    [DataRow(229)]
    [DataRow(257)]
    [DataRow(260)]
    [DataRow(1499)]
    [DataRow(1620)]
    [DataRow(1627)]
    [DataRow(1646)]
    [DataRow(1651)]
    [DataRow(1672)]
    [DataRow(1673)]
    [DataRow(1694)]
    [DataRow(1750)]
    public void TheMaskKindClaimsNoEntryNextToItsRanges(int entry)
    {
        Assert.IsFalse(Kind.Claims(entry));
    }

    [TestMethod]
    public void TheMaskKindClaimsNoAliasDestination()
    {
        var destinations = ArchiveFile
            .Read(ArchiveBytes.Build(4000, Enumerable.Range(1, 3999).ToDictionary(n => n, _ => new byte[] { 1 })))
            .Entries.Where(e => e.AliasOf != null);

        Assert.IsFalse(destinations.Any(e => Kind.Claims(e.Number)));
    }

    [TestMethod]
    public void TheMaskKindClaimsNoEntryOfTheRawImagesOrThePalettes()
    {
        var others = new ArchiveEntryKind[] { new RawImageKind(), new PaletteKind(), new FillTextureKind() };

        Assert.IsFalse(Enumerable.Range(1, ushort.MaxValue).Any(n => Kind.Claims(n) && others.Any(k => k.Claims(n))));
    }

    [TestMethod]
    [DataRow(4, 0, 252, 0)]
    [DataRow(1500, 0, 252, 0)]
    [DataRow(1645, 0, 252, 0)]
    [DataRow(1693, 0, 252, 0)]
    [DataRow(60, 252, 0, 0)]
    [DataRow(221, 252, 0, 0)]
    [DataRow(230, 252, 0, 0)]
    [DataRow(256, 252, 0, 0)]
    public void EachRangeIsDrawnInTheColorTheProgramStoresForIt(int entry, int red, int green, int blue)
    {
        var covered = Preview(entry).Rgba.Skip(4).Take(4);

        CollectionAssert.AreEqual(new byte[] { (byte)red, (byte)green, (byte)blue, 255 }, covered.ToArray());
    }

    static RawImagePreview Preview(int entry)
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(2000, new Dictionary<int, byte[]> { [3] = Palette(), [entry] = Mask() })
        );

        return (RawImagePreview)
            List<MaskKind>(fake).First(a => a.File == Files[0] && a.Entry == entry.ToString()).Load();
    }

    [TestMethod]
    public void AShadowIsDrawnAloneInPaletteColor0x80WithItsSizeAndTransparentElsewhere()
    {
        var preview = Preview(1500);

        Assert.AreEqual(2, preview.Width);
        Assert.AreEqual(1, preview.Height);
        CollectionAssert.AreEqual(new byte[] { 0, 252, 0, 0, 0, 252, 0, 255 }, preview.Rgba);
    }

    [TestMethod]
    public void TheShadowOfEntry4IsDrawnInPaletteColor0x80()
    {
        CollectionAssert.AreEqual(new byte[] { 0, 252, 0, 0, 0, 252, 0, 255 }, Preview(4).Rgba);
    }

    [TestMethod]
    public void ATexturedFillMaskIsDrawnAloneInTheFirstPlayersColor()
    {
        CollectionAssert.AreEqual(new byte[] { 252, 0, 0, 0, 252, 0, 0, 255 }, Preview(60).Rgba);
    }

    [TestMethod]
    public void TheSummaryNamesTheEntryTheKindOfMaskAndTheColor()
    {
        StringAssert.Contains(Preview(1500).Summary, "shadow");
        StringAssert.Contains(Preview(1500).Summary, "0x80");
        StringAssert.Contains(Preview(60).Summary, "textured fill");
        StringAssert.Contains(Preview(60).Summary, "0x40");
    }

    [TestMethod]
    public void AnAliasOfAShadowIsListedWithItsSourceAndDrawnAsItsSource()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(2000, new Dictionary<int, byte[]> { [3] = Palette(), [1602] = Mask() })
        );

        var alias = List<MaskKind>(fake).First(a => a.File == Files[0] && a.Entry == "1613");

        Assert.IsTrue(alias.IsAlias);
        Assert.IsInstanceOfType<RawImagePreview>(alias.Load());
    }

    [TestMethod]
    public void AMaskWhoseRunOverrunsTheWidthIsUnreadableWithItsFileAndEntryAndTheNextMaskStillLoads()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(
                2000,
                new Dictionary<int, byte[]>
                {
                    [3] = Palette(),
                    [60] = MaskFileTests.Entry(2, 1, 1, 2, 0, 0),
                    [61] = Mask(),
                }
            )
        );

        var assets = List<MaskKind>(fake).Where(a => a.File == Files[1]).ToList();

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => assets.Single(a => a.Entry == "60").Load());
        StringAssert.Contains(failure.Message, "width 2");
        Assert.AreEqual(Files[1], assets.Single(a => a.Entry == "60").File);
        Assert.IsInstanceOfType<RawImagePreview>(assets.Single(a => a.Entry == "61").Load());
    }

    [TestMethod]
    public void AMaskWhoseStreamEndsPastTheEntryIsUnreadable()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(
                2000,
                new Dictionary<int, byte[]> { [3] = Palette(), [1500] = MaskFileTests.Entry(2, 2, 0, 2, 0, 0) }
            )
        );

        var asset = List<MaskKind>(fake).First(a => a.Entry == "1500");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
        StringAssert.Contains(failure.Message, "past the end");
    }

    [TestMethod]
    public void AMaskOfAnArchiveWithNoPaletteIsUnreadableAndNamesThePalette()
    {
        using var fake = InstallWith(ArchiveBytes.Build(2000, new Dictionary<int, byte[]> { [1500] = Mask() }));

        var asset = List<MaskKind>(fake).First(a => a.Entry == "1500");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
        StringAssert.Contains(failure.Message, "no entry 3");
    }

    [TestMethod]
    public void TheCheckReportsAnUnreadableMaskWithFileEntryAndReasonAndStillListsTheOthers()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(
                2000,
                new Dictionary<int, byte[]>
                {
                    [3] = Palette(),
                    [60] = MaskFileTests.Entry(2, 1, 1, 2, 0, 0),
                    [61] = Mask(),
                }
            )
        );
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, [new MaskKind()]);

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL loca/SPAD.PA entry 60: ") && l.Contains("width 2")));
        CollectionAssert.Contains(report.Lines.ToList(), "loca/SPAD.PA: 1 entry (mask)");
    }

    [TestMethod]
    public void TheMaskKindRegistersItselfThroughDiscover()
    {
        CollectionAssert.Contains(AssetKind.Discover().Select(k => k.Name).ToList(), "mask");
    }

    [TestMethod]
    public void TheFillTextureKindClaimsTheTexturesTheFillReads()
    {
        var claimed = Enumerable.Range(1, ushort.MaxValue).Where(new FillTextureKind().Claims).ToList();

        CollectionAssert.AreEqual(Enumerable.Range(260, 33).Concat(Enumerable.Range(300, 10)).ToList(), claimed);
    }

    [TestMethod]
    public void AFillTextureIsPreviewedOpaqueInTheGamesPalette()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(
                400,
                new Dictionary<int, byte[]>
                {
                    [3] = Palette(),
                    [260] = GraphicFileTests.Entry(1, 0, 2, 1, 0, 0, [0x40, 1]),
                }
            )
        );

        var preview = (RawImagePreview)List<FillTextureKind>(fake).First(a => a.Entry == "260").Load();

        Assert.AreEqual(2, preview.Width);
        CollectionAssert.AreEqual(new byte[] { 252, 0, 0, 255, 0, 0, 252, 255 }, preview.Rgba);
    }

    [TestMethod]
    public void AFillTextureWhoseSizeDoesNotMatchItsWidthAndHeightIsUnreadable()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(
                400,
                new Dictionary<int, byte[]> { [3] = Palette(), [300] = GraphicFileTests.Entry(1, 0, 2, 2, 0, 0, [1]) }
            )
        );

        var failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            List<FillTextureKind>(fake).First(a => a.Entry == "300").Load()
        );
        StringAssert.Contains(failure.Message, "2 x 2");
    }
}
