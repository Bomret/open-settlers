using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The ports the launch builds from its arguments (LaunchInstall.Assets, LaunchInstall.RuleTables) through the commands that read
// them. Each launch is parsed with no SIEDLER_DIR, so an install of the host does not stand in.
[TestClass]
public sealed class LaunchInstallTests
{
    [TestMethod]
    public void AnAssetCheckWithNoInstallGivenExitsTwoNamingBothWaysToGiveOne()
    {
        var args = LaunchArgs.Parse(["--check"], null);

        var report = AssetCheck.Run(args, LaunchInstall.Assets(args));

        Assert.AreEqual(
            (2, "ERROR no install given: pass --game <dir> or set SIEDLER_DIR"),
            (report.ExitCode, string.Join("\n", report.Lines))
        );
    }

    [TestMethod]
    public void AnAssetCheckWithABadArgumentExitsFour()
    {
        var args = LaunchArgs.Parse(["--check", "--game"], null);

        var report = AssetCheck.Run(args, LaunchInstall.Assets(args));

        Assert.AreEqual(LaunchArgs.BadArgumentExitCode, report.ExitCode);
    }

    [TestMethod]
    public void SeedInfoOnAMissingInstallFolderExitsWithNoTablesAndNamesTheFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Seed.TryParse("7", out var seed, out _);

        var code = SeedInfo.Run(
            LaunchInstall.RuleTables(LaunchArgs.Parse(["--game", folder], null)),
            "7",
            seed,
            MapSize.Offered[0],
            out var text
        );

        Assert.AreEqual(SeedInfo.NoTablesExitCode, code);
        StringAssert.Contains(text, $"install folder not found: {folder}");
    }
}
