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
        while [ -n "${FAKE_GATE:-}" ] && [ ! -e "$FAKE_GATE" ] && [ $i -lt 2400 ]; do
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

    static string _templateRoot = "";

    string _root = "";
    string _repo = "";
    string _fake = "";

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
        _templateRoot = Path.Combine(Path.GetTempPath(), "play-run-fixture-" + Guid.NewGuid().ToString("N"));
        var repo = Path.Combine(_templateRoot, "repo");
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
        Put("Directory.Build.props", "<Project />");
        Put("play/demo.play", "wait 1");
        Put("play/.cache.play", "wait 1");
        Put("content/open-settlers/manifest.json", "{}");
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
    public static void DeleteFixture() => Directory.Delete(_templateRoot, true);

    [TestInitialize]
    [UnsupportedOSPlatform("windows")]
    public void Init()
    {
        _root = Path.Combine(Path.GetTempPath(), "play-run-wrapper-" + Guid.NewGuid().ToString("N"));
        _repo = Path.Combine(_root, "repo");
        _fake = Path.Combine(_root, "fake");
        Directory.CreateDirectory(_fake);
        Executable(Path.Combine(_fake, "mise"), FakeMise);
        Executable(Path.Combine(_fake, "godot"), FakeGodot);
        Executable(Path.Combine(_fake, "dotnet"), FakeDotnet);
        Directory.CreateDirectory(Path.Combine(_fake, "psbin"));
        Executable(Path.Combine(_fake, "psbin", "ps"), FakePs);
        CopyTree(Path.Combine(_templateRoot, "repo"), _repo);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

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
        var full = Path.Combine(_repo, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    string Out(string path) => Path.Combine(_repo, "play-out", path);

    int Calls => File.Exists(Path.Combine(_fake, "calls")) ? File.ReadAllLines(Path.Combine(_fake, "calls")).Length : 0;

    string[] Order => File.Exists(Path.Combine(_fake, "order")) ? File.ReadAllLines(Path.Combine(_fake, "order")) : [];

    string[] LogLines => File.Exists(Out("runs.log")) ? File.ReadAllLines(Out("runs.log")) : [];

    static readonly string[] Args = ["demo", "--seed", "abc", "--size", "2", "--fps", "10"];

    // A started process whose output is collected as it arrives, so a test can poll for a line while it runs. Disposing
    // kills a process still alive, so a failing wait leaves none behind.
    sealed class Running : IDisposable
    {
        readonly Process _process;
        readonly string _name;
        readonly StringBuilder _stdout = new();
        readonly StringBuilder _stderr = new();

        public Running(Process process, string name)
        {
            _process = process;
            _name = name;
            process.OutputDataReceived += (_, e) => Append(_stdout, e.Data);
            process.ErrorDataReceived += (_, e) => Append(_stderr, e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        public int Pid => _process.Id;

        public string Stderr
        {
            get
            {
                lock (_stderr)
                {
                    return _stderr.ToString();
                }
            }
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
            if (!_process.WaitForExit(30000))
            {
                Assert.Fail($"{_name} did not exit within 30 s");
            }

            _process.WaitForExit();
            return (_process.ExitCode, _stdout.ToString(), Stderr);
        }

        public void Dispose()
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
                _process.WaitForExit();
            }

            _process.Dispose();
        }
    }

    static void WaitFor(Func<bool> condition, string what) =>
        Assert.IsTrue(
            SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(30)),
            $"timed out after 30 s waiting for {what}"
        );

    Running Start(string[] args, Dictionary<string, string?>? env = null)
    {
        var info = new ProcessStartInfo("sh")
        {
            WorkingDirectory = _repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("tools/play-run.sh");
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        info.Environment["PLAY_RUN_MISE"] = Path.Combine(_fake, "mise");
        info.Environment["PLAY_RUN_GODOT"] = Path.Combine(_fake, "godot");
        info.Environment["PLAY_RUN_DOTNET"] = Path.Combine(_fake, "dotnet");
        info.Environment["SIEDLER_DIR"] = Siedler;
        info.Environment["FAKE_DIR"] = _fake;
        foreach (var (key, value) in env ?? [])
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

        return new Running(Process.Start(info)!, "sh");
    }

    (int Status, string Stdout, string Stderr) Run(string[] args, Dictionary<string, string?>? env = null)
    {
        using var running = Start(args, env);
        return running.Finish();
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
            ("deleted", () => File.Delete(Path.Combine(_repo, "scenes/main.tscn"))),
            ("renamed", () => File.Move(Path.Combine(_repo, "src/Game.cs"), Path.Combine(_repo, "src/Game2.cs"))),
            ("project", () => Write("project.godot", "config 2")),
            ("csproj", () => Write("OpenSettlers.csproj", "<Project><!-- 2 --></Project>")),
            ("global-json", () => Write("global.json", "{ }")),
            ("build-props", () => Write("Directory.Build.props", "<Project><!-- 2 --></Project>")),
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

    // The wrapper hashes "<file> missing" for an absent project file and "<file> <git hash of the content>" for a
    // present one, so an empty file (the hash of zero bytes) and an absent file are two entries. Hand check: the
    // two lines differ in their second field, so they cannot hash alike.
    [TestMethod]
    [DataRow("project.godot")]
    [DataRow("OpenSettlers.csproj")]
    [DataRow("global.json")]
    [DataRow("Directory.Build.props")]
    public void AMissingProjectFileHashesApartFromAnEmptyOne(string file)
    {
        Run(Args);
        File.Delete(Path.Combine(_repo, file));
        Run(Args);
        Assert.AreEqual(2, Calls, $"a missing {file} reused the entry of the present one");

        Write(file, "");
        Run(Args);
        Assert.AreEqual(3, Calls, $"an empty {file} reused the entry of the missing one");

        File.Delete(Path.Combine(_repo, file));
        Run(Args);
        Assert.AreEqual(3, Calls, $"a missing {file} again did not reuse its own entry");
    }

    // Each signal reaches the wrapper after Godot started, which is after the wrapper wrote its pid into the lock.
    // The shell runs a trap once its foreground command ends, so the gate opens after the signal. 130 is the status
    // the trap in tools/play-run.sh states (`exit 130`).
    static void Signal(Running call, string signal)
    {
        // The shell's own kill builtin, so a host with no standalone kill executable still runs the case.
        using var kill = Process.Start("sh", ["-c", "kill -s \"$1\" \"$2\"", "sh", signal, call.Pid.ToString()])!;
        kill.WaitForExit();
        Assert.AreEqual(0, kill.ExitCode, $"kill -s {signal} failed");
    }

    [TestMethod]
    [DataRow("INT")]
    [DataRow("TERM")]
    [DataRow("HUP")]
    public void ASignalReleasesTheWrappersOwnLockAndExitsOneHundredThirty(string signal)
    {
        var gate = Path.Combine(_fake, "gate");
        using var call = Start(Args, new() { ["FAKE_GATE"] = gate });
        WaitFor(() => Calls == 1, "the call to start Godot");
        Assert.AreEqual(call.Pid + "\n", File.ReadAllText(Out(".lock-demo/pid")));

        Signal(call, signal);
        File.WriteAllText(gate, "");
        var (status, _, _) = call.Finish();

        Assert.AreEqual(130, status);
        Assert.IsFalse(Directory.Exists(Out(".lock-demo")), "the lock stayed after the signal");
        Assert.AreEqual(1, Calls);
    }

    [TestMethod]
    [DataRow("INT")]
    [DataRow("TERM")]
    [DataRow("HUP")]
    public void ASignalLeavesALockThatHoldsAnotherPid(string signal)
    {
        var gate = Path.Combine(_fake, "gate");
        using var call = Start(Args, new() { ["FAKE_GATE"] = gate });
        WaitFor(() => Calls == 1, "the call to start Godot");
        File.WriteAllText(Out(".lock-demo/pid"), "1\n");

        Signal(call, signal);
        File.WriteAllText(gate, "");
        var (status, _, _) = call.Finish();

        Assert.AreEqual(130, status);
        Assert.AreEqual("1\n", File.ReadAllText(Out(".lock-demo/pid")));
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
        var miseCalls = File.ReadAllLines(Path.Combine(_fake, "mise.log")).Length;
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
            var args = File.ReadAllLines(Path.Combine(_fake, "args"));
            return args[(Array.IndexOf(args, "--write-movie") + 2)..^2];
        }

        Write("play/a.layout", "layout");
        Write("play/b.layout", "layout");

        Run(Args);
        var map = File.ReadAllLines(Path.Combine(_fake, "args"));
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
        var whole = File.ReadAllLines(Path.Combine(_fake, "args"));
        CollectionAssert.DoesNotContain(whole, "--seed");
        CollectionAssert.DoesNotContain(whole, "--size");
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
    public void GodotGetsTheGraphicsFlagOnlyWhereTheRunGaveOne()
    {
        string[] Launch() => File.ReadAllLines(Path.Combine(_fake, "args"));

        Run([.. Args, "--graphics", "open-settlers"]);
        CollectionAssert.AreEqual(
            new[] { "--size", "2", "--graphics", "open-settlers", "--play", "play/demo.play" },
            Launch()[^6..]
        );

        Run(["demo", "--fps", "10", "--graphics", "history-edition"]);
        CollectionAssert.AreEqual(new[] { "--graphics", "history-edition" }, Launch()[^4..^2]);

        Run(["demo", "--fps", "10", "--seed", "abc"]);
        CollectionAssert.DoesNotContain(Launch(), "--graphics");
    }

    // The two sources never share a result, and the default is the History Edition: naming it changes nothing. A
    // change to the content set, a new file in it included, starts Godot again.
    [TestMethod]
    public void TheGraphicsSourceAndTheContentSetAreHashed()
    {
        string[] History = [.. Args, "--graphics", "history-edition"];
        string[] Own = [.. Args, "--graphics", "open-settlers"];

        Run(Args);
        Run(History);
        Assert.AreEqual(1, Calls, "naming the History Edition is not the default");
        Run(Own);
        Assert.AreEqual(2, Calls, "an open-settlers run reused a History Edition result");
        Run(Own);
        Assert.AreEqual(2, Calls, "a repeated open-settlers call did not reuse");

        Write("content/open-settlers/manifest.json", "{ }");
        Run(Own);
        Assert.AreEqual(3, Calls, "an edited manifest did not start Godot");
        Write("content/open-settlers/terrain/new.png", "png");
        Run(Own);
        Assert.AreEqual(4, Calls, "a new image did not start Godot");
        File.Delete(Path.Combine(_repo, "content/open-settlers/manifest.json"));
        Run(Own);
        Assert.AreEqual(5, Calls, "a removed manifest did not start Godot");
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
        CollectionAssert.AreEqual(new[] { "exit=3", "build" }, LogLines.Single().Split('\t')[11..]);
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
            new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = _repo, RedirectStandardOutput = true }
        )!;
        var head = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();

        var first = lines[0].Split('\t');
        Assert.AreEqual(13, first.Length, lines[0]);
        StringAssert.Matches(first[0], new System.Text.RegularExpressions.Regex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$"));
        StringAssert.Matches(first[1], new System.Text.RegularExpressions.Regex("^[0-9a-f]{40}$"));
        CollectionAssert.AreEqual(
            new[]
            {
                head,
                "clean",
                "demo",
                "mode=map",
                "layout=",
                "seed=abc",
                "size=2",
                "fps=10",
                "graphics=history-edition",
                "exit=0",
                "run",
            },
            first[2..]
        );
        var second = lines[1].Split('\t');
        Assert.AreEqual(first[1], second[1]);
        CollectionAssert.AreEqual(new[] { "dirty", "reused" }, new[] { second[3], second[12] });
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
    [DataRow("empty size", "demo", "--size", "", "--fps", "10")]
    [DataRow("empty seed", "demo", "--seed", "", "--fps", "10")]
    [DataRow("slash stem", "play/demo", "--fps", "10")]
    [DataRow("dots stem", "..", "--fps", "10")]
    [DataRow("parent stem", "../x", "--fps", "10")]
    [DataRow("hidden stem", ".cache", "--fps", "10")]
    [DataRow("missing script", "nothing", "--fps", "10")]
    [DataRow("unknown flag", "demo", "--fps", "10", "--fast")]
    [DataRow("missing layout", "demo", "--layout", "play/none.layout", "--fps", "10")]
    [DataRow("layout with no value", "demo", "--fps", "10", "--layout")]
    [DataRow("empty layout", "demo", "--layout", "", "--fps", "10")]
    [DataRow("unknown graphics source", "demo", "--graphics", "retro", "--fps", "10")]
    [DataRow("graphics with no value", "demo", "--fps", "10", "--graphics")]
    [DataRow("repeated graphics", "demo", "--graphics", "open-settlers", "--graphics", "open-settlers", "--fps", "10")]
    [DataRow("no args")]
    public void BadArgumentsExitTwoAndLeaveNoTrace(string reason, params string[] args) =>
        AssertUsageError(Run(args), reason);

    [TestMethod]
    [DataRow("layout with seed", "--layout", "play/a.layout", "--seed", "abc")]
    [DataRow("layout with size", "--layout", "play/a.layout", "--size", "2")]
    [DataRow("seed before layout", "--seed", "abc", "--layout", "play/a.layout")]
    [DataRow("layout with empty size", "--layout", "play/a.layout", "--size", "")]
    [DataRow("layout with empty seed", "--layout", "play/a.layout", "--seed", "")]
    public void ALayoutWithSeedOrSizeExitsTwoAndLeavesNoTrace(string reason, params string[] args)
    {
        Write("play/a.layout", "layout");
        var result = Run(["demo", .. args, "--fps", "10"]);

        AssertUsageError(result, reason);
        StringAssert.Contains(result.Stderr, reason.Contains("empty") ? "needs a value" : "own seed and size", reason);
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
        var gate = Path.Combine(_fake, "gate");
        var env = new Dictionary<string, string?> { ["FAKE_GATE"] = gate };
        using var first = Start(Args, env);
        WaitFor(() => Calls == 1, "the first call to start Godot");
        using var second = Start(Args, env);
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
        var gate = Path.Combine(_fake, "gate");
        var env = new Dictionary<string, string?> { ["FAKE_GATE"] = gate };
        using var first = Start(Args, env);
        using var second = Start(["other", "--fps", "10"], env);
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

    // `ps -p abc` exits 1 with no output, so a pid that names no process reads as dead.
    [TestMethod]
    public void ALockWhosePidIsNotANumberIsTakenOver()
    {
        Directory.CreateDirectory(Out(".lock-demo"));
        File.WriteAllText(Out(".lock-demo/pid"), "abc\n");

        Assert.AreEqual(0, Run(Args).Status);
        Assert.AreEqual(1, Calls);
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void AnUnwritablePlayOutExitsWithTwoAndLogsNoRun()
    {
        Assert.AreEqual(0, Run(Args).Status);
        var before = LogLines;
        var playOut = Path.Combine(_repo, "play-out");
        var mode = File.GetUnixFileMode(playOut);
        File.SetUnixFileMode(playOut, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            try
            {
                File.WriteAllText(Path.Combine(playOut, "probe"), "x");
                Assert.Inconclusive("play-out/ stays writable for this user (root)");
            }
            catch (UnauthorizedAccessException) { }

            var (status, _, stderr) = Run(Args);

            Assert.AreEqual(2, status);
            StringAssert.Contains(stderr, "play-run.sh: cannot write play-out/");
            CollectionAssert.AreEqual(before, LogLines);
            Assert.AreEqual(1, Calls);
        }
        finally
        {
            File.SetUnixFileMode(playOut, mode);
        }
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
        const string Waiting =
            "play-run.sh: waiting on play-out/.lock-demo or play-out/.lock-demo.takeover (remove them if no run is live)";
        Run(Args);
        HoldLock(Environment.ProcessId);
        using var call = Start(Args);
        WaitFor(() => call.Stderr.Contains(Waiting), "the call to say it waits on the lock");
        Write("src/Game.cs", "class Game { int changed; }");
        Directory.Delete(Out(".lock-demo"), true);
        var (status, _, stderr) = call.Finish();

        Assert.AreEqual(0, status);
        StringAssert.Contains(stderr, Waiting);
        Assert.AreEqual(2, Calls, "the hash was taken before the lock, so the call reused the stale entry");
    }

    void HoldTakeover(TimeSpan age)
    {
        Directory.CreateDirectory(Out(".lock-demo.takeover"));
        Directory.SetLastWriteTimeUtc(Out(".lock-demo.takeover"), DateTime.UtcNow - age);
    }

    static int DeadPid()
    {
        using var gone = Process.Start("true")!;
        gone.WaitForExit();
        return gone.Id;
    }

    [TestMethod]
    public void ATakeoverMutexOlderThanFiveSecondsIsClearedAndTheCallGoesOn()
    {
        HoldLock(DeadPid());
        HoldTakeover(TimeSpan.FromMinutes(5));

        Assert.AreEqual(0, Run(Args).Status);
        Assert.AreEqual(1, Calls);
        Assert.IsFalse(Directory.Exists(Out(".lock-demo.takeover")));
    }

    [TestMethod]
    public void AFreshTakeoverMutexMakesTheCallWaitAndTheMessageNamesIt()
    {
        HoldLock(DeadPid());
        HoldTakeover(TimeSpan.Zero);
        using var call = Start(Args);
        WaitFor(() => call.Stderr.Contains("play-out/.lock-demo.takeover"), "the call to name the takeover mutex");
        Assert.AreEqual(0, Calls);
        Directory.Delete(Out(".lock-demo.takeover"));

        Assert.AreEqual(0, call.Finish().Status);
        Assert.AreEqual(1, Calls);
    }

    // A `ps` that prints a live pid and exits 0, and judges the dead owner's pid dead (empty output, exit 1) and
    // numbers those judgments. At judgment FAKE_PS_SWAP_AT it also replaces the lock's pid with a live owner's, as
    // another call does between this call's judgment and its removal of the lock. Asked about FAKE_PS_MDEAD it
    // replaces the takeover mutex's pid with a live holder's, as a call does that takes the mutex between another
    // call's judgment of the mutex and the clearing of it, and with FAKE_PS_MLOG it appends to ps-mutex-log whether
    // the mutex path stands. FAKE_PS_OWN makes the first self-probe (a pid that is neither dead nor FAKE_PS_MDEAD)
    // leave a mutex holding that pid, which the wrapper's own pid is, as a dead earlier call with a reused pid leaves one.
    // Asked about a live pid while the lock path is free, it creates a lock for FAKE_PS_W, as a waiting call does.
    const string FakePs = """
        #!/bin/sh
        [ -n "${FAKE_PS_EXIT:-}" ] && exit "$FAKE_PS_EXIT"
        if [ "$2" = "${FAKE_PS_MSWAP_PID:-x}" ]; then
          if [ -d play-out/.lock-demo.takeover ]; then echo present; else echo absent; fi >> "$FAKE_DIR/ps-swapped-log"
          exit 1
        fi
        if [ "$2" = "${FAKE_PS_MDEAD:-x}" ]; then
          if [ -n "${FAKE_PS_MLOG:-}" ]; then
            if [ -d play-out/.lock-demo.takeover ]; then echo present; else echo absent; fi >> "$FAKE_DIR/ps-mutex-log"
          fi
          [ -n "${FAKE_PS_MSWAP:-}" ] && echo "${FAKE_PS_MSWAP_PID:-$FAKE_PS_LIVE}" > play-out/.lock-demo.takeover/pid
          exit 1
        fi
        if [ -n "${FAKE_PS_OWN:-}" ] && [ "$2" != "$FAKE_PS_DEAD" ] && [ ! -e "$FAKE_DIR/own-done" ] && [ ! -d play-out/.lock-demo.takeover ]; then
          : > "$FAKE_DIR/own-done"
          mkdir play-out/.lock-demo.takeover
          echo "$2" > play-out/.lock-demo.takeover/pid
        fi
        if [ "$2" = "$FAKE_PS_DEAD" ]; then
          n=$(cat "$FAKE_DIR/ps-count" 2>/dev/null || echo 0)
          n=$((n + 1))
          echo "$n" > "$FAKE_DIR/ps-count"
          [ "$n" = "${FAKE_PS_SWAP_AT:-0}" ] && echo "$FAKE_PS_LIVE" > play-out/.lock-demo/pid
          exit 1
        fi
        if [ -n "${FAKE_PS_W:-}" ] && [ ! -d play-out/.lock-demo ]; then
          mkdir play-out/.lock-demo
          echo "$FAKE_PS_W" > play-out/.lock-demo/pid
        fi
        echo "$2"
        exit 0
        """;

    Dictionary<string, string?> FakePsEnv(int dead, Dictionary<string, string?>? more = null)
    {
        var bin = Path.Combine(_fake, "psbin");
        var env = new Dictionary<string, string?>
        {
            ["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            ["FAKE_PS_DEAD"] = dead.ToString(),
            ["FAKE_PS_LIVE"] = Environment.ProcessId.ToString(),
        };
        foreach (var (key, value) in more ?? [])
        {
            env[key] = value;
        }

        return env;
    }

    // The call judges the dead lock stale outside the mutex (judgment 1), clears the stale mutex, then judges it again
    // holding the mutex (judgment 3), and between that judgment and its removal the lock turns into a live owner's.
    [TestMethod]
    public void ALockThatTurnsLiveAfterTheJudgmentUnderTheMutexIsLeftInPlace()
    {
        var dead = DeadPid();
        HoldLock(dead);
        HoldTakeover(TimeSpan.FromMinutes(5));
        using var call = Start(Args, FakePsEnv(dead, new() { ["FAKE_PS_SWAP_AT"] = "3" }));
        WaitFor(() => call.Stderr.Contains("waiting on"), "the call to wait on the live owner");

        Assert.IsFalse(
            Directory.Exists(Out(".lock-demo.takeover")),
            "the stale mutex was cleared, so the call took the branch"
        );
        Assert.AreEqual(Environment.ProcessId + "\n", File.ReadAllText(Out(".lock-demo/pid")));
        Assert.AreEqual(0, Calls);
    }

    // Same turn, and a waiting call creates the lock path should the wrapper ever move the live lock off it: the
    // wrapper must leave exactly that one pid file in the lock, with nothing nested into it.
    [TestMethod]
    public void AJudgedLockIsNeverMovedOffItsPath()
    {
        var dead = DeadPid();
        HoldLock(dead);
        HoldTakeover(TimeSpan.FromMinutes(5));
        using var call = Start(Args, FakePsEnv(dead, new() { ["FAKE_PS_SWAP_AT"] = "3", ["FAKE_PS_W"] = "1" }));
        WaitFor(() => call.Stderr.Contains("waiting on"), "the call to wait");

        CollectionAssert.AreEqual(
            new[] { "pid" },
            Directory.GetFileSystemEntries(Out(".lock-demo")).Select(Path.GetFileName).ToArray()
        );
        Assert.AreEqual(0, Calls);
    }

    [TestMethod]
    public void AFailingPsIsNoProofThatAnOwnerIsDead()
    {
        HoldLock(Environment.ProcessId);
        using var call = Start(Args, FakePsEnv(DeadPid(), new() { ["FAKE_PS_EXIT"] = "127" }));
        WaitFor(() => call.Stderr.Contains("waiting on"), "the call to wait when ps fails");

        Assert.AreEqual(Environment.ProcessId + "\n", File.ReadAllText(Out(".lock-demo/pid")));
        Assert.AreEqual(0, Calls);
    }

    // A `ps` without -p (BusyBox) prints its usage and exits 1, which is no proof that the owner is dead.
    [TestMethod]
    public void APsThatRejectsItsArgumentsWithExitOneIsNoProofThatAnOwnerIsDead()
    {
        HoldLock(Environment.ProcessId);
        using var call = Start(Args, FakePsEnv(DeadPid(), new() { ["FAKE_PS_EXIT"] = "1" }));
        WaitFor(() => call.Stderr.Contains("waiting on"), "the call to wait when ps rejects -p");

        Assert.AreEqual(Environment.ProcessId + "\n", File.ReadAllText(Out(".lock-demo/pid")));
        Assert.AreEqual(0, Calls);
    }

    void HoldTakeover(int holder, TimeSpan age)
    {
        HoldTakeover(age);
        File.WriteAllText(Out(".lock-demo.takeover/pid"), holder + "\n");
        Directory.SetLastWriteTimeUtc(Out(".lock-demo.takeover"), DateTime.UtcNow - age);
    }

    [TestMethod]
    public void AFreshTakeoverMutexOfADeadHolderIsClearedAndTheCallGoesOn()
    {
        HoldLock(DeadPid());
        HoldTakeover(DeadPid(), TimeSpan.Zero);

        Assert.AreEqual(0, Run(Args).Status);
        Assert.AreEqual(1, Calls);
        Assert.IsFalse(Directory.Exists(Out(".lock-demo.takeover")));
    }

    [TestMethod]
    public void AnOldTakeoverMutexOfALiveHolderMakesTheCallWait()
    {
        HoldLock(DeadPid());
        HoldTakeover(Environment.ProcessId, TimeSpan.FromMinutes(5));
        using var call = Start(Args);
        WaitFor(
            () => call.Stderr.Contains("play-out/.lock-demo.takeover"),
            "the call to wait on the live holder's mutex"
        );

        Assert.AreEqual(Environment.ProcessId + "\n", File.ReadAllText(Out(".lock-demo.takeover/pid")));
        Assert.AreEqual(0, Calls);
    }

    // The call judges the mutex of a dead holder stale, and before it clears the mutex a live call takes the path
    // (the fake ps puts a live pid into the mutex). The call must leave that holder's mutex in place and wait.
    [TestMethod]
    public void AMutexThatTurnsLiveBeforeItIsClearedIsLeftInPlaceAndTheCallWaits()
    {
        var deadHolder = DeadPid();
        var deadOwner = DeadPid();
        HoldLock(deadOwner);
        HoldTakeover(deadHolder, TimeSpan.FromMinutes(5));
        using var call = Start(
            Args,
            FakePsEnv(deadOwner, new() { ["FAKE_PS_MDEAD"] = deadHolder.ToString(), ["FAKE_PS_MSWAP"] = "1" })
        );
        WaitFor(() => call.Stderr.Contains("waiting on"), "the call to wait on the live holder's mutex");

        Assert.AreEqual(Environment.ProcessId + "\n", File.ReadAllText(Out(".lock-demo.takeover/pid")));
        Assert.AreEqual(
            1,
            Directory.GetFileSystemEntries(Out(""), ".lock-demo.takeover*").Length,
            "no sibling of the mutex is left"
        );
        Assert.AreEqual(0, Calls);
    }

    // The reaper of dead holder D judges the mutex stale a second time, and by then another dead holder E has taken
    // the path (the fake ps puts E's pid into the mutex). D's reaper must leave E's mutex in place: the call goes on
    // through E's reaper, which judges the mutex stale (ps-swapped-log) before it clears it.
    [TestMethod]
    public void AReaperOfOneDeadHolderLeavesTheMutexOfAnotherDeadHolderToItsOwnReaper()
    {
        var holderD = DeadPid();
        var holderE = DeadPid();
        var deadOwner = DeadPid();
        HoldLock(deadOwner);
        HoldTakeover(holderD, TimeSpan.Zero);
        var call = Run(
            Args,
            FakePsEnv(
                deadOwner,
                new()
                {
                    ["FAKE_PS_MDEAD"] = holderD.ToString(),
                    ["FAKE_PS_MSWAP"] = "1",
                    ["FAKE_PS_MSWAP_PID"] = holderE.ToString(),
                }
            )
        );

        Assert.AreEqual(0, call.Status);
        Assert.AreEqual(1, Calls);
        var log = File.ReadAllLines(Path.Combine(_fake, "ps-swapped-log"));
        Assert.IsTrue(
            log.Length >= 3 && log.All(l => l == "present"),
            "E's mutex stood through D's reaper and was judged again by E's reaper: " + string.Join(",", log)
        );
        Assert.AreEqual(0, Directory.GetFileSystemEntries(Out(""), ".lock-demo.takeover*").Length);
    }

    // The mutex is never moved off its path: while the call judges it stale a second time, the path still stands, so
    // no third call can take it and no live holder's mutex can be dropped when it is put back.
    [TestMethod]
    public void AStaleMutexStandsOnItsPathWhileItIsJudgedAgain()
    {
        var deadHolder = DeadPid();
        var deadOwner = DeadPid();
        HoldLock(deadOwner);
        HoldTakeover(deadHolder, TimeSpan.Zero);
        var call = Run(
            Args,
            FakePsEnv(deadOwner, new() { ["FAKE_PS_MDEAD"] = deadHolder.ToString(), ["FAKE_PS_MLOG"] = "1" })
        );

        Assert.AreEqual(0, call.Status);
        var log = File.ReadAllLines(Path.Combine(_fake, "ps-mutex-log"));
        Assert.IsTrue(log.Length >= 2, "the mutex is judged before and while it is cleared");
        Assert.IsTrue(log.All(l => l == "present"), "the mutex path stood at every judgment: " + string.Join(",", log));
    }

    // A call never waits on a mutex that holds its own pid outside its critical section (pid reuse: a dead
    // earlier call with this call's pid left it there): it removes it and goes on.
    [TestMethod]
    public void AMutexHoldingTheCallsOwnPidOutsideItsCriticalSectionIsRemovedAndTheCallGoesOn()
    {
        var dead = DeadPid();
        HoldLock(dead);
        var call = Run(Args, FakePsEnv(dead, new() { ["FAKE_PS_OWN"] = "1" }));

        Assert.AreEqual(0, call.Status);
        Assert.AreEqual(1, Calls);
        Assert.IsFalse(Directory.Exists(Out(".lock-demo.takeover")));
    }

    void HoldReap(string key, string? pid, TimeSpan age)
    {
        Directory.CreateDirectory(Out($".lock-demo.takeover.reap.{key}"));
        if (pid is not null)
        {
            File.WriteAllText(Out($".lock-demo.takeover.reap.{key}/pid"), pid + "\n");
        }

        Directory.SetLastWriteTimeUtc(Out($".lock-demo.takeover.reap.{key}"), DateTime.UtcNow - age);
    }

    // One reaper per dead holder: while a live call is reaping the mutex of holder D, another call leaves it.
    [TestMethod]
    public void ALiveReaperOfTheHoldersMutexMakesTheCallWait()
    {
        var deadHolder = DeadPid();
        HoldLock(DeadPid());
        HoldTakeover(deadHolder, TimeSpan.Zero);
        HoldReap(deadHolder.ToString(), Environment.ProcessId.ToString(), TimeSpan.FromMinutes(5));
        using var call = Start(Args);
        WaitFor(() => call.Stderr.Contains("waiting on"), "the call to wait on the reaper");

        Assert.IsTrue(Directory.Exists(Out(".lock-demo.takeover")), "the mutex is left to the reaper");
        Assert.AreEqual(0, Calls);
    }

    // A reaper killed inside its step leaves a reap dir with a dead pid, or with none: both are cleared by the next
    // call, which then reaps the mutex and goes on.
    [TestMethod]
    public void AReapDirOfADeadReaperIsClearedAndTheCallGoesOn()
    {
        var deadHolder = DeadPid();
        HoldLock(DeadPid());
        HoldTakeover(deadHolder, TimeSpan.Zero);
        HoldReap(deadHolder.ToString(), DeadPid().ToString(), TimeSpan.Zero);

        Assert.AreEqual(0, Run(Args).Status);
        Assert.AreEqual(1, Calls);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(Out(""), ".lock-demo.takeover*").Length);
    }

    [TestMethod]
    public void AReapDirWithNoPidOlderThanFiveSecondsIsClearedAndTheCallGoesOn()
    {
        HoldLock(DeadPid());
        HoldTakeover(TimeSpan.FromMinutes(5));
        HoldReap("nopid", null, TimeSpan.FromMinutes(5));

        Assert.AreEqual(0, Run(Args).Status);
        Assert.AreEqual(1, Calls);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(Out(""), ".lock-demo.takeover*").Length);
    }

    // Two calls find a dead lock beside a stale mutex. The lock is taken over once: one Godot call, one stored entry.
    [TestMethod]
    public void TwoCallsOnADeadLockAndAStaleMutexRunGodotOnceAndStoreOneEntry()
    {
        HoldLock(DeadPid());
        HoldTakeover(TimeSpan.FromMinutes(5));
        using var first = Start(Args);
        using var second = Start(Args);
        var results = new[] { first.Finish(), second.Finish() };

        CollectionAssert.AreEqual(new[] { 0, 0 }, results.Select(r => r.Status).ToArray());
        Assert.AreEqual(1, Calls);
        Assert.AreEqual(1, Directory.GetDirectories(Out(".cache")).Length);
    }

    // kill -0 on pid 1 fails with EPERM for a user other than root, which a liveness check by kill -0 reads as dead.
    [TestMethod]
    public void ALockHeldByAnotherUsersLiveProcessMakesTheCallWait()
    {
        HoldLock(1);
        using var call = Start(Args);
        WaitFor(() => call.Stderr.Contains("waiting on"), "the call to wait on pid 1's lock");

        Assert.AreEqual("1\n", File.ReadAllText(Out(".lock-demo/pid")));
        Assert.AreEqual(0, Calls);
    }
}
