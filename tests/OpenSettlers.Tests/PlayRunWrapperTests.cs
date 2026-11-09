using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// tools/play-run.sh runs in a throwaway git repository against a fake mise (PLAY_RUN_MISE), a fake Godot
// (PLAY_RUN_GODOT) and a fake dotnet (PLAY_RUN_DOTNET). The fake mise logs a call and runs the command after
// `exec --`. The fake Godot counts its calls, waits until the FAKE_GATE file exists (when set), writes a dump,
// two named shots, numbered frames, a WAV and log lines, and exits with FAKE_STATUS. The fake dotnet exits with
// FAKE_BUILD_STATUS. Both append their name and arguments to the order file. A test that needs two calls to
// overlap holds the gate shut and polls for the effect it waits on, so it depends on no sleep and no clock.
// The git fixture repository is built once per class and copied for each test. tools/play-run.sh is a POSIX
// shell script.
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
        i=0
        while [ -n "${FAKE_GATE:-}" ] && [ ! -e "$FAKE_GATE" ] && [ $i -lt 600 ]; do
          sleep 0.05
          i=$((i + 1))
        done
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

    static string templateRoot = "";

    string root = "";
    string repo = "";
    string fake = "";

    [UnsupportedOSPlatform("windows")]
    static void Executable(string path, string text)
    {
        File.WriteAllText(path, text);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }

    [ClassInitialize]
    public static void BuildFixture(TestContext _)
    {
        templateRoot = Path.Combine(Path.GetTempPath(), "play-run-fixture-" + Guid.NewGuid().ToString("N"));
        var repo = Path.Combine(templateRoot, "repo");
        void Put(string path, string text)
        {
            var full = Path.Combine(repo, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        Put("tools/play-run.sh", File.ReadAllText(Path.Combine(RepoRoot(), "tools", "play-run.sh")));
        Put("src/Game.cs", "class Game {}");
        Put("scenes/main.tscn", "[scene]");
        Put("project.godot", "config");
        Put("OpenSettlers.csproj", "<Project />");
        Put("global.json", "{}");
        Put("play/demo.play", "wait 1");
        Put("play/.cache.play", "wait 1");
        Put("README.md", "readme");
        Put("tests/T.cs", "class T {}");
        Put(".gitignore", "play-out/\nsrc/ignored.txt\n");
        Put("src/ignored.txt", "ignored");
        foreach (
            var args in new[]
            {
                new[] { "init", "-q", "--template=" },
                ["add", "-A"],
                ["-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "init"],
            }
        )
        {
            var info = new ProcessStartInfo("git") { WorkingDirectory = repo };
            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            using var process = Process.Start(info)!;
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, $"git {args[^1]} failed in the fixture");
        }
    }

    [ClassCleanup]
    public static void DeleteFixture() => Directory.Delete(templateRoot, true);

    [TestInitialize]
    [UnsupportedOSPlatform("windows")]
    public void Init()
    {
        root = Path.Combine(Path.GetTempPath(), "play-run-wrapper-" + Guid.NewGuid().ToString("N"));
        repo = Path.Combine(root, "repo");
        fake = Path.Combine(root, "fake");
        Directory.CreateDirectory(fake);
        Executable(Path.Combine(fake, "mise"), FakeMise);
        Executable(Path.Combine(fake, "godot"), FakeGodot);
        Executable(Path.Combine(fake, "dotnet"), FakeDotnet);
        CopyTree(Path.Combine(templateRoot, "repo"), repo);
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

    // A started process whose output is collected as it arrives, so a test can poll for a line while it runs.
    sealed class Running(Process process, string name)
    {
        readonly StringBuilder stdout = new();
        readonly StringBuilder stderr = new();

        public string Stderr
        {
            get
            {
                lock (stderr)
                {
                    return stderr.ToString();
                }
            }
        }

        public void Collect()
        {
            process.OutputDataReceived += (_, e) => Append(stdout, e.Data);
            process.ErrorDataReceived += (_, e) => Append(stderr, e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        static void Append(StringBuilder to, string? line)
        {
            lock (to)
            {
                to.AppendLine(line);
            }
        }

        public (int Status, string Stdout, string Stderr) Finish()
        {
            if (!process.WaitForExit(30000))
            {
                process.Kill(true);
                Assert.Fail($"{name} did not exit within 30 s");
            }

            process.WaitForExit();
            var result = (process.ExitCode, stdout.ToString(), Stderr);
            process.Dispose();
            return result;
        }
    }

    static void WaitFor(Func<bool> condition, string what) =>
        Assert.IsTrue(
            SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(30)),
            $"timed out after 30 s waiting for {what}"
        );

    Running Launch(string file, string[] args, Dictionary<string, string?> env)
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

        var running = new Running(Process.Start(info)!, file);
        running.Collect();
        return running;
    }

    Running Start(string[] args, Dictionary<string, string?>? env = null) =>
        Launch("sh", ["tools/play-run.sh", .. args], RunEnv(env));

    Dictionary<string, string?> RunEnv(Dictionary<string, string?>? env)
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

        return all;
    }

    (int Status, string Stdout, string Stderr) Run(string[] args, Dictionary<string, string?>? env = null) =>
        Start(args, env).Finish();

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

    // One run sequence on one repository: each step changes one input against the state the step before it ran
    // on, so an input the hash misses leaves that step on the previous entry and Godot is not started.
    [TestMethod]
    public void EachChangedHashedInputStartsGodotAgain()
    {
        var args = new List<string>(Args);
        var env = new Dictionary<string, string?>();
        var steps = new (string Input, Action Change)[]
        {
            ("src", () => Write("src/Game.cs", "class Game { int x; }")),
            ("untracked-scenes", () => Write("scenes/new.tscn", "[scene]")),
            ("deleted", () => File.Delete(Path.Combine(repo, "scenes/main.tscn"))),
            ("renamed", () => File.Move(Path.Combine(repo, "src/Game.cs"), Path.Combine(repo, "src/Game2.cs"))),
            ("project", () => Write("project.godot", "config 2")),
            ("script", () => Write("play/demo.play", "wait 2")),
            ("seed", () => args[2] = "xyz"),
            ("size", () => args[4] = "3"),
            ("no-seed", () => args.RemoveRange(1, 2)),
            ("fps", () => args[^1] = "30"),
            ("version", () => env["FAKE_VERSION"] = "4.8.stable.mono"),
            ("siedler", () => env["SIEDLER_DIR"] = "/games/other"),
        };
        Run([.. args], env);
        var expected = 1;

        foreach (var (input, change) in steps)
        {
            change();
            Run([.. args], env);
            expected++;

            Assert.AreEqual(expected, Calls, $"a changed {input} did not start Godot again");
        }
    }

    [TestMethod]
    public void AnEditOutsideTheHashedInputsReuses()
    {
        Run(Args);
        Write("README.md", "edited");
        Write("tests/T.cs", "edited");
        Write("src/ignored.txt", "edited");

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

    // Each run has its own mode, layout path or seed and size, so none reuses an earlier entry and each
    // rewrites the args file. A SIEDLER_DIR with spaces is one element of the line.
    [TestMethod]
    public void GodotGetsTheLaunchLineOfEachMode()
    {
        string[] LaunchLine()
        {
            var args = File.ReadAllLines(Path.Combine(fake, "args"));
            return args[(Array.IndexOf(args, "--write-movie") + 2)..^2];
        }

        Write("play/a.layout", "layout");
        Write("play/b.layout", "layout");

        Run(Args);
        var map = File.ReadAllLines(Path.Combine(fake, "args"));
        var movie = Array.IndexOf(map, "--write-movie");
        Assert.AreEqual("play-out/demo/frame.png", map[movie + 1]);
        CollectionAssert.AreEqual(new[] { "--play", "play/demo.play" }, map[^2..]);
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--map", "--seed", "abc", "--size", "2" },
            LaunchLine()
        );

        Run([.. Args, "--game-view"]);
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--game-view", "--seed", "abc", "--size", "2" },
            LaunchLine()
        );

        Run(["demo", "--layout", "play/a.layout", "--fps", "10"]);
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--game-view", "--layout", "play/a.layout" },
            LaunchLine()
        );

        Run(["demo", "--game-view", "--layout", "play/b.layout", "--fps", "10"]);
        CollectionAssert.AreEqual(
            new[] { "--fixed-fps", "10", "--", "--game", Siedler, "--game-view", "--layout", "play/b.layout" },
            LaunchLine()
        );

        Run(["demo", "--fps", "10"]);
        var bare = LaunchLine();
        CollectionAssert.DoesNotContain(bare, "--seed");
        CollectionAssert.DoesNotContain(bare, "--size");
        Assert.AreEqual(5, Calls, "a run did not start Godot, so the args file is stale");
    }

    // Mode, layout path and layout bytes are hashed, a repeated call reuses, and the map and the game view
    // never share an entry.
    [TestMethod]
    public void TheModeAndTheLayoutAreHashedAndARepeatedCallReuses()
    {
        Write("play/a.layout", "layout");
        Write("play/b.layout", "layout");
        string[] a = ["demo", "--layout", "play/a.layout", "--fps", "10"];
        string[] gameView = [.. Args, "--game-view"];

        Run(Args);
        Run(gameView);
        Assert.AreEqual(2, Calls, "the game view reused the map view entry");
        Run(Args);
        Run(gameView);
        Assert.AreEqual(2, Calls, "a repeated call did not reuse");
        Assert.AreEqual(2, Directory.GetDirectories(Out(".cache")).Length);

        Run(a);
        Run(a);
        Assert.AreEqual(3, Calls, "a repeated layout call did not reuse");
        Run(["demo", "--layout", "play/b.layout", "--fps", "10"]);
        Assert.AreEqual(4, Calls, "the layout path is not hashed");
        Write("play/a.layout", "layouu");
        Run(a);
        Assert.AreEqual(5, Calls, "the layout bytes are not hashed");
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
    public void TwoConcurrentCallsForOneStemRunGodotOnceAndStoreOneEntry()
    {
        var gate = Path.Combine(fake, "gate");
        var env = new Dictionary<string, string?> { ["FAKE_GATE"] = gate };
        var first = Start(Args, env);
        WaitFor(() => Calls == 1, "the first call to start Godot");
        var second = Start(Args, env);
        WaitFor(() => second.Stderr.Contains("waiting on"), "the second call to say it waits on the lock");
        File.WriteAllText(gate, "");
        var results = new[] { first.Finish(), second.Finish() };

        CollectionAssert.AreEqual(new[] { 0, 0 }, results.Select(r => r.Status).ToArray());
        Assert.AreEqual(1, Calls);
        var entry = Directory.GetDirectories(Out(".cache")).Single();
        CollectionAssert.AreEqual(new[] { "out" }, Directory.GetDirectories(entry).Select(Path.GetFileName).ToArray());
        Assert.AreEqual("state 1\n", File.ReadAllText(Out("demo/end.txt")));
    }

    [TestMethod]
    public void CallsOfDifferentStemsRunInParallel()
    {
        Write("play/other.play", "wait 1");
        var gate = Path.Combine(fake, "gate");
        var env = new Dictionary<string, string?> { ["FAKE_GATE"] = gate };
        var first = Start(Args, env);
        var second = Start(["other", "--fps", "10"], env);
        // Godot of each call waits on the shut gate, so both starts show only where the calls overlap.
        WaitFor(() => Calls == 2, "both stems to start Godot while the gate is shut");
        File.WriteAllText(gate, "");

        CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { first.Finish().Status, second.Finish().Status });
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
    public void ACallWaitingOnALiveLockSaysSoAndHashesTheInputsOnceItIsFree()
    {
        const string waiting = "play-run.sh: waiting on play-out/.lock-demo (remove it if no run is live)";
        Run(Args);
        HoldLock(Environment.ProcessId);
        var call = Start(Args);
        WaitFor(() => call.Stderr.Contains(waiting), "the call to say it waits on the lock");
        Write("src/Game.cs", "class Game { int changed; }");
        Directory.Delete(Out(".lock-demo"), true);
        var (status, _, stderr) = call.Finish();

        Assert.AreEqual(0, status);
        StringAssert.Contains(stderr, waiting);
        Assert.AreEqual(2, Calls, "the hash was taken before the lock, so the call reused the stale entry");
    }
}
