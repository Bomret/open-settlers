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

        protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
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
    public void AnEntryNoKindClaimsIsListedByTheUnrecoveredKindWithItsLocationAndSize()
    {
        using var fake = InstallWith(Small());
        var palette = new ClaimingKind("palette", 3, 3);
        var unrecovered = new UnrecoveredEntryKind();

        var assets = List(unrecovered, fake, palette).Where(a => a.File == Files[1]).ToList();

        CollectionAssert.AreEqual(new[] { "1", "2", "5" }, assets.Select(a => a.Entry).ToList());
        StringAssert.Contains(assets[1].Name, "size 2");
        StringAssert.Contains(assets[1].Name, "offset 65");
        StringAssert.Contains(assets[1].Load().Summary, "offset 65");
    }

    [TestMethod]
    public void AnAliasOfAClaimedEntryIsClaimedWithItAndNotUnrecovered()
    {
        using var fake = InstallWith(ArchiveBytes.Build(Rows, new Dictionary<int, byte[]> { [1352] = [4] }));

        var assets = List(new UnrecoveredEntryKind(), fake, new ClaimingKind("sprite", 1352, 1352));

        Assert.AreEqual(0, assets.Count);
    }

    [TestMethod]
    public void ACorruptTableIsOneFailingAssetOfTheFileFromTheUnrecoveredKindAndNoOtherKind()
    {
        using var fake = InstallWith(Small());
        fake.Put(Files[1], Small()[..20]);
        var palette = new ClaimingKind("palette", 1, 7);

        var fromPalette = List(palette, fake).Where(a => a.File == Files[1]).ToList();
        var fromUnrecovered = List(new UnrecoveredEntryKind(), fake, palette).Where(a => a.File == Files[1]).ToList();

        Assert.AreEqual(0, fromPalette.Count);
        var failing = fromUnrecovered.Single();
        Assert.AreEqual(Files[1], failing.File);
        Assert.IsNull(failing.Entry);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => failing.Load()).Message, "table");
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

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void AnArchiveTheProcessMayNotReadIsOneFailingAssetOfTheFileAndNoException()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("File modes need Unix.");
        }

        using var fake = InstallWith(Small());
        File.SetUnixFileMode(Path.Combine(fake.Root, Files[1]), UnixFileMode.None);

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
        using var fake = InstallWith(
            ArchiveBytes.Build(Rows, new Dictionary<int, byte[]> { [1352] = [4], [12] = [1] })
        );
        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, [new UnrecoveredEntryKind()]);

        Assert.AreEqual(0, report.ExitCode);
        CollectionAssert.Contains(report.Lines.ToList(), "loca/SPAD.PA: 2 entries, 6 aliases (unrecovered)");
    }
}
