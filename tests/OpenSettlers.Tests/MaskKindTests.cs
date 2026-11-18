using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The tests of the mask kind that run through the asset check (the library of the asset check owns them); the tests of the
// kind itself are in the History Edition source's test project, in the class of the same name.
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
    static byte[] Mask() => GraphicEntries.MaskEntry(2, 1, 1, 1, 0, 0);

    [TestMethod]
    public void TheCheckReportsAnUnreadableMaskWithFileEntryAndReasonAndStillListsTheOthers()
    {
        using var fake = InstallWith(
            ArchiveBytes.Build(
                2000,
                new Dictionary<int, byte[]>
                {
                    [3] = Palette(),
                    [60] = GraphicEntries.MaskEntry(2, 1, 1, 2, 0, 0),
                    [61] = Mask(),
                }
            )
        );
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, RealKinds.Over(args, new MaskKind()));

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL loca/SPAD.PA entry 60: ") && l.Contains("width 2")));
        CollectionAssert.Contains(report.Lines.ToList(), "loca/SPAD.PA: 1 entry (mask)");
    }
}
