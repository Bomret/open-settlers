using System;
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

    static string RepositoryRoot()
    {
        var folder = AppContext.BaseDirectory;
        while (folder is not null && !File.Exists(Path.Combine(folder, "project.godot")))
        {
            folder = Path.GetDirectoryName(folder);
        }

        return folder ?? throw new InvalidOperationException("no project.godot above the test binaries");
    }

    // Seed info on the basic set reads the rule files of content/basic/ and nothing else: no install is named anywhere,
    // and SIEDLER_DIR points at a folder that does not exist.
    [TestMethod]
    public void SeedInfoOnTheBasicSetReadsTheRuleFilesAndNoInstall()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var args = LaunchArgs.Parse(["--seed-info", "--seed", "1234567812345678", "--content-set", "basic"], missing);
        Seed.TryParse("1234567812345678", out var seed, out _);

        var code = SeedInfo.Run(
            LaunchInstall.RuleTables(args, RepositoryRoot()),
            "1234567812345678",
            seed,
            MapSize.Offered[0],
            out var text
        );

        Assert.AreEqual(0, code, text);
        StringAssert.Contains(text, "1234567812345678");
    }

    [TestMethod]
    public void SeedInfoOnABrokenBasicRuleFileExitsWithNoTablesAndNamesTheFileAndKey()
    {
        var project = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var content = Path.Combine(project, "content", "basic");
        Directory.CreateDirectory(Path.Combine(content, "rules"));
        foreach (var file in OpenSettlersRuleTables.Files)
        {
            File.Copy(Path.Combine(RepositoryRoot(), "content", "basic", file), Path.Combine(content, file));
        }

        File.WriteAllText(Path.Combine(content, OpenSettlersRuleTables.WorldFile), "{ \"objectClasses\": 3 }");
        var args = LaunchArgs.Parse(["--seed-info", "--seed", "1234567812345678", "--content-set", "basic"], null);
        Seed.TryParse("1234567812345678", out var seed, out _);
        try
        {
            var code = SeedInfo.Run(
                LaunchInstall.RuleTables(args, project),
                "1234567812345678",
                seed,
                MapSize.Offered[0],
                out var text
            );

            Assert.AreEqual(SeedInfo.NoTablesExitCode, code);
            StringAssert.Contains(text, "rules/world.jsonc: ");
        }
        finally
        {
            Directory.Delete(project, true);
        }
    }

    [TestMethod]
    public void SeedInfoOnAMissingInstallFolderExitsWithNoTablesAndNamesTheFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Seed.TryParse("1234567812345678", out var seed, out _);

        var code = SeedInfo.Run(
            LaunchInstall.RuleTables(LaunchArgs.Parse(["--game", folder], null), RepositoryRoot()),
            "1234567812345678",
            seed,
            MapSize.Offered[0],
            out var text
        );

        Assert.AreEqual(SeedInfo.NoTablesExitCode, code);
        StringAssert.Contains(text, $"install folder not found: {folder}");
    }
}
