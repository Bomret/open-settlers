using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TextKindTests
{
    static Install Open(FakeInstall fake)
    {
        Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _);

        return install!;
    }

    static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    static FakeInstall Install3(string eng, string ger, string fre) =>
        new FakeInstall()
            .Put("loca/eng.txt", Bytes(eng))
            .Put("loca/ger.txt", Bytes(ger))
            .Put("loca/fre.txt", Bytes(fre));

    [TestMethod]
    public void TheThreeLanguageFilesListInTheProgramsOrderWhetherPresentOrNot()
    {
        using var fake = new FakeInstall();

        var assets = new TextKind().Enumerate(Open(fake)).ToList();

        CollectionAssert.AreEqual(
            new[] { "loca/eng.txt", "loca/ger.txt", "loca/fre.txt" },
            assets.Select(a => a.File).ToList()
        );
        Assert.IsTrue(assets.All(a => a.Kind == "text"));
    }

    [TestMethod]
    public void AMissingFileFailsOnLoadAndTheOthersStillLoad()
    {
        using var fake = new FakeInstall()
            .Put("loca/eng.txt", Bytes("ALPHA=\"one\"\r\n"))
            .Put("loca/fre.txt", Bytes("ALPHA=\"un\"\r\n"));

        var assets = new TextKind().Enumerate(Open(fake)).ToList();

        Assert.ThrowsExactly<FileNotFoundException>(() => assets[1].Load());
        var table = (TextPreview)assets[0].Load();
        CollectionAssert.AreEqual(new[] { "eng", "ger", "fre" }, table.Languages.ToArray());
        CollectionAssert.AreEqual(new[] { "one", null, "un" }, table.Rows.Single().Values.ToArray());
    }

    [TestMethod]
    public void AMissingFileFailsWithItsInstallRelativePathAsTheOtherKindsDo()
    {
        using var fake = new FakeInstall().Put("loca/eng.txt", Bytes("ALPHA=\"one\"\r\n"));

        var failure = Assert.ThrowsExactly<FileNotFoundException>(() =>
            new TextKind().Enumerate(Open(fake)).ToList()[1].Load()
        );

        Assert.AreEqual("the file loca/ger.txt is missing: the install must hold it", failure.Message);
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
    public void AnUnreadableFileFailsOnLoadAndTheOthersStillLoad()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("File modes do not deny reading on Windows.");
        }

        using var fake = Install3("ALPHA=\"one\"\r\n", "ALPHA=\"eins\"\r\n", "ALPHA=\"un\"\r\n");
        var ger = Path.Combine(fake.Root, "loca/ger.txt");
        File.SetUnixFileMode(ger, UnixFileMode.None);
        try
        {
            if (CanStillRead(ger))
            {
                Assert.Inconclusive("The process reads files whatever their mode (root).");
            }

            var assets = new TextKind().Enumerate(Open(fake)).ToList();

            Assert.ThrowsExactly<UnauthorizedAccessException>(() => assets[1].Load());
            var table = (TextPreview)assets[0].Load();
            CollectionAssert.AreEqual(new[] { "one", null, "un" }, table.Rows.Single().Values.ToArray());
        }
        finally
        {
            File.SetUnixFileMode(ger, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [TestMethod]
    public void ThePreviewIsOneTableOfEveryKeyWithAValuePerLanguage()
    {
        using var fake = Install3(
            "ALPHA=\"one\"\r\nBETA=\"two\"\r\n",
            "ALPHA=\"eins\"\r\nBETA=\"zwei\"\r\n",
            "ALPHA=\"un\"\r\nBETA=\"deux\"\r\n"
        );

        var asset = new TextKind().Enumerate(Open(fake)).Last();

        var table = (TextPreview)asset.Load();
        CollectionAssert.AreEqual(new[] { "ALPHA", "BETA" }, table.Rows.Select(r => r.Key).ToArray());
        CollectionAssert.AreEqual(new[] { "two", "zwei", "deux" }, table.Rows[1].Values.ToArray());
    }

    [TestMethod]
    public void AKeyOneLanguageLacksShowsAnEmptyCellAndKeysKeepFirstAppearanceOrder()
    {
        using var fake = Install3(
            "ALPHA=\"one\"\r\n",
            "ALPHA=\"eins\"\r\nGAMMA=\"drei\"\r\n",
            "BETA=\"deux\"\r\nALPHA=\"un\"\r\n"
        );

        var table = (TextPreview)new TextKind().Enumerate(Open(fake)).First().Load();

        CollectionAssert.AreEqual(new[] { "ALPHA", "GAMMA", "BETA" }, table.Rows.Select(r => r.Key).ToArray());
        CollectionAssert.AreEqual(new[] { "one", "eins", "un" }, table.Rows[0].Values.ToArray());
        CollectionAssert.AreEqual(new string?[] { null, "drei", null }, table.Rows[1].Values.ToArray());
        CollectionAssert.AreEqual(new string?[] { null, null, "deux" }, table.Rows[2].Values.ToArray());
    }

    [TestMethod]
    public void SpacesInsideAValueSurviveToTheTable()
    {
        using var fake = Install3("ALPHA=\"  one  \"\r\n", "ALPHA=\"   \"\r\n", "ALPHA=\"\"\r\n");

        var table = (TextPreview)new TextKind().Enumerate(Open(fake)).First().Load();

        CollectionAssert.AreEqual(new[] { "  one  ", "   ", "" }, table.Rows.Single().Values.ToArray());
    }

    [TestMethod]
    public void ARejectedLineFailsOnlyItsOwnFileWithFileLineAndReasonAndTheTableStillShowsTheOthers()
    {
        using var fake = Install3(
            "ALPHA=\"one\"\r\nBETA=\"two\"\r\n",
            "ALPHA=\"eins\"\r\nno equals sign\r\nBETA=\"zwei\"\r\n",
            "ALPHA=\"un\"\r\n"
        );

        var assets = new TextKind().Enumerate(Open(fake)).ToList();

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => assets[1].Load());
        StringAssert.Contains(failure.Message, "line 2");
        StringAssert.Contains(failure.Message, "'='");
        var table = (TextPreview)assets[0].Load();
        CollectionAssert.AreEqual(new[] { "two", "zwei", null }, table.Rows[1].Values.ToArray());
        Assert.IsInstanceOfType<TextPreview>(assets[2].Load());
    }

    [TestMethod]
    public void TheCheckNamesTheFileAndLineOfARejectedLineAndStillReadsTheOtherFiles()
    {
        using var fake = Install3("ALPHA=\"one\"\r\n", "ALPHA=\"eins\"\r\nbroken\r\n", "ALPHA=\"un\"\r\n");

        var report = AssetCheck.Run(LaunchArgs.Parse(["--check", "--game", fake.Root], null), [new TextKind()]);

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL loca/ger.txt: line 2")));
        Assert.IsTrue(report.Lines.Contains("loca/eng.txt: 1 entry (text)"));
        Assert.IsTrue(report.Lines.Contains("loca/fre.txt: 1 entry (text)"));
    }

    [TestMethod]
    public void TheTextKindRegistersItselfThroughDiscover()
    {
        Assert.IsTrue(AssetKind.Discover().Any(k => k is TextKind));
    }
}
