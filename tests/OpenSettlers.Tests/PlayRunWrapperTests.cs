using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// tools/play-run.sh runs in a throwaway git repository against a fake mise (PLAY_RUN_MISE), a fake Godot
// (PLAY_RUN_GODOT) and a fake dotnet (PLAY_RUN_DOTNET). The fake mise logs a call and runs the command after
// `exec --`. The fake Godot counts its calls, writes a dump, two named shots, numbered frames, a WAV and log
// lines, and exits with FAKE_STATUS after FAKE_SLEEP seconds. The fake dotnet exits with FAKE_BUILD_STATUS.
// Both append their name and arguments to the order file. tools/play-run.sh is a POSIX shell script.
[TestClass]
[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
public class PlayRunWrapperTests
{
    const string Siedler = "/games/The Settlers";

    const string FakeMise = """
        #!/bin/sh
        echo mise >> "$FAKE_DIR/mise.log"
        [ "$1" = exec ] && shift
        [ "$1" = -- ] && shift
        exec "$@"
        """;

    const string FakeGodot = """
        #!/bin/sh
        if [ "$1" = --version ]; then
          [ "${FAKE_VERSION_FAIL:-}" = 1 ] && exit 1
          echo "${FAKE_VERSION:-4.7.2.stable.mono}"
          exit 0
        fi
        echo call >> "$FAKE_DIR/calls"
        echo godot >> "$FAKE_DIR/order"
        printf '%s\n' "$@" > "$FAKE_DIR/args"
        prev=
        for a; do
          [ "$prev" = --write-movie ] && out=$(dirname "$a")
          prev=$a
        done
        sleep "${FAKE_SLEEP:-0}"
        echo "state ${FAKE_MARK:-1}" > "$out/end.txt"
        echo shot > "$out/start.png"
        echo shot > "$out/42.png"
        echo frame > "$out/frame00000000.png"
        echo frame > "$out/frame00000001.png"
        echo wav > "$out/frame.wav"
        echo "WARN: a warning"
        echo "plain output"
        echo "ERROR: an error" >&2
        exit "${FAKE_STATUS:-0}"
        """;

    const string FakeDotnet = """
        #!/bin/sh
        echo "dotnet $*" >> "$FAKE_DIR/order"
        echo "build output"
        exit "${FAKE_BUILD_STATUS:-0}"
        """;

    string root = "";
    string repo = "";
    string fake = "";

    static void Executable(string path, string text)
    {
        File.WriteAllText(path, text);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [TestInitialize]
    public void Init()
    {
        root = Path.Combine(Path.GetTempPath(), "play-run-wrapper-" + Guid.NewGuid().ToString("N"));
        repo = Path.Combine(root, "repo");
        fake = Path.Combine(root, "fake");
        Directory.CreateDirectory(fake);
        Executable(Path.Combine(fake, "mise"), FakeMise);
        Executable(Path.Combine(fake, "godot"), FakeGodot);
        Executable(Path.Combine(fake, "dotnet"), FakeDotnet);

        Write("tools/play-run.sh", File.ReadAllText(Path.Combine(RepoRoot(), "tools", "play-run.sh")));
        Write("src/Game.cs", "class Game {}");
        Write("scenes/main.tscn", "[scene]");
        Write("project.godot", "config");
        Write("OpenSettlers.csproj", "<Project />");
        Write("global.json", "{}");
        Write("play/demo.play", "wait 1");
        Write("play/.cache.play", "wait 1");
        Write("README.md", "readme");
        Write("tests/T.cs", "class T {}");
        Write(".gitignore", "play-out/\nsrc/ignored.txt\n");
        Write("src/ignored.txt", "ignored");
        Git("init", "-q");
        Git("add", "-A");
        Git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "init");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, true);

    static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "play-run.sh")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("tools/play-run.sh not found");
        }

        return dir;
    }

    void Write(string path, string text)
    {
        var full = Path.Combine(repo, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    string Out(string path) => Path.Combine(repo, "play-out", path);

    int Calls => File.Exists(Path.Combine(fake, "calls")) ? File.ReadAllLines(Path.Combine(fake, "calls")).Length : 0;

    string[] Order => File.Exists(Path.Combine(fake, "order")) ? File.ReadAllLines(Path.Combine(fake, "order")) : [];

    string[] LogLines => File.Exists(Out("runs.log")) ? File.ReadAllLines(Out("runs.log")) : [];

    static readonly string[] Args = ["demo", "--seed", "abc", "--size", "2", "--fps", "10"];

    void Git(params string[] args) => Exec("git", args, new Dictionary<string, string?>());

    (int Status, string Stdout, string Stderr) Exec(string file, string[] args, Dictionary<string, string?> env)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env)
        {
            if (value is null)
            {
                info.Environment.Remove(key);
            }
            else
            {
                info.Environment[key] = value;
            }
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(true);
            Assert.Fail($"{file} did not exit within 30 s");
        }

        return (process.ExitCode, stdout.Result, stderr.Result);
    }

    (int Status, string Stdout, string Stderr) Run(string[] args, Dictionary<string, string?>? env = null)
    {
        var all = new Dictionary<string, string?>
        {
            ["PLAY_RUN_MISE"] = Path.Combine(fake, "mise"),
            ["PLAY_RUN_GODOT"] = Path.Combine(fake, "godot"),
            ["PLAY_RUN_DOTNET"] = Path.Combine(fake, "dotnet"),
            ["SIEDLER_DIR"] = Siedler,
            ["FAKE_DIR"] = fake,
        };
        foreach (var (key, value) in env ?? [])
        {
            all[key] = value;
        }

        return Exec("sh", ["tools/play-run.sh", .. args], all);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void SecondCallOnUnchangedInputsReusesTheStoredResult(int status)
    {
        var env = new Dictionary<string, string?> { ["FAKE_STATUS"] = status.ToString() };
        Assert.AreEqual(status, Run(Args, env).Status);
        File.WriteAllText(Out("demo/stray.txt"), "stray");
        File.Delete(Out("demo/end.txt"));

        var (second, stdout, _) = Run(Args, env);

        Assert.AreEqual(1, Calls);
        Assert.AreEqual(status, second);
        StringAssert.Contains(stdout, "reused");
        StringAssert.Matches(stdout, new Regex($@"reused \S+ exit {status}\b"));
        StringAssert.Contains(stdout, "WARN: a warning");
        StringAssert.Contains(stdout, "ERROR: an error");
        Assert.IsFalse(stdout.Contains("plain output"));
        Assert.AreEqual("state 1\n", File.ReadAllText(Out("demo/end.txt")));
        Assert.IsTrue(File.Exists(Out("demo/start.png")));
        Assert.IsTrue(File.Exists(Out("demo/frame.wav")));
        Assert.IsFalse(File.Exists(Out("demo/stray.txt")));
    }

    [TestMethod]
    [DataRow("src")]
    [DataRow("untracked-scenes")]
    [DataRow("deleted")]
    [DataRow("renamed")]
    [DataRow("project")]
    [DataRow("script")]
    [DataRow("seed")]
    [DataRow("size")]
    [DataRow("no-seed")]
    [DataRow("fps")]
    [DataRow("version")]
    [DataRow("siedler")]
    public void AChangedHashedInputStartsGodotAgain(string input)
    {
        Run(Args);
        var second = Args;
        var env = new Dictionary<string, string?>();
        switch (input)
        {
            case "src":
                Write("src/Game.cs", "class Game { int x; }");
                break;
            case "untracked-scenes":
                Write("scenes/new.tscn", "[scene]");
                break;
            case "deleted":
                File.Delete(Path.Combine(repo, "scenes/main.tscn"));
                break;
            case "renamed":
                File.Move(Path.Combine(repo, "src/Game.cs"), Path.Combine(repo, "src/Game2.cs"));
                break;
            case "project":
                Write("project.godot", "config 2");
                break;
            case "script":
                Write("play/demo.play", "wait 2");
                break;
            case "seed":
                second = ["demo", "--seed", "xyz", "--size", "2", "--fps", "10"];
                break;
            case "size":
                second = ["demo", "--seed", "abc", "--size", "3", "--fps", "10"];
                break;
            case "no-seed":
                second = ["demo", "--size", "2", "--fps", "10"];
                break;
            case "fps":
                second = ["demo", "--seed", "abc", "--size", "2", "--fps", "30"];
                break;
            case "version":
                env["FAKE_VERSION"] = "4.8.stable.mono";
                break;
            case "siedler":
                env["SIEDLER_DIR"] = "/games/other";
                break;
        }

        Run(second, env);

        Assert.AreEqual(2, Calls);
    }

    [TestMethod]
    [DataRow("README.md")]
    [DataRow("tests/T.cs")]
    [DataRow("src/ignored.txt")]
    public void AnEditOutsideTheHashedInputsReuses(string path)
    {
        Run(Args);
        Write(path, "edited");

        Run(Args);

        Assert.AreEqual(1, Calls);
    }

    [TestMethod]
    public void FreshStartsGodotOnAHitAndReplacesTheEntry()
    {
        Run(Args);

        Run([.. Args, "--fresh"], new Dictionary<string, string?> { ["FAKE_MARK"] = "2" });
        File.Delete(Out("demo/end.txt"));
        Run(Args);

        Assert.AreEqual(2, Calls);
        Assert.AreEqual("state 2\n", File.ReadAllText(Out("demo/end.txt")));
    }

    [TestMethod]
    public void AnExitOtherThanZeroOrOneStoresNothing()
    {
        var env = new Dictionary<string, string?> { ["FAKE_STATUS"] = "4" };

        Assert.AreEqual(4, Run(Args, env).Status);
        Assert.IsFalse(Directory.Exists(Out(".cache")) && Directory.EnumerateFileSystemEntries(Out(".cache")).Any());
        Run(Args, env);

        Assert.AreEqual(2, Calls);
    }

    [TestMethod]
    public void ARunBuildsTheGameBeforeGodotThroughMiseAndAReuseDoesNot()
    {
        Run(Args);
        var miseCalls = File.ReadAllLines(Path.Combine(fake, "mise.log")).Length;
        Run(Args);

        CollectionAssert.AreEqual(new[] { "dotnet build OpenSettlers.csproj", "godot" }, Order);
        Assert.IsTrue(miseCalls >= 2);
    }

    [TestMethod]
    public void GodotGetsTheMapViewLaunchLine()
    {
        Run(Args);

        var args = File.ReadAllLines(Path.Combine(fake, "args"));
        var movie = Array.IndexOf(args, "--write-movie");
        Assert.AreEqual("play-out/demo/frame.png", args[movie + 1]);
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--map", "--seed", "abc", "--size", "2" },
            args[(movie + 2)..^2]
        );
        CollectionAssert.AreEqual(new[] { "--play", "play/demo.play" }, args[^2..]);
    }

    [TestMethod]
    public void GameViewGetsTheGameViewLaunchLineWithSeedAndSize()
    {
        Run([.. Args, "--game-view"]);

        var args = File.ReadAllLines(Path.Combine(fake, "args"));
        var movie = Array.IndexOf(args, "--write-movie");
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--game-view", "--seed", "abc", "--size", "2" },
            args[(movie + 2)..^2]
        );
    }

    [TestMethod]
    public void ALayoutGetsTheGameViewOnTheFileWithoutSeedAndSize()
    {
        Write("play/a.layout", "layout");

        Run(["demo", "--layout", "play/a.layout", "--fps", "10"]);

        var args = File.ReadAllLines(Path.Combine(fake, "args"));
        var movie = Array.IndexOf(args, "--write-movie");
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--game-view", "--layout", "play/a.layout" },
            args[(movie + 2)..^2]
        );
    }

    [TestMethod]
    public void AGameViewFlagWithALayoutPassesTheGameViewFlagOnceAndNoSeedOrSize()
    {
        Write("play/a.layout", "layout");

        Run(["demo", "--game-view", "--layout", "play/a.layout", "--fps", "10"]);

        var args = File.ReadAllLines(Path.Combine(fake, "args"));
        var movie = Array.IndexOf(args, "--write-movie");
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--game-view", "--layout", "play/a.layout" },
            args[(movie + 2)..^2]
        );
    }

    [TestMethod]
    public void ALayoutByteChangeStartsGodotAgainAndTheSameLayoutReuses()
    {
        string[] layout = ["demo", "--layout", "play/a.layout", "--fps", "10"];
        Write("play/a.layout", "layout");
        Run(layout);
        Run(layout);
        Assert.AreEqual(1, Calls);

        Write("play/a.layout", "layouu");
        Run(layout);

        Assert.AreEqual(2, Calls);
    }

    [TestMethod]
    public void TheModeAndTheLayoutPathChangeTheHash()
    {
        Write("play/a.layout", "layout");
        Write("play/b.layout", "layout");
        Run(["demo", "--layout", "play/a.layout", "--fps", "10"]);
        Run(["demo", "--layout", "play/b.layout", "--fps", "10"]);
        Run(["demo", "--fps", "10"]);
        Run(["demo", "--game-view", "--fps", "10"]);

        Assert.AreEqual(4, Calls);
    }

    [TestMethod]
    public void TheLogLineNamesTheModeAndTheLayout()
    {
        Write("play/a.layout", "layout");

        Run(["demo", "--layout", "play/a.layout", "--fps", "10"]);
        Run(["demo", "--game-view", "--fps", "10"]);

        var lines = LogLines.Select(l => l.Split('\t')).ToArray();
        CollectionAssert.AreEqual(new[] { "mode=game-view", "layout=play/a.layout" }, lines[0][5..7]);
        CollectionAssert.AreEqual(new[] { "mode=game-view", "layout=" }, lines[1][5..7]);
    }

    [TestMethod]
    public void WithoutSeedAndSizeTheLaunchLineOmitsThem()
    {
        Run(["demo", "--fps", "10"]);

        var args = File.ReadAllLines(Path.Combine(fake, "args"));
        CollectionAssert.DoesNotContain(args, "--seed");
        CollectionAssert.DoesNotContain(args, "--size");
    }

    [TestMethod]
    public void AFailedBuildStartsNoGodotStoresNothingAndExitsThree()
    {
        var env = new Dictionary<string, string?> { ["FAKE_BUILD_STATUS"] = "1" };

        var (status, stdout, _) = Run(Args, env);

        Assert.AreEqual(3, status);
        StringAssert.Contains(stdout, "build output");
        Assert.AreEqual(0, Calls);
        Assert.IsFalse(Directory.Exists(Out(".cache")) && Directory.EnumerateFileSystemEntries(Out(".cache")).Any());
        CollectionAssert.AreEqual(new[] { "exit=3", "build" }, LogLines.Single().Split('\t')[10..]);
    }

    [TestMethod]
    public void NumberedFramesAreNotStoredAndAnAllDigitShotIs()
    {
        Run(Args);
        var entry = Directory.GetDirectories(Out(".cache")).Single();
        var stored = Directory.GetFiles(entry, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();

        CollectionAssert.Contains(stored, "42.png");
        CollectionAssert.DoesNotContain(stored, "frame00000000.png");
        CollectionAssert.DoesNotContain(stored, "frame00000001.png");
    }

    [TestMethod]
    public void EachCallAppendsOneLogLineWithEveryField()
    {
        Run(Args);
        Write("notes.txt", "untracked");
        Run(Args);
        Run(["demo", "--fps", "10"]);

        var lines = LogLines;
        Assert.AreEqual(3, lines.Length);
        using var git = Process.Start(
            new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = repo, RedirectStandardOutput = true }
        )!;
        var head = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();

        var first = lines[0].Split('\t');
        Assert.AreEqual(12, first.Length, lines[0]);
        StringAssert.Matches(first[0], new System.Text.RegularExpressions.Regex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$"));
        StringAssert.Matches(first[1], new System.Text.RegularExpressions.Regex("^[0-9a-f]{40}$"));
        CollectionAssert.AreEqual(
            new[] { head, "clean", "demo", "mode=map", "layout=", "seed=abc", "size=2", "fps=10", "exit=0", "run" },
            first[2..]
        );
        var second = lines[1].Split('\t');
        Assert.AreEqual(first[1], second[1]);
        CollectionAssert.AreEqual(new[] { "dirty", "reused" }, new[] { second[3], second[11] });
        CollectionAssert.AreEqual(new[] { "seed=", "size=" }, lines[2].Split('\t')[7..9]);
    }

    [TestMethod]
    [DataRow("no fps", "demo", "--seed", "abc")]
    [DataRow("no value", "demo", "--seed")]
    [DataRow("zero fps", "demo", "--fps", "0")]
    [DataRow("negative fps", "demo", "--fps", "-5")]
    [DataRow("text fps", "demo", "--fps", "ten")]
    [DataRow("zero size", "demo", "--size", "0", "--fps", "10")]
    [DataRow("text size", "demo", "--size", "big", "--fps", "10")]
    [DataRow("slash stem", "play/demo", "--fps", "10")]
    [DataRow("dots stem", "..", "--fps", "10")]
    [DataRow("parent stem", "../x", "--fps", "10")]
    [DataRow("hidden stem", ".cache", "--fps", "10")]
    [DataRow("missing script", "nothing", "--fps", "10")]
    [DataRow("unknown flag", "demo", "--fps", "10", "--fast")]
    [DataRow("missing layout", "demo", "--layout", "play/none.layout", "--fps", "10")]
    [DataRow("layout with no value", "demo", "--fps", "10", "--layout")]
    [DataRow("empty layout", "demo", "--layout", "", "--fps", "10")]
    [DataRow("no args")]
    public void BadArgumentsExitTwoAndLeaveNoTrace(string reason, params string[] args) =>
        AssertUsageError(Run(args), reason);

    [TestMethod]
    [DataRow("layout with seed", "--layout", "play/a.layout", "--seed", "abc")]
    [DataRow("layout with size", "--layout", "play/a.layout", "--size", "2")]
    [DataRow("seed before layout", "--seed", "abc", "--layout", "play/a.layout")]
    public void ALayoutWithSeedOrSizeExitsTwoAndLeavesNoTrace(string reason, params string[] args)
    {
        Write("play/a.layout", "layout");
        var result = Run(["demo", .. args, "--fps", "10"]);

        AssertUsageError(result, reason);
        StringAssert.Contains(result.Stderr, "own seed and size", reason);
    }

    [TestMethod]
    public void AnUnsetSiedlerDirExitsTwo() =>
        AssertUsageError(Run(Args, new Dictionary<string, string?> { ["SIEDLER_DIR"] = null }), "unset");

    [TestMethod]
    public void AFailingVersionReadExitsTwo() =>
        AssertUsageError(Run(Args, new Dictionary<string, string?> { ["FAKE_VERSION_FAIL"] = "1" }), "version");

    void AssertUsageError((int Status, string Stdout, string Stderr) result, string reason)
    {
        Assert.AreEqual(2, result.Status, reason);
        Assert.AreNotEqual("", result.Stderr.Trim(), reason);
        Assert.AreEqual(0, Calls, reason);
        Assert.IsFalse(Directory.Exists(Out(".cache")), reason);
        Assert.AreEqual(0, LogLines.Length, reason);
    }

    [TestMethod]
    public void ASiedlerDirWithSpacesReachesGodotAsOneArgument()
    {
        Run(Args);

        CollectionAssert.Contains(File.ReadAllLines(Path.Combine(fake, "args")), Siedler);
    }

    [TestMethod]
    public void TwoConcurrentCallsForOneStemRunGodotOnceAndStoreOneEntry()
    {
        var env = new Dictionary<string, string?> { ["FAKE_SLEEP"] = "2" };
        var calls = new[] { Task.Run(() => Run(Args, env)), Task.Run(() => Run(Args, env)) };
        Task.WaitAll(calls);

        CollectionAssert.AreEqual(new[] { 0, 0 }, calls.Select(c => c.Result.Status).ToArray());
        Assert.AreEqual(1, Calls);
        var entry = Directory.GetDirectories(Out(".cache")).Single();
        CollectionAssert.AreEqual(new[] { "out" }, Directory.GetDirectories(entry).Select(Path.GetFileName).ToArray());
        Assert.AreEqual("state 1\n", File.ReadAllText(Out("demo/end.txt")));
    }

    [TestMethod]
    public void CallsOfDifferentStemsRunInParallel()
    {
        Write("play/other.play", "wait 1");
        var env = new Dictionary<string, string?> { ["FAKE_SLEEP"] = "3" };
        var watch = Stopwatch.StartNew();
        var calls = new[] { Task.Run(() => Run(Args, env)), Task.Run(() => Run(["other", "--fps", "10"], env)) };
        Task.WaitAll(calls);

        Assert.AreEqual(2, Calls);
        Assert.IsTrue(watch.Elapsed.TotalSeconds < 5.5, $"the calls ran one after the other ({watch.Elapsed})");
    }

    void HoldLock(int pid)
    {
        Directory.CreateDirectory(Out(".lock-demo"));
        File.WriteAllText(Out(".lock-demo/pid"), pid + "\n");
    }

    [TestMethod]
    public void ALockOfADeadProcessIsTakenOver()
    {
        using var gone = Process.Start("true")!;
        gone.WaitForExit();
        HoldLock(gone.Id);

        Assert.AreEqual(0, Run(Args).Status);
        Assert.AreEqual(1, Calls);
    }

    [TestMethod]
    public void ALockWithNoPidOlderThanTheGraceIsTakenOver()
    {
        Directory.CreateDirectory(Out(".lock-demo"));
        Directory.SetLastWriteTimeUtc(Out(".lock-demo"), DateTime.UtcNow.AddMinutes(-5));

        Assert.AreEqual(0, Run(Args).Status);
        Assert.AreEqual(1, Calls);
    }

    [TestMethod]
    public void ACallWaitingOnALiveLockSaysSoAndRunsOnceItIsFree()
    {
        HoldLock(Environment.ProcessId);
        var call = Task.Run(() => Run(Args));
        Thread.Sleep(1500);
        Directory.Delete(Out(".lock-demo"), true);

        Assert.AreEqual(0, call.Result.Status);
        StringAssert.Contains(
            call.Result.Stderr,
            "play-run.sh: waiting on play-out/.lock-demo (remove it if no run is live)"
        );
    }

    [TestMethod]
    public void TheHashIsTakenAfterTheLock()
    {
        Run(Args);
        HoldLock(Environment.ProcessId);
        var call = Task.Run(() => Run(Args));
        Thread.Sleep(1500);
        Write("src/Game.cs", "class Game { int changed; }");
        Directory.Delete(Out(".lock-demo"), true);
        call.Wait();

        Assert.AreEqual(2, Calls);
    }
}
