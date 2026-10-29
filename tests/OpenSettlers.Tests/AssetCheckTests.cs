using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class AssetCheckTests
{
    sealed record TextPreview : Preview;

    // A kind that lists whatever the test hands it, without reading the install.
    sealed class ListedKind(string name, params Asset[] assets) : AssetKind
    {
        public override string Name => name;

        public override IEnumerable<Asset> Enumerate(Install install) => assets;
    }

    static Asset Entry(string kind, string file, string? entry, Func<Preview>? load = null) =>
        new()
        {
            Kind = kind,
            File = file,
            Entry = entry,
            Name = entry ?? file,
            Load = load ?? (() => new TextPreview { Summary = "ok" }),
        };

    static CheckReport Run(FakeInstall fake, params AssetKind[] kinds) =>
        AssetCheck.Run(LaunchArgs.Parse(["--check", "--game", fake.Root], null), kinds);

    [TestMethod]
    public void EveryPresentSoundFileIsListedWithItsEntryCountAndKindAndTheExitCodeIsZero()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", [1, 2]).Put("snd/03.SND", [1]);

        var report = Run(fake, new SoundKind());

        Assert.AreEqual(0, report.ExitCode);
        CollectionAssert.Contains(report.Lines.ToList(), "snd/01.SND: 1 entry (sound)");
        CollectionAssert.Contains(report.Lines.ToList(), "snd/03.SND: 1 entry (sound)");
    }

    [TestMethod]
    public void AFileWithSeveralEntriesAndKindsListsTheCountAndEachKindOnce()
    {
        using var fake = new FakeInstall();
        var kinds = new AssetKind[]
        {
            new ListedKind("palette", Entry("palette", "loca/A.PA", "1")),
            new ListedKind("sprite", Entry("sprite", "loca/A.PA", "2"), Entry("sprite", "loca/A.PA", "3")),
        };

        var report = Run(fake, kinds);

        CollectionAssert.Contains(report.Lines.ToList(), "loca/A.PA: 3 entries (palette, sprite)");
    }

    [TestMethod]
    public void AnEmptySoundFileIsReportedWithItsFileAndTheOtherFilesAreStillRead()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", []).Put("snd/02.SND", [1]);

        var report = Run(fake, new SoundKind());

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL snd/01.SND: ") && l.Contains("empty")));
        CollectionAssert.Contains(report.Lines.ToList(), "snd/02.SND: 1 entry (sound)");
    }

    [TestMethod]
    public void AFailureNamesTheFileTheEntryAndTheReason()
    {
        using var fake = new FakeInstall();
        var bad = Entry("sprite", "loca/A.PA", "7", () => throw new InvalidDataException("run past the end"));

        var report = Run(fake, new ListedKind("sprite", bad));

        CollectionAssert.Contains(report.Lines.ToList(), "FAIL loca/A.PA entry 7: run past the end");
    }

    [TestMethod]
    public void AMissingSndFolderIsReportedAndTheOtherKindsAreStillRead()
    {
        using var fake = new FakeInstall();
        var other = new ListedKind("text", Entry("text", "loca/T.TXT", null));

        var report = Run(fake, new SoundKind(), other);

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL snd: no sound file")));
        CollectionAssert.Contains(report.Lines.ToList(), "loca/T.TXT: 1 entry (text)");
    }

    [TestMethod]
    public void AKindThatCannotListItsAssetsFailsOnceAndTheOtherKindsAreStillRead()
    {
        using var fake = new FakeInstall();
        var other = new ListedKind("text", Entry("text", "loca/T.TXT", null));

        var report = Run(fake, new ThrowingKind(), other);

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL broken: ") && l.Contains("cannot list")));
        CollectionAssert.Contains(report.Lines.ToList(), "loca/T.TXT: 1 entry (text)");
    }

    sealed class ThrowingKind : AssetKind
    {
        public override string Name => "broken";

        public override IEnumerable<Asset> Enumerate(Install install) => throw new IOException("cannot list");
    }

    [TestMethod]
    public void FilesTheProgramDoesNotReadAppearNowhereInTheReport()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", [1]).Put("snd/100.SND", [1]).Put("loca/OTHER.DAT", [1]);

        var report = Run(fake, new SoundKind());

        CollectionAssert.Contains(report.Lines.ToList(), "snd/01.SND: 1 entry (sound)");
        Assert.IsFalse(report.Lines.Any(l => l.Contains("100.SND") || l.Contains("OTHER.DAT")));
    }

    [TestMethod]
    public void WithoutAnInstallTheExitCodeIsTwoAndTheReportNamesBothWaysToGiveOne()
    {
        var report = AssetCheck.Run(LaunchArgs.Parse(["--check"], null), [new SoundKind()]);

        Assert.AreEqual(2, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.Contains("--game") && l.Contains("SIEDLER_DIR")));
    }

    [TestMethod]
    public void AFolderWithoutTheProgramExitsTwo()
    {
        using var fake = new FakeInstall(withProgram: false).Put("snd/01.SND", []);

        var report = Run(fake, new SoundKind());

        Assert.AreEqual(2, report.ExitCode);
        Assert.IsFalse(report.Lines.Any(l => l.StartsWith("FAIL")));
    }

    [TestMethod]
    public void AFlagWithoutItsValueExitsTwo()
    {
        using var fake = new FakeInstall();

        var report = AssetCheck.Run(LaunchArgs.Parse(["--check", "--game"], fake.Root), [new SoundKind()]);

        Assert.AreEqual(2, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.Contains("--game needs a value")));
    }

    [TestMethod]
    public void ExitTwoWinsOverOneWhenTheInstallIsMissingAndAnEntryWouldFail()
    {
        var report = AssetCheck.Run(
            LaunchArgs.Parse(["--check", "--game", "/no/such/folder"], null),
            [new SoundKind()]
        );

        Assert.AreEqual(2, report.ExitCode);
    }
}
