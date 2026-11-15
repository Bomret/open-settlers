using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>The History Edition assets port over an install in a temporary folder.</summary>
[TestClass]
public sealed class HistoryEditionAssetsTests
{
    static LaunchArgs Check(FakeInstall fake) => LaunchArgs.Parse(["--check", "--game", fake.Root], null);

    static CheckReport Run(FakeInstall fake)
    {
        var args = Check(fake);

        return AssetCheck.Run(args, LaunchInstall.Assets(args));
    }

    [TestMethod]
    public void TheKindsHaveUniqueNamesAndStampTheirAssetsWithTheirOwnName()
    {
        // One minimal file per kind: every entry of the three archives is one byte, so each kind's normal listing path
        // runs and not only its missing-file path.
        var archive = ArchiveBytes.Build(4000, Enumerable.Range(1, 3999).ToDictionary(n => n, _ => new byte[] { 0 }));
        byte[] midi =
        [
            .. "MThd"u8.ToArray(),
            0,
            0,
            0,
            6,
            0,
            0,
            0,
            1,
            0,
            96,
            .. "MTrk"u8.ToArray(),
            0,
            0,
            0,
            4,
            0,
            0xFF,
            0x2F,
            0,
        ];
        using var fake = new FakeInstall()
            .Put("loca/SPAD.PA", archive)
            .Put("loca/SPAE.PA", archive)
            .Put("loca/SPAF.PA", archive)
            .Put("snd/01.SND", [1])
            .Put("snd/music_1.midi", midi)
            .Put("loca/eng.txt", "A=\"x\"\r\n"u8.ToArray())
            .Put("loca/ger.txt", "A=\"y\"\r\n"u8.ToArray())
            .Put("loca/fre.txt", "A=\"z\"\r\n"u8.ToArray());

        var kinds = LaunchInstall.Assets(Check(fake)).Kinds();

        Assert.IsNull(AssetKinds.NameClash(kinds));
        foreach (var kind in kinds)
        {
            var assets = kind.Enumerate().ToList();

            Assert.IsNotEmpty(assets, kind.Name);
            Assert.IsTrue(assets.All(a => a.Kind == kind.Name), kind.Name);
        }
    }

    [TestMethod]
    public void TheKindsAreListedInNameOrder()
    {
        using var fake = new FakeInstall();

        var names = LaunchInstall.Assets(Check(fake)).Kinds().Select(k => k.Name).ToList();

        CollectionAssert.AreEqual(names.Order(System.StringComparer.Ordinal).ToList(), names);
    }

    [TestMethod]
    public void EveryPresentSoundFileIsReportedWithItsEntryCountAndKind()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", [1, 2]).Put("snd/03.SND", [1]);

        var report = Run(fake);

        CollectionAssert.Contains(report.Lines.ToList(), "snd/01.SND: 1 entry (sound)");
        CollectionAssert.Contains(report.Lines.ToList(), "snd/03.SND: 1 entry (sound)");
    }

    [TestMethod]
    public void AnEmptySoundFileIsReportedWithItsFileAndTheOtherFilesAreStillRead()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", []).Put("snd/02.SND", [1]);

        var report = Run(fake);

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL snd/01.SND: ") && l.Contains("empty")));
        CollectionAssert.Contains(report.Lines.ToList(), "snd/02.SND: 1 entry (sound)");
    }

    [TestMethod]
    public void AMissingListedFileIsReportedWithItsFileAndTheOtherKindsAreStillRead()
    {
        using var fake = new FakeInstall().Put("loca/eng.txt", "A=\"x\"\r\n"u8.ToArray());

        var report = Run(fake);

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL snd: no sound file")));
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL loca/SPAD.PA: the archive loca/SPAD.PA is missing")));
        CollectionAssert.Contains(report.Lines.ToList(), "loca/eng.txt: 1 entry (text)");
    }

    [TestMethod]
    public void FilesTheProgramDoesNotReadAppearNowhereInTheReport()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", [1]).Put("snd/100.SND", [1]).Put("loca/OTHER.DAT", [1]);

        var report = Run(fake);

        CollectionAssert.Contains(report.Lines.ToList(), "snd/01.SND: 1 entry (sound)");
        Assert.IsFalse(report.Lines.Any(l => l.Contains("100.SND") || l.Contains("OTHER.DAT")));
    }

    [TestMethod]
    public void WithoutAnInstallTheExitCodeIsTwoAndTheReportNamesBothWaysToGiveOne()
    {
        var args = LaunchArgs.Parse(["--check"], null);

        var report = AssetCheck.Run(args, LaunchInstall.Assets(args));

        Assert.AreEqual(2, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.Contains("--game") && l.Contains("SIEDLER_DIR")));
    }

    [TestMethod]
    public void AFolderWithoutTheProgramExitsTwoAndTheBrowserShowsTheSameText()
    {
        using var fake = new FakeInstall(withProgram: false).Put("snd/01.SND", []);

        var report = Run(fake);
        var state = AssetBrowserState.Open(LaunchInstall.Assets(Check(fake)));

        Assert.AreEqual(2, report.ExitCode);
        Assert.IsFalse(report.Lines.Any(l => l.StartsWith("FAIL")));
        Assert.AreEqual(report.Lines.Single(), $"ERROR {state.Message}");
        StringAssert.Contains(state.Message, "Siedler1.exe");
    }

    [TestMethod]
    public void AMissingFolderExitsTwoEvenWhenAnEntryWouldFail()
    {
        var args = LaunchArgs.Parse(["--check", "--game", "/no/such/folder"], null);

        var report = AssetCheck.Run(args, LaunchInstall.Assets(args));

        Assert.AreEqual(2, report.ExitCode);
    }

    [TestMethod]
    public void ABadLaunchArgumentExitsFourBeforeTheInstallIsOpened()
    {
        var args = LaunchArgs.Parse(["--check", "--game"], "/no/such/folder");

        var report = AssetCheck.Run(args, LaunchInstall.Assets(args));

        Assert.AreEqual(4, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.Contains("--game needs a value")));
    }

    [TestMethod]
    public void AnArchiveKindShowsAMissingArchiveAsTheFailureOfItsSelectedAsset()
    {
        using var fake = new FakeInstall();

        var state = AssetBrowserState.Open(LaunchInstall.Assets(Check(fake)));

        StringAssert.Contains(state.Failure, "loca/SPAD.PA");
    }

    [TestMethod]
    public void TheBrowserListsASoundKindsFilesInNumberOrderAndShowsAFailedFileAsItsReason()
    {
        using var fake = new FakeInstall().Put("snd/10.SND", [1]).Put("snd/02.SND", []);
        var state = AssetBrowserState.Open(LaunchInstall.Assets(Check(fake)), selectFirst: false);

        state.SelectKind("sound");

        CollectionAssert.AreEqual(new[] { "02.SND", "10.SND" }, state.Assets.Select(a => a.Name).ToList());
        StringAssert.Contains(state.Failure, "empty");
        state.SelectAsset(1);
        Assert.IsInstanceOfType<SoundPreview>(state.Preview);
    }
}
