using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The tests of the archive entry kinds that run through the asset check (the library of the asset check owns them); the tests
// of the kinds themselves are in the History Edition source's test project, in the class of the same name.
[TestClass]
public sealed class ArchiveEntryKindTests
{
    const int Rows = 3768;

    static readonly string[] Files = ["loca/SPAD.PA", "loca/SPAE.PA", "loca/SPAF.PA"];

    // A kind that claims the entries first to last. It has no parameterless constructor, so Discover skips it.
    sealed class ClaimingKind(string name, int first, int last) : ArchiveEntryKind
    {
        public override string Name => name;

        public override bool Claims(int entry) => entry >= first && entry <= last;

        protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
            new ClaimedPreview { Summary = $"{data.Length} bytes", Entry = entry };
    }

    sealed record ClaimedPreview : Preview
    {
        public required ArchiveEntry Entry { get; init; }
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

    static byte[] Small() =>
        ArchiveBytes.Build(
            8,
            new Dictionary<int, byte[]>
            {
                [1] = [1],
                [2] = [2, 2],
                [3] = new byte[768],
                [5] = [5],
            }
        );

    [TestMethod]
    public void TheCheckFailsAPresentEntryNoKindClaimsWithFileEntryAndReason()
    {
        // Entry 12 lies in no range of the discovered kinds.
        using var fake = InstallWith(ArchiveBytes.Build(Rows, new Dictionary<int, byte[]> { [12] = [1] }));
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, RealKinds.Over(args, new UnrecoveredEntryKind()));

        Assert.AreEqual(1, report.ExitCode);
        var line = report.Lines.Single(l => l.StartsWith("FAIL loca/SPAF.PA entry 12:"));
        StringAssert.Contains(line, "no kind claims entry 12");
        Assert.AreEqual(3, report.Lines.Count(l => l.StartsWith("FAIL ") && l.Contains("entry 12:")));
    }

    [TestMethod]
    public void TheCheckReportsAFailingArchiveOnceWhateverNumberOfKindsListIt()
    {
        using var fake = new FakeInstall().Put(Files[0], Small()).Put(Files[1], Small()[..20]);
        var args = LaunchArgs.Parse(["--game", fake.Root], null);

        var report = AssetCheck.Run(
            args,
            RealKinds.Over(args, new ClaimingKind("palette", 1, 7), new UnrecoveredEntryKind())
        );

        Assert.AreEqual(1, report.Lines.Count(l => l.StartsWith($"FAIL {Files[1]}:")));
        Assert.AreEqual(1, report.Lines.Count(l => l.StartsWith($"FAIL {Files[2]}:")));
    }

    [TestMethod]
    public void TheCheckReportsPresentEntriesAndAliasesSeparatelyPerArchive()
    {
        // Entry 1352 is a sprite and its copies are aliases; entry 3 is the palette that the sprite needs; entry 2 is rule data.
        var sprite = GraphicEntries.Entry(1, 0, 1, 1, 0, 0, [0, 1, 1, 0, 0]);
        using var fake = InstallWith(
            ArchiveBytes.Build(
                Rows,
                new Dictionary<int, byte[]>
                {
                    [1352] = sprite,
                    [3] = new byte[768],
                    [2] = [1],
                }
            )
        );
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(
            args,
            RealKinds.Over(args, new PaletteKind(), new SpriteKind(), new RuleDataKind(), new UnrecoveredEntryKind())
        );

        Assert.AreEqual(0, report.ExitCode);
        CollectionAssert.Contains(
            report.Lines.ToList(),
            "loca/SPAD.PA: 3 entries, 6 aliases (palette, sprite, rule data)"
        );
    }
}
