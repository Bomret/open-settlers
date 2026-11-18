using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Import.HistoryEdition;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class InstallTests
{
    [TestMethod]
    public void AFolderWithTheProgramOpensAsAnInstall()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", [0x80, 0x90]);

        var opened = LaunchInstall.TryOpen(
            LaunchArgs.Parse(["--game", fake.Root], null),
            out var install,
            out var problem
        );

        Assert.IsTrue(opened);
        // The install is the one at the folder: its assets port lists the file the folder holds.
        var files = new HistoryEditionAssets(install!).Kinds().SelectMany(kind => kind.Enumerate()).Select(a => a.File);
        CollectionAssert.Contains(files.ToList(), "snd/01.SND");
        Assert.IsNull(problem);
    }

    [TestMethod]
    public void NoInstallGivenNamesTheGameFlagAndTheEnvironmentVariable()
    {
        var opened = LaunchInstall.TryOpen(LaunchArgs.Parse([], null), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, "--game");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }

    [TestMethod]
    public void AMissingFolderIsAProblemThatNamesTheFolderAndBothWaysToGiveOne()
    {
        var missing = Path.Combine(Path.GetTempPath(), "open-settlers-tests-missing-folder");

        var opened = LaunchInstall.TryOpen(LaunchArgs.Parse(["--game", missing], null), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, missing);
        StringAssert.Contains(problem, "--game");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }

    [TestMethod]
    public void AFolderWithoutTheProgramIsAProblemThatNamesTheProgramAndBothWaysToGiveOne()
    {
        using var fake = new FakeInstall(withProgram: false);

        var opened = LaunchInstall.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, "Siedler1.exe");
        StringAssert.Contains(problem, "--game");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }

    [TestMethod]
    public void ALaunchProblemIsReportedAndTheInstallIsNotOpened()
    {
        using var fake = new FakeInstall();

        var opened = LaunchInstall.TryOpen(LaunchArgs.Parse(["--game"], fake.Root), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, "--game needs a value");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }

    [TestMethod]
    public void TheCheckAndTheBrowserNeverWriteToTheInstall()
    {
        using var fake = new FakeInstall()
            .Put("snd/01.SND", [0x80, 0x90])
            .Put("loca/SPAD.PA", ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [3] = new byte[768], [5] = [1] }));
        foreach (var file in Directory.EnumerateFiles(fake.Root, "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTimeUtc(file, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        var before = Snapshot(fake.Root);
        var args = LaunchArgs.Parse(["--game", fake.Root], null);
        var assets = LaunchInstall.Assets(args);
        AssetCheck.Run(args, assets);
        var state = AssetBrowserState.Open(assets);
        foreach (var category in state.Categories)
        {
            state.SelectKind(category);
            for (var i = 0; i < state.Assets.Count; i++)
            {
                state.SelectAsset(i);
            }
        }

        CollectionAssert.AreEqual(before, Snapshot(fake.Root));
    }

    // Each file under the root with its size and modification time, in path order.
    static List<string> Snapshot(string root) =>
        [
            .. Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(f => $"{f} {new FileInfo(f).Length} {File.GetLastWriteTimeUtc(f):O}"),
        ];
}
