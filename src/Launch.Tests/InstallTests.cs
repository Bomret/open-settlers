using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The launch's opening of an install; the asset check's and the browser's half of this class is in src/Assets.Tests.
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
}
