using System.IO;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>An install whose names differ in letter case from the program's spelling, on a case-sensitive folder.</summary>
[TestClass]
public sealed class InstallCaseTests
{
    static Install Open(CaseSensitiveFolder folder)
    {
        Assert.IsTrue(
            Install.TryOpen(LaunchArgs.Parse(["--game", folder.Root], null), out var install, out var problem),
            problem
        );
        return install!;
    }

    [TestMethod]
    public void ALowerCasedProgramOpensAsAnInstall()
    {
        using var folder = new CaseSensitiveFolder().Put("siedler1.exe", [1]);

        Open(folder);
    }

    [TestMethod]
    public void AFolderWithTheProgramInNoSpellingIsRefusedWithTheSameProblem()
    {
        using var folder = new CaseSensitiveFolder().Put("siedler2.exe", [1]);

        var opened = Install.TryOpen(LaunchArgs.Parse(["--game", folder.Root], null), out _, out var problem);

        Assert.IsFalse(opened);
        Assert.AreEqual($"no Siedler1.exe in {folder.Root}, so it is not an install ({Install.HowToGive})", problem);
    }

    [TestMethod]
    public void AFileInAFolderOfAnotherCaseIsFoundAndRead()
    {
        using var folder = new CaseSensitiveFolder().Put("siedler1.exe", [1]).Put("snd/sound.dat", [7, 8]);
        var install = Open(folder);

        Assert.IsTrue(install.Exists("SND/Sound.DAT"));
        CollectionAssert.AreEqual(new byte[] { 7, 8 }, install.ReadAll("SND/Sound.DAT"));
    }

    [TestMethod]
    public void TheExactSpellingWinsOverOtherCases()
    {
        using var folder = new CaseSensitiveFolder()
            .Put("Siedler1.exe", [1])
            .Put("gfx/a.dat", [1])
            .Put("gfx/A.dat", [2])
            .Put("GFX/A.dat", [3]);
        var install = Open(folder);

        CollectionAssert.AreEqual(new byte[] { 2 }, install.ReadAll("gfx/A.dat"));
        CollectionAssert.AreEqual(new byte[] { 3 }, install.ReadAll("GFX/A.dat"));
    }

    [TestMethod]
    public void WithoutTheExactSpellingTheFirstInOrdinalOrderWins()
    {
        using var folder = new CaseSensitiveFolder().Put("Siedler1.exe", [1]).Put("x.dat", [1]).Put("X.dat", [2]);
        var install = Open(folder);

        // Ordinal order puts "X.dat" (0x58) before "x.dat" (0x78).
        CollectionAssert.AreEqual(new byte[] { 2 }, install.ReadAll("x.DAT"));
    }

    [TestMethod]
    public void AFolderNameMatchesOnlyAFolderAndAFileNameOnlyAFile()
    {
        using var folder = new CaseSensitiveFolder()
            .Put("Siedler1.exe", [1])
            .Put("DATA", [9])
            .Put("data/f.dat", [4])
            .Put("F.DAT/x", [5])
            .Put("f.dat", [6]);
        var install = Open(folder);

        // "DATA" is a file, so the folder segment takes "data"; "F.DAT" is a folder, so the file takes "f.dat".
        CollectionAssert.AreEqual(new byte[] { 4 }, install.ReadAll("Data/F.dat"));
        CollectionAssert.AreEqual(new byte[] { 6 }, install.ReadAll("F.dat"));
    }

    [TestMethod]
    public void AFileAbsentInEverySpellingIsMissing()
    {
        using var folder = new CaseSensitiveFolder().Put("Siedler1.exe", [1]).Put("snd/a.dat", [1]);
        var install = Open(folder);

        Assert.IsFalse(install.Exists("snd/b.dat"));
        Assert.IsFalse(install.Exists("gfx/a.dat"));
        Assert.ThrowsExactly<FileNotFoundException>(() => install.ReadAll("snd/b.dat"));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => install.ReadAll("gfx/a.dat"));
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void AFolderThatCannotBeListedReadsAsMissing()
    {
        if (!System.OperatingSystem.IsLinux() && !System.OperatingSystem.IsMacOS())
        {
            Assert.Inconclusive("Folder modes need Unix.");
        }

        using var folder = new CaseSensitiveFolder().Put("Siedler1.exe", [1]).Put("snd/a.dat", [1]);
        var locked = Path.Combine(folder.Root, "snd");
        File.SetUnixFileMode(locked, UnixFileMode.UserWrite);
        try
        {
            if (Directory.Exists(locked) && CanList(locked))
            {
                Assert.Inconclusive("The process lists folders whatever their mode (root).");
            }

            Assert.IsFalse(Open(folder).Exists("SND/A.DAT"));
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [TestMethod]
    public void FindingAFileWritesNothing()
    {
        using var folder = new CaseSensitiveFolder().Put("siedler1.exe", [1]).Put("snd/a.dat", [1]);
        var before = Directory.GetFileSystemEntries(folder.Root, "*", SearchOption.AllDirectories);
        var install = Open(folder);

        install.Exists("SND/A.DAT");
        install.ReadAll("SND/A.DAT");
        install.Exists("GFX/B.DAT");

        CollectionAssert.AreEquivalent(
            before,
            Directory.GetFileSystemEntries(folder.Root, "*", SearchOption.AllDirectories)
        );
    }

    static bool CanList(string path)
    {
        try
        {
            Directory.EnumerateFileSystemEntries(path).GetEnumerator().MoveNext();
            return true;
        }
        catch (System.UnauthorizedAccessException)
        {
            return false;
        }
    }
}
