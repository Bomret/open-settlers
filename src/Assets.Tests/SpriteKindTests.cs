using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The test of the sprite kind that runs through the asset check (the library of the asset check owns it); the tests of the
// kind itself are in the History Edition source's test project, in the class of the same name.
[TestClass]
public sealed class SpriteKindTests
{
    static readonly string[] Files = ["loca/SPAD.PA", "loca/SPAE.PA", "loca/SPAF.PA"];

    // Width 2, height 1: one drawn pixel of index 1 on the right, offsets -5 and -6.
    static byte[] Sprite() => GraphicEntries.Entry(1, 0, 2, 1, -5, -6, [1, 1, 1, 0, 0]);

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

    [TestMethod]
    public void TheCheckReportsAStreamPastTheEndOfTheEntryWithFileEntryAndReasonAndStillReadsTheRest()
    {
        var cut = GraphicEntries.Entry(1, 0, 4, 1, 0, 0, [0, 3, 1]);
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

        var report = AssetCheck.Run(args, NamedKinds.Over(args, "palette", "sprite"));

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(
            report.Lines.Any(l => l.StartsWith("FAIL loca/SPAD.PA entry 610: ") && l.Contains("past the end"))
        );
        CollectionAssert.Contains(report.Lines.ToList(), "loca/SPAD.PA: 2 entries (palette, sprite)");
    }
}
