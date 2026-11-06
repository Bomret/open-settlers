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

    [TestMethod]
    public void TheMapFlagSelectsTheMapView()
    {
        var args = LaunchArgs.Parse(["--map", "--game", "/siedler"], null);

        Assert.AreEqual(LaunchMode.Map, args.Mode);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    public void AModeFlagAfterTheGameFlagIsNeverItsValue()
    {
        var args = LaunchArgs.Parse(["--game", "--map"], null);

        Assert.IsNull(args.Game);
        Assert.HasCount(1, args.Problems);
        Assert.AreEqual(LaunchMode.Map, args.Mode);
    }

    [TestMethod]
    public void TwoModeFlagsAreAProblem()
    {
        var args = LaunchArgs.Parse(["--check", "--map", "--game", "/siedler"], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--check");
        StringAssert.Contains(args.Problems[0], "--map");
        Assert.AreEqual("/siedler", args.Game);
    }

    [TestMethod]
    public void ARepeatedModeFlagIsNoConflict()
    {
        var args = LaunchArgs.Parse(["--map", "--map"], null);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual(LaunchMode.Map, args.Mode);
    }

    [TestMethod]
    public void AModeFlagInsideAPathIsNotAMode()
    {
        var args = LaunchArgs.Parse(["--game", "/siedler/--map"], null);

        Assert.AreEqual(LaunchMode.Browser, args.Mode);
        Assert.AreEqual("/siedler/--map", args.Game);
    }

    [TestMethod]
    public void TheGameViewFlagSelectsTheGame()
    {
        var args = LaunchArgs.Parse(["--game-view", "--game", "/siedler"], null);

        Assert.AreEqual(LaunchMode.GameView, args.Mode);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    [DataRow("--map")]
    [DataRow("--game-view")]
    public void TheSeedAndSizeFlagsGiveTheMapsSeedAndSize(string mode)
    {
        var args = LaunchArgs.Parse([mode, "--seed", "1234567812345678", "--size", "2"], null);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual("1234567812345678", args.SeedText);
        Assert.AreEqual(2, args.Size?.Value);
        Assert.IsNotNull(args.Seed);
    }

    [TestMethod]
    public void BothSpellingsOfOneSeedParseToTheSameSeed()
    {
        var plain = LaunchArgs.Parse(["--map", "--seed", "1234567812345678"], null);
        var grouped = LaunchArgs.Parse(["--map", "--seed", "1234 5678 1234 5678"], null);

        Assert.IsEmpty(grouped.Problems);
        Assert.AreEqual(plain.Seed, grouped.Seed);
    }

    [TestMethod]
    public void WithoutSeedAndSizeFlagsTheyAreNull()
    {
        var args = LaunchArgs.Parse(["--map"], null);

        Assert.IsNull(args.Seed);
        Assert.IsNull(args.SeedText);
        Assert.IsNull(args.Size);
    }

    [TestMethod]
    public void TheSeedInfoFlagSelectsTheSeedInfoMode()
    {
        var args = LaunchArgs.Parse(["--seed-info", "--seed", "1234567812345678", "--size", "3"], null);

        Assert.AreEqual(LaunchMode.SeedInfo, args.Mode);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    [DataRow("--map")]
    [DataRow("--check")]
    public void TheGameViewFlagWithAnotherModeFlagIsAProblemAndSelectsNoMode(string other)
    {
        var args = LaunchArgs.Parse(["--game-view", other], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--game-view");
        StringAssert.Contains(args.Problems[0], other);
        Assert.AreEqual(LaunchMode.Browser, args.Mode);
    }

    [TestMethod]
    public void SeedInfoWithoutASeedIsAProblem()
    {
        var args = LaunchArgs.Parse(["--seed-info", "--size", "2"], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--seed-info needs --seed");
    }

    [TestMethod]
    [DataRow("--seed")]
    [DataRow("--size")]
    [DataRow("--game")]
    public void AFlagWithoutAValueIsAProblem(string flag)
    {
        var args = LaunchArgs.Parse(["--map", flag], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], flag);
    }

    [TestMethod]
    public void AFlagBeforeAnotherFlagHasNoValue()
    {
        var args = LaunchArgs.Parse(["--map", "--seed", "--size", "2"], null);

        Assert.HasCount(1, args.Problems);
        Assert.AreEqual(2, args.Size?.Value);
    }

    [TestMethod]
    public void AnUnknownFlagIsAProblem()
    {
        var args = LaunchArgs.Parse(["--map", "--bogus"], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--bogus");
    }

    [TestMethod]
    [DataRow("1234")]
    [DataRow("")]
    [DataRow("1234567812345679")]
    [DataRow("abcdefghabcdefgh")]
    public void ABadSeedIsAProblem(string seed)
    {
        var args = LaunchArgs.Parse(["--map", "--seed", seed], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--seed");
        Assert.IsNull(args.Seed);
    }

    [TestMethod]
    public void AnUnquotedFourGroupSeedIsOneProblemAboutTheSeed()
    {
        var args = LaunchArgs.Parse(["--map", "--seed", "1234", "5678", "1234", "5678"], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--seed");
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("0")]
    [DataRow("4")]
    [DataRow("2.0")]
    [DataRow("abc")]
    [DataRow("99999999999999999999")]
    public void ABadSizeIsAProblem(string size)
    {
        var args = LaunchArgs.Parse(["--map", "--size", size], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--size");
        Assert.IsNull(args.Size);
    }

    [TestMethod]
    [DataRow("--check", "--map")]
    [DataRow("--map", "--seed-info")]
    [DataRow("--seed-info", "--check")]
    public void TwoDifferentModeFlagsAreAProblemAndSelectNoMode(string first, string second)
    {
        var args = LaunchArgs.Parse([first, second], null);

        Assert.HasCount(1, args.Problems);
        Assert.AreEqual(LaunchMode.Browser, args.Mode);
    }

    [TestMethod]
    [DataRow("--check")]
    [DataRow("")]
    public void SeedAndSizeWithTheCheckOrTheBrowserAreProblems(string mode)
    {
        string[] flags = mode.Length > 0 ? [mode] : [];
        var seedArgs = LaunchArgs.Parse([.. flags, "--seed", "1234567812345678"], null);
        var sizeArgs = LaunchArgs.Parse([.. flags, "--size", "2"], null);

        Assert.HasCount(1, seedArgs.Problems);
        StringAssert.Contains(seedArgs.Problems[0], "--seed");
        Assert.HasCount(1, sizeArgs.Problems);
        StringAssert.Contains(sizeArgs.Problems[0], "--size");
    }

    [TestMethod]
    public void ThePlayFlagGivesTheScriptPath()
    {
        var args = LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null, movieMaker: true);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual("play/zoom.play", args.Play);
    }

    [TestMethod]
    public void ThePlayFlagWithTheGameViewFlagGivesTheGameViewAndTheScriptPath()
    {
        var args = LaunchArgs.Parse(["--game-view", "--play", "play/loop.play"], null, movieMaker: true);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual(LaunchMode.GameView, args.Mode);
        Assert.AreEqual("play/loop.play", args.Play);
    }

    [TestMethod]
    public void ThePlayFlagWithTheGameViewFlagKeepsTheSeedAndSize()
    {
        var args = LaunchArgs.Parse(
            ["--game-view", "--play", "play/loop.play", "--seed", "1234567812345678", "--size", "2"],
            null,
            movieMaker: true
        );

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual("1234567812345678", args.SeedText);
        Assert.AreEqual(2, args.Size!.Value);
    }

    [TestMethod]
    [DataRow(new string[] { "--play", "play/zoom.play" })]
    [DataRow(new string[] { "--check", "--play", "play/zoom.play" })]
    [DataRow(new string[] { "--seed-info", "--seed", "1234567812345678", "--play", "play/zoom.play" })]
    public void ThePlayFlagWithoutTheMapOrGameViewFlagIsAProblemNamingBoth(string[] given)
    {
        var args = LaunchArgs.Parse(given, null, movieMaker: true);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--play");
        StringAssert.Contains(args.Problems[0], "--map");
        StringAssert.Contains(args.Problems[0], "--game-view");
    }

    [TestMethod]
    public void ThePlayFlagWithTheGameViewFlagWithoutMovieMakerIsAProblem()
    {
        var args = LaunchArgs.Parse(["--game-view", "--play", "play/loop.play"], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--write-movie");
    }

    [TestMethod]
    public void ThePlayFlagWithoutAValueIsAProblem()
    {
        var args = LaunchArgs.Parse(["--map", "--play"], null, movieMaker: true);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--play needs a value");
        Assert.IsNull(args.Play);
    }

    [TestMethod]
    [DataRow("--seed", "1", "9")]
    [DataRow("--seed", "1", "bad")]
    [DataRow("--size", "1", "2")]
    [DataRow("--size", "1", "9")]
    [DataRow("--play", "play/a.play", "play/b.play")]
    public void ARepeatedValueFlagIsAProblem(string flag, string first, string second)
    {
        var args = LaunchArgs.Parse(["--map", flag, first, flag, second], null, movieMaker: true);

        StringAssert.Contains(string.Join("; ", args.Problems), $"repeated {flag}");
    }

    [TestMethod]
    public void ThePlayFlagWithoutMovieMakerIsAProblem()
    {
        var args = LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--write-movie");
    }

    [TestMethod]
    public void TheLayoutFlagSelectsTheGameViewAndNamesTheFile()
    {
        var args = LaunchArgs.Parse(["--layout", "layouts/a.jsonc", "--game", "/siedler"], null);

        Assert.AreEqual(LaunchMode.GameView, args.Mode);
        Assert.AreEqual("layouts/a.jsonc", args.Layout);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    public void TheLayoutFlagWithTheGameViewFlagGivesTheGameViewAndNoProblem()
    {
        var args = LaunchArgs.Parse(["--game-view", "--layout", "a.jsonc"], null);

        Assert.AreEqual(LaunchMode.GameView, args.Mode);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    public void TheLayoutFlagWithAPlayScriptIsNoProblem()
    {
        var args = LaunchArgs.Parse(["--layout", "a.jsonc", "--play", "play/a.play"], null, movieMaker: true);

        Assert.AreEqual("play/a.play", args.Play);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    public void TheGameViewFlagWithoutALayoutHasNoLayout()
    {
        var args = LaunchArgs.Parse(["--game-view", "--seed", "1234567812345678"], null);

        Assert.IsNull(args.Layout);
        Assert.AreEqual(LaunchMode.GameView, args.Mode);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    [DataRow("--seed", "1234567812345678")]
    [DataRow("--size", "2")]
    public void TheLayoutFlagWithTheSeedOrSizeFlagIsAProblem(string flag, string value)
    {
        var args = LaunchArgs.Parse(["--layout", "a.jsonc", flag, value], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], flag);
        StringAssert.Contains(args.Problems[0], "--layout");
    }

    [TestMethod]
    [DataRow("--map")]
    [DataRow("--check")]
    [DataRow("--seed-info")]
    public void TheLayoutFlagWithAnotherModeFlagIsAProblem(string other)
    {
        var args = LaunchArgs.Parse(["--layout", "a.jsonc", other, "--seed", "1234567812345678"], null);

        StringAssert.Contains(string.Join("; ", args.Problems), "--layout");
        StringAssert.Contains(string.Join("; ", args.Problems), other);
    }

    [TestMethod]
    public void ARepeatedLayoutFlagIsAProblem()
    {
        var args = LaunchArgs.Parse(["--layout", "a.jsonc", "--layout", "b.jsonc"], null);

        StringAssert.Contains(string.Join("; ", args.Problems), "repeated --layout");
    }

    [TestMethod]
    public void TheLayoutFlagAtTheEndWithoutAValueIsAProblem()
    {
        var args = LaunchArgs.Parse(["--layout"], null);

        StringAssert.Contains(string.Join("; ", args.Problems), "--layout needs a value");
    }

    [TestMethod]
    [DataRow("--map")]
    [DataRow("--game-view")]
    public void TheLayoutFlagBeforeAnotherFlagWithoutAValueIsAProblem(string next)
    {
        var args = LaunchArgs.Parse(["--layout", next], null);

        StringAssert.Contains(string.Join("; ", args.Problems), "--layout needs a value");
    }
}
