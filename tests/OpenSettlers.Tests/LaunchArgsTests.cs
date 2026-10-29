using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class LaunchArgsTests
{
    [TestMethod]
    public void WithoutAModeFlagTheModeIsTheAssetBrowser()
    {
        var args = LaunchArgs.Parse(["--game", "/siedler"], null);

        Assert.AreEqual(LaunchMode.Browser, args.Mode);
    }

    [TestMethod]
    public void TheCheckFlagSelectsTheAssetCheck()
    {
        var args = LaunchArgs.Parse(["--check", "--game", "/siedler"], null);

        Assert.AreEqual(LaunchMode.Check, args.Mode);
    }

    [TestMethod]
    public void TheGameFlagNamesTheInstall()
    {
        var args = LaunchArgs.Parse(["--game", "/siedler"], null);

        Assert.AreEqual("/siedler", args.Game);
    }

    [TestMethod]
    public void TheEnvironmentValueStandsInWhenTheGameFlagIsAbsent()
    {
        var args = LaunchArgs.Parse(["--check"], "/from-env");

        Assert.AreEqual("/from-env", args.Game);
    }

    [TestMethod]
    public void TheGameFlagWinsOverTheEnvironmentValue()
    {
        var args = LaunchArgs.Parse(["--game", "/from-flag"], "/from-env");

        Assert.AreEqual("/from-flag", args.Game);
    }

    [TestMethod]
    public void NoGameFlagAndNoEnvironmentValueLeavesNoInstallAndNoProblem()
    {
        var args = LaunchArgs.Parse(["--check"], null);

        Assert.IsNull(args.Game);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    public void AnEmptyEnvironmentValueStandsInForNothing()
    {
        var args = LaunchArgs.Parse([], "");

        Assert.IsNull(args.Game);
    }

    [TestMethod]
    public void AGameFlagAtTheEndIsAProblemAndNeverAPath()
    {
        var args = LaunchArgs.Parse(["--check", "--game"], "/from-env");

        Assert.IsNull(args.Game);
        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--game");
    }

    [TestMethod]
    public void AGameFlagFollowedByAnotherFlagIsAProblemAndNeverAPath()
    {
        var args = LaunchArgs.Parse(["--game", "--check"], null);

        Assert.IsNull(args.Game);
        Assert.HasCount(1, args.Problems);
        Assert.AreEqual(LaunchMode.Check, args.Mode);
    }
}
