using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The asset check's reading of the launch's ports; the launch's half of this class is in src/Launch.Tests. Each launch is
// parsed with no SIEDLER_DIR, so an install of the host does not stand in.
[TestClass]
public sealed class LaunchInstallTests
{
    [TestMethod]
    public void AnAssetCheckWithNoInstallGivenExitsTwoNamingBothWaysToGiveOne()
    {
        var args = LaunchArgs.Parse(["--check"], null);

        var report = AssetCheck.Run(args, LaunchInstall.Assets(args));

        Assert.AreEqual(
            (2, $"ERROR no install given: {LaunchInstall.HowToGive}"),
            (report.ExitCode, string.Join("\n", report.Lines))
        );
        StringAssert.Contains(LaunchInstall.HowToGive, "pass --game <dir> or set SIEDLER_DIR");
        StringAssert.Contains(LaunchInstall.HowToGive, "--content-set basic");
    }

    [TestMethod]
    public void AnAssetCheckWithABadArgumentExitsFour()
    {
        var args = LaunchArgs.Parse(["--check", "--game"], null);

        var report = AssetCheck.Run(args, LaunchInstall.Assets(args));

        Assert.AreEqual(LaunchArgs.BadArgumentExitCode, report.ExitCode);
    }
}
