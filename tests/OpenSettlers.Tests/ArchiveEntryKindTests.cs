using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

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

    static Install Open(FakeInstall fake)
    {
        Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _);

        return install!;
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

    static List<Asset> List(ArchiveEntryKind kind, FakeInstall fake, params ArchiveEntryKind[] others) =>
        kind.Enumerate(Open(fake), [kind, .. others]).ToList();

    [TestMethod]
    public void AKindListsItsClaimedPresentEntriesOfEachArchiveInEntryOrder()
    {
        using var fake = InstallWith(Small());

        var assets = List(new ClaimingKind("palette", 2, 3), fake);

        CollectionAssert.AreEqual(
            Files.SelectMany(f => new[] { (f, "2"), (f, "3") }).ToList(),
            assets.Select(a => (a.File, a.Entry!)).ToList()
        );
        Assert.IsTrue(assets.All(a => a.Kind == "palette"));
    }

    [TestMethod]
    public void AnAbsentEntryIsListedByNoKind()
    {
        using var fake = InstallWith(Small());

        var assets = List(new ClaimingKind("all", 1, 7), fake);

        CollectionAssert.DoesNotContain(assets.Select(a => a.Entry).ToList(), "4");
        Assert.AreEqual(4 * 3, assets.Count);
    }

    [TestMethod]
    public void ALoadedEntryHandsTheKindItsDataAndItsSlot()
    {
        using var fake = InstallWith(Small());

        var preview = (ClaimedPreview)List(new ClaimingKind("palette", 3, 3), fake)[0].Load();

        Assert.AreEqual("768 bytes", preview.Summary);
        Assert.AreEqual(3, preview.Entry.Number);
    }

    [TestMethod]
    public void AnAliasIsListedAsTheEntryItRepeatsAndCountsAsAnAlias()
    {
        using var fake = InstallWith(ArchiveBytes.Build(Rows, new Dictionary<int, byte[]> { [1352] = [4, 4] }));

        var assets = List(new ClaimingKind("sprite", 1352, 1352), fake).Where(a => a.File == Files[0]).ToList();

        Assert.AreEqual(7, assets.Count);
        Assert.IsFalse(assets[0].IsAlias);
        Assert.IsTrue(assets.Skip(1).All(a => a.IsAlias));
        CollectionAssert.AreEqual(
            new[] { "1352", "1363", "1364", "1365", "1366", "1367", "1368" },
            assets.Select(a => a.Entry).ToList()
        );
        StringAssert.Contains(assets[1].Name, "1352");
        Assert.AreEqual("2 bytes", assets[1].Load().Summary);
    }

    [TestMethod]
    public void AnEntryPastTheEndFailsOnItsOwnWithTheEntryAndTheOtherEntriesStillLoad()
    {
        var bytes = Small();
        ArchiveBytes.Put(bytes, 2 * 8, 900);
        using var fake = InstallWith(bytes);

        var assets = List(new ClaimingKind("all", 1, 7), fake).Where(a => a.File == Files[0]).ToList();

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => assets.Single(a => a.Entry == "2").Load());
        StringAssert.Contains(failure.Message, "entry 2");
        Assert.AreEqual("1 bytes", assets.Single(a => a.Entry == "1").Load().Summary);
    }

    [TestMethod]
    public void AnEntryClaimedByTwoKindsFailsInBothAndNamesBoth()
    {
        using var fake = InstallWith(Small());
        var first = new ClaimingKind("palette", 1, 3);
        var second = new ClaimingKind("sprite", 3, 4);

        var shared = List(first, fake, second).Single(a => a.File == Files[0] && a.Entry == "3");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => shared.Load());
        StringAssert.Contains(failure.Message, "palette");
        StringAssert.Contains(failure.Message, "sprite");
        StringAssert.Contains(failure.Message, "entry 3");
    }

    [TestMethod]
    public void AnEntryNoKindClaimsIsListedByTheUnrecoveredKindWithItsLocationAndFailsWithTheReason()
    {
        using var fake = InstallWith(Small());
        var palette = new ClaimingKind("palette", 3, 3);
        var unrecovered = new UnrecoveredEntryKind();

        var assets = List(unrecovered, fake, palette).Where(a => a.File == Files[1]).ToList();

        CollectionAssert.AreEqual(new[] { "1", "2", "5" }, assets.Select(a => a.Entry).ToList());
        StringAssert.Contains(assets[1].Name, "size 2");
        StringAssert.Contains(assets[1].Name, "offset 65");
        var failure = Assert.ThrowsExactly<InvalidDataException>(() => assets[1].Load());
        StringAssert.Contains(failure.Message, "entry 2");
        StringAssert.Contains(failure.Message, "no kind");
    }

    [TestMethod]
    public void TheCheckFailsAPresentEntryNoKindClaimsWithFileEntryAndReason()
    {
        // Entry 12 lies in no range of the discovered kinds.
        using var fake = InstallWith(ArchiveBytes.Build(Rows, new Dictionary<int, byte[]> { [12] = [1] }));
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, [new UnrecoveredEntryKind()]);

        Assert.AreEqual(1, report.ExitCode);
        var line = report.Lines.Single(l => l.StartsWith("FAIL loca/SPAF.PA entry 12:"));
        StringAssert.Contains(line, "no kind claims entry 12");
        Assert.AreEqual(3, report.Lines.Count(l => l.StartsWith("FAIL ") && l.Contains("entry 12:")));
    }

    [TestMethod]
    public void AnAliasOfAClaimedEntryIsClaimedWithItAndNotUnrecovered()
    {
        using var fake = InstallWith(ArchiveBytes.Build(Rows, new Dictionary<int, byte[]> { [1352] = [4] }));

        var assets = List(new UnrecoveredEntryKind(), fake, new ClaimingKind("sprite", 1352, 1352));

        Assert.AreEqual(0, assets.Count);
    }

    [TestMethod]
    public void ACorruptTableIsOneFailingAssetOfTheFileInEveryKind()
    {
        using var fake = InstallWith(Small());
        fake.Put(Files[1], Small()[..20]);
        var palette = new ClaimingKind("palette", 1, 7);
        var unrecovered = new UnrecoveredEntryKind();

        foreach (var kind in new ArchiveEntryKind[] { palette, unrecovered })
        {
            var failing = List(kind, fake, palette, unrecovered).Single(a => a.File == Files[1]);
            Assert.IsNull(failing.Entry);
            StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => failing.Load()).Message, "table");
        }
    }

    [TestMethod]
    public void TheCheckReportsAFailingArchiveOnceWhateverNumberOfKindsListIt()
    {
        using var fake = new FakeInstall().Put(Files[0], Small()).Put(Files[1], Small()[..20]);
        var args = LaunchArgs.Parse(["--game", fake.Root], null);

        var report = AssetCheck.Run(args, [new ClaimingKind("palette", 1, 7), new UnrecoveredEntryKind()]);

        Assert.AreEqual(1, report.Lines.Count(l => l.StartsWith($"FAIL {Files[1]}:")));
        Assert.AreEqual(1, report.Lines.Count(l => l.StartsWith($"FAIL {Files[2]}:")));
    }

    [TestMethod]
    public void AMissingArchiveIsOneFailingAssetOfTheFileFromTheUnrecoveredKind()
    {
        using var fake = new FakeInstall().Put(Files[0], Small()).Put(Files[1], Small());

        var failing = List(new UnrecoveredEntryKind(), fake).Single(a => a.File == Files[2]);

        var failure = Assert.ThrowsExactly<FileNotFoundException>(() => failing.Load());
        StringAssert.Contains(failure.Message, Files[2]);
    }

    [TestMethod]
    public void NoEntryOfTheArchiveIsClaimedByMoreThanOneDiscoveredKind()
    {
        var all = AssetKind.Discover().OfType<ArchiveEntryKind>().ToList();

        var doubled = Enumerable
            .Range(1, ushort.MaxValue)
            .Where(entry => ArchiveEntryKind.ClaimedBy(entry, all).Count > 1)
            .Select(entry => $"{entry}: {string.Join(", ", ArchiveEntryKind.ClaimedBy(entry, all))}")
            .ToList();

        Assert.IsEmpty(doubled, string.Join("; ", doubled));
    }

    [TestMethod]
    public void NoDiscoveredKindClaimsAnAliasDestination()
    {
        var all = AssetKind.Discover().OfType<ArchiveEntryKind>().ToList();
        var sources = Enumerable.Range(1, Rows - 1).ToDictionary(n => n, _ => new byte[] { 1 });
        var aliases = ArchiveFile.Read(ArchiveBytes.Build(Rows, sources)).Entries.Where(e => e.AliasOf != null);

        var claimed = aliases.Where(a => all.Any(k => k.Claims(a.Number))).Select(a => a.Number).ToList();

        Assert.IsEmpty(claimed, string.Join(", ", claimed));
    }

    [TestMethod]
    public void AKindThatClaimsACopyDestinationFailsTheAliasAndNamesBothEntries()
    {
        using var fake = InstallWith(ArchiveBytes.Build(Rows, new Dictionary<int, byte[]> { [1352] = [4] }));
        var sprite = new ClaimingKind("sprite", 1352, 1352);
        var stray = new ClaimingKind("stray", 1363, 1363);

        var alias = List(sprite, fake, stray).Single(a => a.File == Files[0] && a.Entry == "1363");

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => alias.Load());
        StringAssert.Contains(failure.Message, "1363");
        StringAssert.Contains(failure.Message, "1352");
        StringAssert.Contains(failure.Message, "stray");
    }

    static bool CanStillRead(string path)
    {
        try
        {
            File.ReadAllBytes(path);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void AnArchiveTheProcessMayNotReadIsOneFailingAssetOfTheFileAndNoException()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("File modes need Unix.");
        }

        using var fake = InstallWith(Small());
        var path = Path.Combine(fake.Root, Files[1]);
        File.SetUnixFileMode(path, UnixFileMode.None);
        if (CanStillRead(path))
        {
            Assert.Inconclusive("The process reads files whatever their mode (root).");
        }

        var failing = List(new UnrecoveredEntryKind(), fake).Single(a => a.File == Files[1]);

        Assert.IsNull(failing.Entry);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => failing.Load());
    }

    [TestMethod]
    public void TheUnrecoveredKindRegistersItselfThroughDiscover()
    {
        Assert.IsTrue(AssetKind.Discover().Any(k => k is UnrecoveredEntryKind));
    }

    [TestMethod]
    public void TheCheckReportsPresentEntriesAndAliasesSeparatelyPerArchive()
    {
        // Entry 1352 is a sprite and its copies are aliases; entry 3 is the palette that the sprite needs; entry 2 is rule data.
        var sprite = GraphicFileTests.Entry(1, 0, 1, 1, 0, 0, [0, 1, 1, 0, 0]);
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
            [new PaletteKind(), new SpriteKind(), new RuleDataKind(), new UnrecoveredEntryKind()]
        );

        Assert.AreEqual(0, report.ExitCode);
        CollectionAssert.Contains(
            report.Lines.ToList(),
            "loca/SPAD.PA: 3 entries, 6 aliases (palette, sprite, rule data)"
        );
    }
}
