using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class InstallTests
{
    [TestMethod]
    public void AFolderWithTheProgramOpensAsAnInstall()
    {
        using var fake = new FakeInstall();

        var opened = Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out var problem);

        Assert.IsTrue(opened);
        Assert.AreEqual(fake.Root, install!.Root);
        Assert.IsNull(problem);
    }

    [TestMethod]
    public void NoInstallGivenNamesTheGameFlagAndTheEnvironmentVariable()
    {
        var opened = Install.TryOpen(LaunchArgs.Parse([], null), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, "--game");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }

    [TestMethod]
    public void AMissingFolderIsAProblemThatNamesTheFolderAndBothWaysToGiveOne()
    {
        var missing = Path.Combine(Path.GetTempPath(), "open-settlers-tests-missing-folder");

        var opened = Install.TryOpen(LaunchArgs.Parse(["--game", missing], null), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, missing);
        StringAssert.Contains(problem, "--game");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }

    [TestMethod]
    public void AFolderWithoutTheProgramIsAProblemThatNamesTheProgramAndBothWaysToGiveOne()
    {
        using var fake = new FakeInstall(withProgram: false);

        var opened = Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, "Siedler1.exe");
        StringAssert.Contains(problem, "--game");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }

    [TestMethod]
    public void ALaunchProblemIsReportedAndTheInstallIsNotOpened()
    {
        using var fake = new FakeInstall();

        var opened = Install.TryOpen(LaunchArgs.Parse(["--game"], fake.Root), out _, out var problem);

        Assert.IsFalse(opened);
        StringAssert.Contains(problem, "--game needs a value");
        StringAssert.Contains(problem, "SIEDLER_DIR");
    }
}
