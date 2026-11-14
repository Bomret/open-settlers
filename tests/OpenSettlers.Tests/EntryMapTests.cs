using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>The entries the other kinds leave: rule data, the sprites drawn over color 8, and the entries no routine reads.</summary>
[TestClass]
public sealed class EntryMapTests
{
    const int Rows = 4000;

    static readonly string[] Files = ["loca/SPAD.PA", "loca/SPAE.PA", "loca/SPAF.PA"];

    static FakeInstall InstallWith(Dictionary<int, byte[]> payloads)
    {
        var archive = ArchiveBytes.Build(Rows, payloads);
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

        return [.. new T().Enumerate(install!, all)];
    }

    static IEnumerable<int> Claimed<T>()
        where T : ArchiveEntryKind, new()
    {
        var kind = new T();

        return Enumerable.Range(1, ushort.MaxValue).Where(kind.Claims);
    }

    [TestMethod]
    public void TheRuleDataKindClaimsEntryTwoOnly()
    {
        CollectionAssert.AreEqual(new[] { 2 }, Claimed<RuleDataKind>().ToList());
    }

    [TestMethod]
    public void RuleDataIsListedWithItsLocationAndMeaningPendingAndHasNoBytePreview()
    {
        using var fake = InstallWith(new Dictionary<int, byte[]> { [2] = new byte[12] });

        var asset = List<RuleDataKind>(fake).First(a => a.File == Files[1]);

        Assert.AreEqual("rule data", asset.Kind);
        StringAssert.Contains(asset.Name, "entry 2");
        StringAssert.Contains(asset.Name, "size 12");
        StringAssert.Contains(asset.Name, "offset 32000");
        StringAssert.Contains(asset.Name, "meaning pending");
        var preview = asset.Load();
        Assert.IsInstanceOfType<LocatedEntryPreview>(preview);
        StringAssert.Contains(preview.Summary, "meaning pending");
        StringAssert.Contains(preview.Summary, "size 12");
    }

    [TestMethod]
    public void TheUnreadKindClaimsTheEntriesNoRoutineReads()
    {
        CollectionAssert.AreEqual(
            new[] { 494 }.Concat(Enumerable.Range(3900, 95)).Concat([3999]).ToList(),
            Claimed<UnreadEntryKind>().ToList()
        );
    }

    [TestMethod]
    public void AnUnreadEntryIsListedWithItsLocationAndNotReadByTheProgramAndHasNoBytePreview()
    {
        using var fake = InstallWith(new Dictionary<int, byte[]> { [3990] = new byte[5], [3999] = [1, 2] });

        var assets = List<UnreadEntryKind>(fake).Where(a => a.File == Files[2]).ToList();

        CollectionAssert.AreEqual(new[] { "3990", "3999" }, assets.Select(a => a.Entry).ToList());
        StringAssert.Contains(assets[0].Name, "offset 32000");
        StringAssert.Contains(assets[0].Name, "size 5");
        StringAssert.Contains(assets[0].Name, "not read by the program");
        var preview = assets[1].Load();
        Assert.IsInstanceOfType<LocatedEntryPreview>(preview);
        StringAssert.Contains(preview.Summary, "not read by the program");
        StringAssert.Contains(preview.Summary, "size 2");
    }

    [TestMethod]
    public void TheOverColor8SpriteKindClaimsTheSixteenEntriesFrom630()
    {
        CollectionAssert.AreEqual(Enumerable.Range(630, 16).ToList(), Claimed<SpriteOverColor8Kind>().ToList());
    }

    [TestMethod]
    public void ASpriteOverColor8IsDecodedAsASpriteInTheGamePalette()
    {
        var palette = new byte[768];
        palette[2 * 3] = 0xFC;
        // Row 1: skip 1, one pixel of color 2; row 2: nothing drawn.
        var sprite = GraphicFileTests.Entry(1, 0, 2, 2, 0, 0, [1, 1, 2, 0, 0, 0, 0, 0]);
        using var fake = InstallWith(new Dictionary<int, byte[]> { [3] = palette, [630] = sprite });

        var preview = (SpritePreview)List<SpriteOverColor8Kind>(fake).First(a => a.File == Files[0]).Load();

        Assert.AreEqual(2, preview.Width);
        CollectionAssert.AreEqual(new byte[] { 0xFC, 0, 0, 255 }, preview.Rgba.Skip(4).Take(4).ToArray());
        Assert.AreEqual(0, preview.Rgba[3]);
        StringAssert.Contains(preview.Summary, "color 8");
    }

    [TestMethod]
    public void ACorruptSpriteOverColor8FailsWithItsReason()
    {
        using var fake = InstallWith(new Dictionary<int, byte[]> { [3] = new byte[768], [630] = [1, 2, 3] });

        var asset = List<SpriteOverColor8Kind>(fake).First(a => a.File == Files[0]);

        Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
    }

    [TestMethod]
    [DataRow(2, "rule data")]
    [DataRow(448, "sprite")]
    [DataRow(472, "sprite")]
    [DataRow(482, "sprite")]
    [DataRow(490, "sprite")]
    [DataRow(493, "sprite")]
    [DataRow(494, "unread")]
    [DataRow(495, "sprite")]
    [DataRow(502, "sprite")]
    [DataRow(517, "sprite")]
    [DataRow(521, "sprite")]
    [DataRow(630, "sprite over color 8")]
    [DataRow(645, "sprite over color 8")]
    [DataRow(3901, "unread")]
    [DataRow(3994, "unread")]
    [DataRow(3999, "unread")]
    public void EachRemainingEntryHasExactlyOneKind(int entry, string kind)
    {
        var all = AssetKind.Discover().OfType<ArchiveEntryKind>().ToList();

        CollectionAssert.AreEqual(new[] { kind }, ArchiveEntryKind.ClaimedBy(entry, all).ToArray());
    }
}
