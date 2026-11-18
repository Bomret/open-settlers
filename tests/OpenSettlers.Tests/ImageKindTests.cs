using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The tests of the image kinds that run through the asset check (the library of the asset check owns them); the tests of the
// kinds themselves are in the History Edition source's test project, in the class of the same name.
[TestClass]
public sealed class ImageKindTests
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

    // Color 1 is (63, 0, 0) at 6 bits.
    static byte[] Palette()
    {
        var bytes = new byte[768];
        bytes[3] = 0xFC;

        return bytes;
    }

    static byte[] Image(ushort width, ushort height, params byte[] pixels) =>
        GraphicEntries.Entry(1, 0, width, height, 0, 0, pixels);

    [TestMethod]
    public void TheCheckReportsAnUnreadableRawImageWithFileEntryAndReasonAndStillReadsThePalette()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [1] = Image(2, 2, 1), [3] = Palette() })
        );
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, RealKinds.Over(args, new PaletteKind(), new RawImageKind()));

        Assert.AreEqual(1, report.ExitCode);
        CollectionAssert.Contains(report.Lines.ToList(), "loca/SPAD.PA: 1 entry (palette)");
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL loca/SPAD.PA entry 1: ")));
    }
}
