using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class LaunchArgsTests
{
    // The start window size of a play run, which project.godot sets under Movie Maker.
    static readonly (int Width, int Height) PlayWindow = (1280, 720);

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
        var args = LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null, movieMaker: true, PlayWindow);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual("play/zoom.play", args.Play);
    }

    [TestMethod]
    public void ThePlayFlagWithTheGameViewFlagGivesTheGameViewAndTheScriptPath()
    {
        var args = LaunchArgs.Parse(["--game-view", "--play", "play/loop.play"], null, movieMaker: true, PlayWindow);

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
            movieMaker: true,
            PlayWindow
        );

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual("1234567812345678", args.SeedText);
        Assert.AreEqual(2, args.Size!.Value);
    }

    [TestMethod]
    [DataRow(["--play", "play/zoom.play"])]
    [DataRow(["--check", "--play", "play/zoom.play"])]
    [DataRow(["--seed-info", "--seed", "1234567812345678", "--play", "play/zoom.play"])]
    public void ThePlayFlagWithoutTheMapOrGameViewFlagIsAProblemNamingBoth(string[] given)
    {
        var args = LaunchArgs.Parse(given, null, movieMaker: true, PlayWindow);

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
        var args = LaunchArgs.Parse(["--map", "--play"], null, movieMaker: true, PlayWindow);

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
        var args = LaunchArgs.Parse(["--map", flag, first, flag, second], null, movieMaker: true, PlayWindow);

        StringAssert.Contains(string.Join("; ", args.Problems), $"repeated {flag}");
    }

    [TestMethod]
    [DataRow(["--map", "--play", "play/zoom.play", "--minimized"])]
    [DataRow(["--minimized", "--layout", "layouts/carrier.jsonc", "--play", "play/zoom.play"])]
    public void TheMinimizedFlagWithThePlayFlagMinimizesThePlayRun(string[] given)
    {
        var args = LaunchArgs.Parse(given, null, movieMaker: true, PlayWindow);

        Assert.IsEmpty(args.Problems);
        Assert.IsTrue(args.Minimized);
        Assert.IsNotNull(args.Play);
    }

    [TestMethod]
    public void WithoutTheMinimizedFlagAPlayRunIsNotMinimized() =>
        Assert.IsFalse(
            LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null, movieMaker: true, PlayWindow).Minimized
        );

    [TestMethod]
    [DataRow(["--map", "--minimized"])]
    [DataRow(["--minimized"])]
    [DataRow(["--game-view", "--minimized", "--seed", "1234567812345678"])]
    public void TheMinimizedFlagWithoutThePlayFlagIsAProblem(string[] given)
    {
        var args = LaunchArgs.Parse(given, null, movieMaker: true, PlayWindow);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--minimized needs --play");
    }

    [TestMethod]
    public void ARepeatedMinimizedFlagIsAProblem()
    {
        var args = LaunchArgs.Parse(
            ["--map", "--minimized", "--play", "play/zoom.play", "--minimized"],
            null,
            true,
            PlayWindow
        );

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "repeated --minimized");
    }

    // The flag takes no value, so a path after it is no value of it, and a value flag before it lacks its value.
    [TestMethod]
    public void TheMinimizedFlagTakesNoValueAndIsNoneOfAnotherFlag()
    {
        var after = LaunchArgs.Parse(
            ["--map", "--minimized", "--play", "play/zoom.play"],
            null,
            movieMaker: true,
            PlayWindow
        );
        var before = LaunchArgs.Parse(["--map", "--play", "--minimized"], null, movieMaker: true, PlayWindow);

        Assert.AreEqual("play/zoom.play", after.Play);
        Assert.IsEmpty(after.Problems);
        Assert.IsNull(before.Play);
        CollectionAssert.Contains(before.Problems.ToList(), "--play needs a value");
    }

    [TestMethod]
    public void ThePlayFlagWithoutMovieMakerIsAProblem()
    {
        var args = LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--write-movie");
    }

    [TestMethod]
    [DataRow(1152, 648)]
    [DataRow(1280, 700)]
    [DataRow(1280, 648)]
    [DataRow(1152, 720)]
    [DataRow(0, 0)]
    [DataRow(2560, 1440)]
    [DataRow(1600, 900)]
    [DataRow(1281, 720)]
    public void APlayRunWhoseWindowStartsAtAnotherSizeIsAProblemNamingTheProjectSetting(int width, int height)
    {
        var args = LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null, movieMaker: true, (width, height));

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "window_width_override.movie");
        StringAssert.Contains(args.Problems[0], $"{width} x {height}");
    }

    [TestMethod]
    public void APlayRunWithoutAStartWindowSizeIsAProblemNamingTheProjectSetting()
    {
        var args = LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null, movieMaker: true);

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "window_width_override.movie");
    }

    [TestMethod]
    [DataRow(["--map", "--play"])]
    [DataRow(["--map", "--play", ""])]
    [DataRow(["--map", "--play", "play/a.play", "--play", "play/b.play"])]
    [DataRow(["--layout", "a.jsonc", "--play", "play/a.play"])]
    [DataRow(["--game-view", "--play", "play/a.play"])]
    [DataRow(["--check", "--play", "play/a.play"])]
    public void EveryFormOfAPlayLaunchRefusesAnotherStartWindowSize(string[] given)
    {
        var args = LaunchArgs.Parse(given, null, movieMaker: true, (1152, 648));

        Assert.IsTrue(
            args.Problems.Any(p => p.Contains("window_width_override.movie")),
            string.Join("; ", args.Problems)
        );
    }

    [TestMethod]
    public void APlayRunWithoutMovieMakerReportsTheMovieMakerProblemAlone()
    {
        var args = LaunchArgs.Parse(["--map", "--play", "play/zoom.play"], null, movieMaker: false, (1152, 648));

        Assert.HasCount(1, args.Problems);
        StringAssert.Contains(args.Problems[0], "--write-movie");
    }

    [TestMethod]
    [DataRow(["--game", "/siedler"])]
    [DataRow(["--map"])]
    [DataRow(["--game-view"])]
    [DataRow(["--check"])]
    [DataRow(["--seed-info", "--seed", "1234567812345678"])]
    [DataRow(["--layout", "a.jsonc"])]
    public void ALaunchWithoutAPlayScriptHasNoProblemAtAnyStartWindowSize(string[] given)
    {
        Assert.IsEmpty(LaunchArgs.Parse(given, null, movieMaker: true, (1152, 648)).Problems);
        Assert.IsEmpty(LaunchArgs.Parse(given, null, movieMaker: false, (0, 0)).Problems);
        Assert.IsEmpty(LaunchArgs.Parse(given, null).Problems);
    }

    // Movie Maker takes its frame size from these project settings and starts the window at it; the movie feature tag
    // keeps every launch without Movie Maker at the default window size.
    [TestMethod]
    public void TheProjectStartsAMovieMakerWindowAtThePlayWindowSize()
    {
        var lines = File.ReadAllLines(Path.Combine(PythonTool.RepoRoot(), "project.godot"));

        CollectionAssert.Contains(lines, $"window/size/window_width_override.movie={LaunchArgs.PlayWindowWidth}");
        CollectionAssert.Contains(lines, $"window/size/window_height_override.movie={LaunchArgs.PlayWindowHeight}");
        Assert.IsFalse(
            lines.Any(l => l.StartsWith("window/size/") && !l.Contains(".movie=")),
            "a window size key without the movie tag"
        );
    }

    [TestMethod]
    public void AStartWindowSizeProblemJoinsTheOtherProblemsInOneBadArgumentReport()
    {
        var args = LaunchArgs.Parse(
            ["--map", "--play", "play/zoom.play", "--bogus"],
            null,
            movieMaker: true,
            (1152, 648)
        );

        var bad = MapLaunch.BadArgument(args)!;

        Assert.HasCount(2, args.Problems);
        Assert.AreEqual(LaunchArgs.BadArgumentExitCode, bad.Code);
        StringAssert.Contains(bad.Message, "unknown flag --bogus; ");
        StringAssert.Contains(bad.Message, "window_width_override.movie");
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
        var args = LaunchArgs.Parse(
            ["--layout", "a.jsonc", "--play", "play/a.play"],
            null,
            movieMaker: true,
            PlayWindow
        );

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
        var args = LaunchArgs.Parse(["--layout", "a.jsonc", other], null);

        CollectionAssert.Contains(
            args.Problems.ToList(),
            $"--layout starts the game view, it does not go with {other}"
        );
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

    [TestMethod]
    public void WithoutTheContentSetFlagTheSetIsTheHistoryEdition()
    {
        var args = LaunchArgs.Parse(["--map"], null);

        Assert.AreEqual(ContentSetName.HistoryEdition, args.ContentSet);
    }

    [TestMethod]
    [DataRow("history-edition", ContentSetName.HistoryEdition)]
    [DataRow("basic", ContentSetName.Basic)]
    public void TheContentSetFlagNamesTheSet(string name, ContentSetName set)
    {
        var args = LaunchArgs.Parse(["--map", "--content-set", name], null);

        Assert.AreEqual(set, args.ContentSet);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    [DataRow(new[] { "--map", "--content-set", "retro" }, "bad --content-set 'retro'")]
    [DataRow(new[] { "--map", "--content-set" }, "--content-set needs a value")]
    [DataRow(new[] { "--content-set", "--map" }, "--content-set needs a value")]
    [DataRow(new[] { "--map", "--content-set", "basic", "--content-set", "basic" }, "repeated --content-set")]
    [DataRow(new[] { "--content-set", "basic" }, "--content-set does not apply to the asset browser")]
    [DataRow(new[] { "--content-set", "history-edition" }, "--content-set does not apply to the asset browser")]
    [DataRow(new[] { "--check", "--content-set", "basic" }, "--content-set does not apply to --check")]
    [DataRow(new[] { "--check", "--content-set", "history-edition" }, "--content-set does not apply to --check")]
    [DataRow(
        new[] { "--seed-info", "--seed", "1234567812345678", "--content-set", "basic" },
        "--content-set does not apply to --seed-info"
    )]
    public void ABadContentSetArgumentIsAProblemThatNamesTheKnownSets(string[] flags, string reason)
    {
        var args = LaunchArgs.Parse(flags, null);

        var problems = string.Join("; ", args.Problems);
        StringAssert.Contains(problems, reason);
        StringAssert.Contains(problems, "basic, history-edition");
    }

    [TestMethod]
    public void TheGraphicsFlagIsRefusedWithAMessageThatNamesTheContentSetFlag()
    {
        var args = LaunchArgs.Parse(["--map", "--graphics", "basic"], null);

        StringAssert.Contains(string.Join("; ", args.Problems), "--graphics is gone, use --content-set");
    }

    [TestMethod]
    [DataRow("--game-view")]
    [DataRow("--map")]
    public void TheContentSetFlagGoesWithTheMapViewAndTheGameView(string mode)
    {
        var args = LaunchArgs.Parse([mode, "--content-set", "basic"], null);

        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    public void TheContentSetFlagGoesWithALayout()
    {
        var args = LaunchArgs.Parse(["--layout", "a.jsonc", "--content-set", "basic"], null);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual(LaunchMode.GameView, args.Mode);
    }

    // Sweep #244 F90-F93. The rule is the one LaunchArgs documents: an empty --game value is no value, a repeated --game
    // keeps the first, a stray argument is ignored, a size is digits only.
    [TestMethod]
    [DataRow(null)]
    [DataRow("/env/install")]
    public void AnEmptyGameValueIsOneProblemAndNoInstallWhateverTheEnvironmentHolds(string? env)
    {
        var args = LaunchArgs.Parse(["--game", ""], env);

        CollectionAssert.AreEqual(new[] { "--game needs a value" }, args.Problems.ToArray());
        Assert.IsNull(args.Game);
    }

    [TestMethod]
    [DataRow("--play")]
    [DataRow("--layout")]
    public void AnEmptyPlayOrLayoutValueIsOneProblemLikeAnEmptyGame(string flag)
    {
        var args = LaunchArgs.Parse(["--map", flag, ""], null, movieMaker: true, PlayWindow);

        Assert.IsTrue(args.Problems.Contains($"{flag} needs a value"));
        Assert.IsNull(flag == "--play" ? args.Play : args.Layout);
    }

    [TestMethod]
    public void ARepeatedGameFlagKeepsTheFirstAndIsNoProblem()
    {
        var args = LaunchArgs.Parse(["--game", "a", "--game", "b"], "/env/install");

        Assert.AreEqual("a", args.Game);
        Assert.IsEmpty(args.Problems);
    }

    [TestMethod]
    public void AnEmptyFirstGameFlagIsKeptAndTheSecondIsIgnored()
    {
        var args = LaunchArgs.Parse(["--game", "", "--game", "x"], "/env/install");

        CollectionAssert.AreEqual(new[] { "--game needs a value" }, args.Problems.ToArray());
        Assert.IsNull(args.Game);
    }

    [TestMethod]
    public void AStrayPositionalArgumentIsIgnored()
    {
        var args = LaunchArgs.Parse(["stray", "--map", "other"], null);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual(LaunchMode.Map, args.Mode);
    }

    [TestMethod]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("1 ")]
    [DataRow("-1")]
    public void ASignedOrSpacedSizeIsOneProblemNamingIt(string size)
    {
        var args = LaunchArgs.Parse(["--map", "--size", size], null);

        CollectionAssert.AreEqual(
            new[] { $"bad --size '{size}': not a size number, use 1, 2 or 3" },
            args.Problems.ToArray()
        );
        Assert.IsNull(args.Size);
    }

    // The free-game screen's bar sets the human's start stock level from 0 to 40 (docs/rules/castle.md); --stock stands in for it.
    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("40", 40)]
    public void AStockLevelFrom0To40IsTakenByTheGameView(string value, int level)
    {
        var args = LaunchArgs.Parse(["--game-view", "--stock", value], null);

        Assert.IsEmpty(args.Problems);
        Assert.AreEqual(level, args.Stock);
    }

    [TestMethod]
    public void NoStockFlagLeavesTheLevelToTheSeed()
    {
        Assert.IsNull(LaunchArgs.Parse(["--game-view"], null).Stock);
    }

    [TestMethod]
    [DataRow("41")]
    [DataRow("-1")]
    [DataRow("ten")]
    public void AStockOutside0To40IsOneProblemNamingIt(string value)
    {
        var args = LaunchArgs.Parse(["--game-view", "--stock", value], null);

        CollectionAssert.AreEqual(
            new[] { $"bad --stock '{value}': not a start stock level, use 0 to 40" },
            args.Problems.ToArray()
        );
        Assert.IsNull(args.Stock);
    }

    [TestMethod]
    [DataRow("--game-view --stock")]
    [DataRow("--stock --game-view")]
    public void AStockWithoutAValueIsOneProblem(string arguments)
    {
        var args = LaunchArgs.Parse(arguments.Split(' '), null);

        CollectionAssert.AreEqual(new[] { "--stock needs a value" }, args.Problems.ToArray());
        Assert.AreEqual(LaunchMode.GameView, args.Mode);
    }

    [TestMethod]
    [DataRow("--map")]
    [DataRow("--check")]
    [DataRow("--layout x.layout")]
    [DataRow("--seed-info --seed 1234567812345678")]
    [DataRow("")]
    public void AStockOutsideTheGameViewIsAProblem(string mode)
    {
        var args = LaunchArgs.Parse([.. mode.Split(' ', StringSplitOptions.RemoveEmptyEntries), "--stock", "10"], null);

        Assert.IsTrue(args.Problems.Contains("--stock applies to --game-view without --layout only"));
    }

    [TestMethod]
    public void AStockWithTwoModeFlagsGivesOnlyTheConflict()
    {
        var args = LaunchArgs.Parse(["--game-view", "--map", "--stock", "10"], null);

        CollectionAssert.AreEqual(
            new[] { "--game-view and --map are different modes, give only one" },
            args.Problems.ToArray()
        );
    }

    [TestMethod]
    public void ARepeatedStockIsAProblem()
    {
        var args = LaunchArgs.Parse(["--game-view", "--stock", "10", "--stock", "20"], null);

        CollectionAssert.AreEqual(new[] { "repeated --stock" }, args.Problems.ToArray());
    }
}
