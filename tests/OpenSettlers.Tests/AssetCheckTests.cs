using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>The asset check on a fake assets port: no install on disk.</summary>
[TestClass]
public sealed class AssetCheckTests
{
    static readonly LaunchArgs Check = LaunchArgs.Parse(["--check"], null);

    static Asset Entry(string kind, string file, string? entry, Func<Preview>? load = null, bool alias = false) =>
        new()
        {
            Kind = kind,
            File = file,
            Entry = entry,
            IsAlias = alias,
            Name = entry ?? file,
            Load = load ?? (() => new NotePreview { Summary = "ok" }),
        };

    static CheckReport Run(params IAssetKind[] kinds) => AssetCheck.Run(Check, new FakeAssets(kinds));

    [TestMethod]
    public void EveryListedFileIsReportedWithItsEntryCountAndKindAndTheExitCodeIsZero()
    {
        var report = Run(
            new ListedKind("sound", Entry("sound", "snd/01.SND", null), Entry("sound", "snd/03.SND", null))
        );

        Assert.AreEqual(0, report.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "snd/01.SND: 1 entry (sound)",
                "snd/03.SND: 1 entry (sound)",
                "2 files read, 2 entries read, 0 failed",
            },
            report.Lines.ToList()
        );
    }

    [TestMethod]
    public void AFileWithSeveralEntriesAndKindsListsTheCountAndEachKindOnce()
    {
        var report = Run(
            new ListedKind("palette", Entry("palette", "loca/A.PA", "1")),
            new ListedKind("sprite", Entry("sprite", "loca/A.PA", "2"), Entry("sprite", "loca/A.PA", "3"))
        );

        CollectionAssert.Contains(report.Lines.ToList(), "loca/A.PA: 3 entries (palette, sprite)");
    }

    [TestMethod]
    public void AliasesAreCountedApartFromEntries()
    {
        var report = Run(
            new ListedKind("sprite", Entry("sprite", "loca/A.PA", "2"), Entry("sprite", "loca/A.PA", "3", alias: true))
        );

        CollectionAssert.Contains(report.Lines.ToList(), "loca/A.PA: 1 entry, 1 alias (sprite)");
        Assert.AreEqual("1 files read, 1 entries read, 1 alias, 0 failed", report.Lines[^1]);
    }

    [TestMethod]
    public void AFailingFileIsReportedWithItsFileAndTheOtherFilesAreStillRead()
    {
        var bad = Entry("sound", "snd/01.SND", null, () => throw new InvalidDataException("the file is empty"));

        var report = Run(new ListedKind("sound", bad, Entry("sound", "snd/02.SND", null)));

        Assert.AreEqual(1, report.ExitCode);
        CollectionAssert.Contains(report.Lines.ToList(), "FAIL snd/01.SND: the file is empty");
        CollectionAssert.Contains(report.Lines.ToList(), "snd/02.SND: 1 entry (sound)");
    }

    [TestMethod]
    public void AFailureNamesTheFileTheEntryAndTheReason()
    {
        var bad = Entry("sprite", "loca/A.PA", "7", () => throw new InvalidDataException("run past the end"));

        var report = Run(new ListedKind("sprite", bad));

        CollectionAssert.Contains(report.Lines.ToList(), "FAIL loca/A.PA entry 7: run past the end");
    }

    [TestMethod]
    public void AFileThatFailsAsAWholeIsReportedOnceHoweverManyKindsListIt()
    {
        var missing = new FileNotFoundException("the archive loca/A.PA is missing");

        var report = Run(
            new ListedKind("palette", Entry("palette", "loca/A.PA", null, () => throw missing)),
            new ListedKind("sprite", Entry("sprite", "loca/A.PA", null, () => throw missing))
        );

        Assert.AreEqual(1, report.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "FAIL loca/A.PA: the archive loca/A.PA is missing", "0 files read, 0 entries read, 1 failed" },
            report.Lines.ToList()
        );
    }

    [TestMethod]
    public void AProgrammingErrorWhileLoadingIsNotReportedAsAnUnreadableEntry()
    {
        var bug = Entry("sprite", "loca/A.PA", "7", () => throw new IndexOutOfRangeException("reader bug"));

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => Run(new ListedKind("sprite", bug)));
    }

    [TestMethod]
    public void AKindThatCannotListItsAssetsFailsOnceAndTheOtherKindsAreStillRead()
    {
        var broken = new ListedKind("broken") { ListingFails = new IOException("cannot list") };

        var report = Run(broken, new ListedKind("text", Entry("text", "loca/T.TXT", null)));

        Assert.AreEqual(1, report.ExitCode);
        CollectionAssert.Contains(report.Lines.ToList(), "FAIL broken: cannot list the assets: cannot list");
        CollectionAssert.Contains(report.Lines.ToList(), "loca/T.TXT: 1 entry (text)");
    }

    [TestMethod]
    public void AKindThatCrashesWhileListingPropagates()
    {
        var buggy = new ListedKind("buggy") { ListingFails = new IndexOutOfRangeException("bug") };

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => Run(buggy));
    }

    [TestMethod]
    public void TwoKindsOfOneNameClashAndUniqueNamesDoNot()
    {
        var clash = AssetKinds.NameClash([new ListedKind("text"), new ListedKind("sound"), new ListedKind("text")]);

        StringAssert.Contains(clash, "\"text\"");
        Assert.IsNull(AssetKinds.NameClash([new ListedKind("text"), new ListedKind("sound")]));
        Assert.IsNull(AssetKinds.NameClash([]));
    }

    [TestMethod]
    public void TwoKindsOfTheSameNameStopTheCheckWithExitCodeThree()
    {
        var report = Run(
            new ListedKind("text", Entry("text", "snd/01.SND", "1")),
            new ListedKind("text", Entry("text", "snd/01.SND", "2"))
        );

        Assert.AreEqual(3, report.ExitCode);
        Assert.HasCount(1, report.Lines);
        StringAssert.StartsWith(report.Lines[0], "ERROR ");
        StringAssert.Contains(report.Lines[0], "\"text\"");
    }

    [TestMethod]
    public void APortWithNoUsableDataExitsTwoWithItsReasonAndReadsNothing()
    {
        var report = AssetCheck.Run(
            Check,
            new UnusableAssets("no install given: pass --game <dir> or set SIEDLER_DIR")
        );

        Assert.AreEqual(2, report.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "ERROR no install given: pass --game <dir> or set SIEDLER_DIR" },
            report.Lines.ToList()
        );
    }

    [TestMethod]
    public void ABadArgumentExitsFourBeforeThePortIsAsked()
    {
        var report = AssetCheck.Run(LaunchArgs.Parse(["--check", "--map"], null), new UnusableAssets("never asked"));

        Assert.AreEqual(4, report.ExitCode);
        Assert.IsFalse(report.Lines.Any(l => l.Contains("never asked")));
    }

    [TestMethod]
    public void AFlagWithoutItsValueExitsFour()
    {
        var report = AssetCheck.Run(LaunchArgs.Parse(["--check", "--game"], null), new FakeAssets());

        Assert.AreEqual(4, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.Contains("--game needs a value")));
    }
}
