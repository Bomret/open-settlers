using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ImageKindTests
{
    const int Rows = 4000;

    static readonly string[] Files = ["loca/SPAD.PA", "loca/SPAE.PA", "loca/SPAF.PA"];

    static Install Open(FakeInstall fake)
    {
        Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _);

        return install!;
    }

    static FakeInstall InstallWith(byte[] archive)
    {
        var fake = new FakeInstall();
        foreach (var file in Files)
        {
            fake.Put(file, archive);
        }

        return fake;
    }

    // Color 1 is (63, 0, 0) at 6 bits.
    static byte[] Palette()
    {
        var bytes = new byte[768];
        bytes[3] = 0xFC;

        return bytes;
    }

    static byte[] Image(ushort width, ushort height, params byte[] pixels) =>
        GraphicFileTests.Entry(1, 0, width, height, 0, 0, pixels);

    static List<Asset> List<T>(FakeInstall fake)
        where T : ArchiveEntryKind, new()
    {
        var all = AssetKind.Discover().OfType<ArchiveEntryKind>().ToList();

        return new T().Enumerate(Open(fake), all).ToList();
    }

    [TestMethod]
    public void ThePaletteKindClaimsTheEntryTheProgramLoadsAsItsPalette()
    {
        var claimed = Enumerable.Range(1, ushort.MaxValue).Where(new PaletteKind().Claims).ToList();

        CollectionAssert.AreEqual(new[] { 3, 3997, 3998 }, claimed);
    }

    [TestMethod]
    public void ThePaletteKindPreviewsEntry3AsItsColors()
    {
        using var fake = InstallWith(ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [3] = Palette() }));

        var preview = (PalettePreview)List<PaletteKind>(fake).First(a => a.File == Files[0]).Load();

        Assert.AreEqual(new Rgb(63, 0, 0), preview.Palette[1]);
        Assert.AreEqual(new Rgb(0, 0, 0), preview.Palette[0]);
    }

    [TestMethod]
    public void APaletteOfTheWrongSizeIsUnreadableWithItsEntryAndTheOtherArchivesStillLoad()
    {
        var bad = ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [3] = new byte[767] });
        using var fake = InstallWith(ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [3] = Palette() }));
        fake.Put(Files[1], bad);

        var assets = List<PaletteKind>(fake);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => assets.Single(a => a.File == Files[1]).Load());
        StringAssert.Contains(failure.Message, "767");
        Assert.IsInstanceOfType<PalettePreview>(assets.Single(a => a.File == Files[0]).Load());
    }

    [TestMethod]
    public void TheRawImageKindClaimsEntryOne()
    {
        Assert.IsTrue(new RawImageKind().Claims(1));
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(5, 11)]
    [DataRow(15, 21)]
    [DataRow(25, 38)]
    [DataRow(40, 57)]
    [DataRow(600, 603)]
    [DataRow(660, 663)]
    [DataRow(670, 677)]
    [DataRow(870, 1187)]
    [DataRow(1750, 1774)]
    [DataRow(1780, 1796)]
    [DataRow(1800, 1805)]
    [DataRow(3880, 3882)]
    public void TheRawImageKindClaimsEachRangeTheProgramDrawsRaw(int first, int last)
    {
        var kind = new RawImageKind();

        Assert.IsTrue(Enumerable.Range(first, last - first + 1).All(kind.Claims));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(12)]
    [DataRow(39)]
    [DataRow(58)]
    [DataRow(1188)]
    [DataRow(1775)]
    [DataRow(1797)]
    [DataRow(3883)]
    public void TheRawImageKindClaimsNoEntryNextToItsRanges(int entry)
    {
        Assert.IsFalse(new RawImageKind().Claims(entry));
    }

    [TestMethod]
    public void TheRawImageKindClaimsNoAliasDestination()
    {
        var kind = new RawImageKind();
        var destinations = ArchiveFile
            .Read(ArchiveBytes.Build(Rows, Enumerable.Range(1, Rows - 1).ToDictionary(n => n, _ => new byte[] { 1 })))
            .Entries.Where(e => e.AliasOf != null);

        Assert.IsFalse(destinations.Any(e => kind.Claims(e.Number)));
    }

    // Entries 3, 3997 and 3998 are palettes whose color 1 is red, green, and blue.
    static Dictionary<int, byte[]> ThreePalettes(params (int Entry, byte[] Data)[] more)
    {
        byte[] Palette1(byte r, byte g, byte b)
        {
            var bytes = new byte[768];
            bytes[3] = r;
            bytes[4] = g;
            bytes[5] = b;

            return bytes;
        }

        var entries = new Dictionary<int, byte[]>
        {
            [3] = Palette1(0xFC, 0, 0),
            [3997] = Palette1(0, 0xFC, 0),
            [3998] = Palette1(0, 0, 0xFC),
        };
        foreach (var (entry, data) in more)
        {
            entries[entry] = data;
        }

        return entries;
    }

    static RawImagePreview Preview(int entry, int rows = Rows)
    {
        using var fake = InstallWith(ArchiveBytes.Build(rows, ThreePalettes((entry, Image(1, 1, 1)))));

        return (RawImagePreview)List<RawImageKind>(fake).First(a => a.Entry == entry.ToString()).Load();
    }

    [TestMethod]
    public void ARawImageIsPreviewedAsItsSizeAndOpaqueRgbaInTheGamesPalette()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(700, new Dictionary<int, byte[]> { [600] = Image(2, 1, 1, 0), [3] = Palette() })
        );

        var preview = (RawImagePreview)List<RawImageKind>(fake).First(a => a.Entry == "600").Load();

        Assert.AreEqual(2, preview.Width);
        Assert.AreEqual(1, preview.Height);
        CollectionAssert.AreEqual(new byte[] { 252, 0, 0, 255, 0, 0, 0, 255 }, preview.Rgba);
    }

    [TestMethod]
    public void AnImageOfTheGameScreensIsDrawnInEntry3()
    {
        CollectionAssert.AreEqual(new byte[] { 252, 0, 0, 255 }, Preview(600).Rgba);
    }

    [TestMethod]
    public void TheTitleSceneIsDrawnInEntry3997()
    {
        CollectionAssert.AreEqual(new byte[] { 0, 252, 0, 255 }, Preview(1).Rgba);
    }

    [TestMethod]
    public void TheStartScreenIsDrawnInEntry3998()
    {
        CollectionAssert.AreEqual(new byte[] { 0, 0, 252, 255 }, Preview(40).Rgba);
    }

    [TestMethod]
    public void ARawImageWhoseSizeDoesNotMatchItsWidthAndHeightIsUnreadableWithItsEntryAndFile()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [1] = Image(2, 2, 1, 0), [3] = Palette() })
        );

        var asset = List<RawImageKind>(fake).First(a => a.Entry == "1");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
        StringAssert.Contains(failure.Message, "2 x 2");
        Assert.AreEqual(Files[0], asset.File);
    }

    [TestMethod]
    public void ARawImageShorterThanItsHeaderIsUnreadableAndTheNextImageStillLoads()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(
                700,
                new Dictionary<int, byte[]>
                {
                    [1] = new byte[9],
                    [600] = Image(1, 1, 1),
                    [3] = Palette(),
                }
            )
        );

        var assets = List<RawImageKind>(fake).Where(a => a.File == Files[0]).ToList();

        Assert.ThrowsExactly<InvalidDataException>(() => assets.Single(a => a.Entry == "1").Load());
        Assert.IsInstanceOfType<RawImagePreview>(assets.Single(a => a.Entry == "600").Load());
    }

    [TestMethod]
    public void ARawImageOfAnArchiveWithAnUnreadablePaletteIsUnreadableAndNamesThePalette()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(700, new Dictionary<int, byte[]> { [600] = Image(1, 1, 1), [3] = new byte[5] })
        );

        var asset = List<RawImageKind>(fake).First(a => a.Entry == "600");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
        StringAssert.Contains(failure.Message, "palette (entry 3)");
    }

    [TestMethod]
    public void ARawImageOfAnArchiveWithNoPaletteIsUnreadableAndNamesThePalette()
    {
        using var fake = InstallWith(ArchiveBytes.Build(700, new Dictionary<int, byte[]> { [600] = Image(1, 1, 1) }));

        var asset = List<RawImageKind>(fake).First(a => a.Entry == "600");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
        StringAssert.Contains(failure.Message, "no entry 3");
    }

    [TestMethod]
    public void TheCheckReportsAnUnreadableRawImageWithFileEntryAndReasonAndStillReadsThePalette()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [1] = Image(2, 2, 1), [3] = Palette() })
        );
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, [new PaletteKind(), new RawImageKind()]);

        Assert.AreEqual(1, report.ExitCode);
        CollectionAssert.Contains(report.Lines.ToList(), "loca/SPAD.PA: 1 entry (palette)");
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL loca/SPAD.PA entry 1: ")));
    }

    [TestMethod]
    public void BothKindsRegisterThemselvesThroughDiscover()
    {
        var names = AssetKind.Discover().Select(k => k.Name).ToList();

        CollectionAssert.IsSubsetOf(new[] { "palette", "raw image" }, names);
    }
}
