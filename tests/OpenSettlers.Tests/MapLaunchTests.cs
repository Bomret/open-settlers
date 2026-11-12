using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class MapLaunchTests
{
    const string Project = "project";

    static LaunchArgs Args(params string[] args) => LaunchArgs.Parse(args, null, movieMaker: true);

    static (int Code, Layout? Layout, string? Message) LoadsFine(string path) =>
        LayoutLoad.Run(path, out var layout, out var message) is var code && code == 0
            ? (0, layout, null)
            : (code, null, message);

    static (int Code, Layout? Layout, string? Message) LoadsNothing(string path) => (0, null, null);

    static (int Code, Layout? Layout, string? Message) Fails(string path) => (4, null, "ERROR bad layout");

    static (Install? Install, string? Problem) Opens(LaunchArgs args) =>
        Install.TryOpen(args, out var install, out var problem) ? (install, null) : (null, problem);

    static PlayScript Script(string path) => PlayScript.Parse("wait 1", path);

    static PlayScript BadScript(string path) => PlayScript.Parse("fly 3\nfly 4", path);

    static MapLaunch Decide(
        LaunchArgs args,
        Func<string, (int Code, Layout? Layout, string? Message)>? layout = null,
        Func<string, PlayScript>? script = null
    ) => MapLaunch.Decide(args, layout ?? LoadsNothing, Opens, script ?? Script, () => Project);

    [TestMethod]
    public void ABadArgumentExitsFourOnStderr()
    {
        var outcome = Decide(Args("--map", "--bogus"));

        Assert.AreEqual(new MapLaunch.Exit(4, "ERROR unknown flag --bogus", LaunchStream.Stderr), outcome);
    }

    [TestMethod]
    public void ALayoutFileThatFailsToLoadExitsFourOnStdoutWithItsOwnMessage()
    {
        using var fake = new FakeInstall();

        var outcome = Decide(Args("--layout", "l.jsonc", "--game", fake.Root), layout: Fails);

        Assert.AreEqual(new MapLaunch.Exit(4, "ERROR bad layout", LaunchStream.Stdout), outcome);
    }

    [TestMethod]
    public void AMissingInstallExitsTwoOnStdout()
    {
        var outcome = Decide(Args("--map"));

        var exit = (MapLaunch.Exit)outcome;
        Assert.AreEqual(2, exit.Code);
        Assert.AreEqual(LaunchStream.Stdout, exit.Stream);
        StringAssert.StartsWith(exit.Message, "ERROR no install given: ");
    }

    [TestMethod]
    public void ABadPlayScriptExitsOneOnStderrWithItsProblemsJoined()
    {
        using var fake = new FakeInstall();
        var problems = BadScript(Path.Combine(Project, "x.play")).Problems;

        var outcome = Decide(Args("--map", "--play", "x.play", "--game", fake.Root), script: BadScript);

        Assert.HasCount(2, problems);
        Assert.AreEqual(new MapLaunch.Exit(1, $"ERROR {string.Join("; ", problems)}", LaunchStream.Stderr), outcome);
    }

    [TestMethod]
    public void ABadLayoutWithAMissingInstallExitsFour()
    {
        var outcome = Decide(Args("--layout", "l.jsonc"), layout: Fails);

        Assert.AreEqual(4, ((MapLaunch.Exit)outcome).Code);
    }

    [TestMethod]
    public void AMissingInstallWithABadScriptExitsTwo()
    {
        var outcome = Decide(Args("--map", "--play", "x.play"), script: BadScript);

        Assert.AreEqual(2, ((MapLaunch.Exit)outcome).Code);
    }

    [TestMethod]
    public void ABadArgumentWithABadLayoutExitsFour()
    {
        var outcome = Decide(Args("--layout", "l.jsonc", "--bogus"), layout: Fails);

        Assert.AreEqual(4, ((MapLaunch.Exit)outcome).Code);
        Assert.AreEqual("ERROR unknown flag --bogus", ((MapLaunch.Exit)outcome).Message);
    }

    [TestMethod]
    public void EveryCheckPassingOpensTheViewWithPlayOutAndTheStem()
    {
        using var fake = new FakeInstall();

        var outcome = Decide(Args("--map", "--play", "play/walk.play", "--game", fake.Root));

        var open = (MapLaunch.Open)outcome;
        Assert.AreEqual(Path.Combine(Project, "play-out", "walk"), open.PlayOutFolder);
        Assert.AreEqual(Path.Combine(Project, "play/walk.play"), open.Script!.Path);
        Assert.IsNull(open.Layout);
    }

    [TestMethod]
    public void WithoutAPlayScriptTheViewOpensWithNoScriptAndNoFolder()
    {
        using var fake = new FakeInstall();

        var open = (MapLaunch.Open)Decide(Args("--game-view", "--game", fake.Root));

        Assert.IsNull(open.Script);
        Assert.IsNull(open.PlayOutFolder);
    }

    [TestMethod]
    public void ALoadedLayoutComesBackInTheOpenedView()
    {
        using var fake = new FakeInstall();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jsonc");
        File.WriteAllText(path, LayoutTests.Text());
        try
        {
            var open = (MapLaunch.Open)Decide(Args("--layout", path, "--game", fake.Root), layout: LoadsFine);

            Assert.IsNotNull(open.Layout);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void TheCrashTextNamesTheSelectedAssetFileThenTheExceptionTypeAndMessage()
    {
        var text = CrashText.Of("a/b.dat", new InvalidOperationException("boom"));

        Assert.AreEqual("a/b.dat: crashed: InvalidOperationException: boom", text);
    }

    sealed class OneKind(string name, params Asset[] assets) : AssetKind
    {
        public override string Name => name;

        public override IEnumerable<Asset> Enumerate(Install install) => assets;
    }

    [TestMethod]
    public void TheCrashSubjectIsTheSelectedAssetFile()
    {
        using var fake = new FakeInstall();
        var asset = new Asset
        {
            Kind = "text",
            File = "x/a",
            Name = "a",
            Load = () => throw new InvalidOperationException("unused"),
        };
        var state = AssetBrowserState.Open(
            LaunchArgs.Parse(["--game", fake.Root], null),
            [new OneKind("text", asset)],
            selectFirst: false
        );
        state.SelectKind("text");

        Assert.AreEqual("x/a", CrashText.Subject(state));
    }

    [TestMethod]
    public void WithNoSelectedAssetTheCrashSubjectIsTheSelectedKind()
    {
        using var fake = new FakeInstall();
        var state = AssetBrowserState.Open(
            LaunchArgs.Parse(["--game", fake.Root], null),
            [new OneKind("empty")],
            selectFirst: false
        );
        state.SelectKind("empty");

        Assert.AreEqual("empty", CrashText.Subject(state));
    }

    [TestMethod]
    public void WithNeitherSelectedTheCrashSubjectIsNull()
    {
        using var fake = new FakeInstall();
        var state = AssetBrowserState.Open(
            LaunchArgs.Parse(["--game", fake.Root], null),
            [new OneKind("empty")],
            selectFirst: false
        );

        Assert.IsNull(CrashText.Subject(state));
    }

    [TestMethod]
    public void AGuardedCrashLogsAndReturnsTheTextOfTheSubjectReadAfterTheCrash()
    {
        var subject = "kind";
        string? logged = null;

        var text = CrashText.Guard(
            () =>
            {
                subject = "file.dat";
                throw new ArgumentException("bad");
            },
            () => subject,
            line => logged = line
        );

        Assert.AreEqual("file.dat: crashed: ArgumentException: bad", text);
        StringAssert.StartsWith(logged, "asset browser crashed: System.ArgumentException: bad");
    }

    [TestMethod]
    public void AGuardedActionThatCompletesReturnsNullAndLogsNothing()
    {
        string? logged = null;

        var text = CrashText.Guard(() => { }, () => "k", line => logged = line);

        Assert.IsNull(text);
        Assert.IsNull(logged);
    }
}
