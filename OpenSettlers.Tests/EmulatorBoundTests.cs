using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The per-command bound of tools/GhidraDump/emulate.py: a call whose emulator command, or the import before the first
// one, runs past --timeout fails, names what ran past it on stderr, and leaves no process of the launch running and no
// temporary project folder. A stand-in analyzeHeadless prints canned Ghidra output and then hangs in a foreground
// grandchild, as launch.sh runs java, which writes its pid to a file. Each case bounds its own wait on the tool, so a
// regression fails the case instead of hanging the test run.
[TestClass]
public sealed class EmulatorBoundTests
{
    const string Log = "STANDIN-LOG";

    const string ScriptOpens = "echo 'INFO  SCRIPT: /scripts/EmulateMapStart.java (HeadlessAnalyzer)'";

    static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    string _folder = "";

    string Game => Path.Combine(_folder, "game");

    string Grandchild => Path.Combine(_folder, "grandchild.pid");

    string ProjectRecord => Path.Combine(_folder, "project.txt");

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "emulator-bound-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Game);
        File.WriteAllBytes(Path.Combine(Game, "Siedler1.exe"), [1, 2, 3]);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    static string Script(string line) => $"echo 'INFO  EmulateMapStart.java> {line} (GhidraScript)'";

    static string Ended(int command) => Script($"EMU command={command} ended");

    // A foreground grandchild that writes its pid and runs on: a sleep, or a loop that prints Ghidra lines meanwhile.
    string Hang(bool printing = false) =>
        printing
            ? $"/bin/sh -c 'echo $$ > \"{Grandchild}\"; while :; do echo \"INFO  tick (Ghidra)\"; /bin/sleep 0.2; done'"
            : $"/bin/sh -c 'echo $$ > \"{Grandchild}\"; exec /bin/sleep 60'";

    // The stand-in records the project folder emulate.py hands it, then runs the body.
    string StandIn(string body) =>
        PythonTool.StandInHeadless(
            Path.Combine(_folder, "bin"),
            $"""
            printf '%s' "$1" > '{ProjectRecord}'
            {body}

            """
        );

    string[] Arguments(string[] options, string[] commands) =>
        [PythonTool.InRepo("tools", "GhidraDump", "emulate.py"), "--game", Game, .. options, .. commands];

    ToolRun Call(string body, string[] commands, params string[] options) =>
        PythonTool.RunOnPath(_folder, Arguments(options, commands), [StandIn(body)], null, Limit);

    void AssertLaunchGone()
    {
        Assert.IsTrue(File.Exists(Grandchild), "the stand-in never reached its hang");
        var pid = int.Parse(File.ReadAllText(Grandchild).Trim());
        Assert.IsTrue(PythonTool.Gone(pid), $"the grandchild {pid} of the launch still runs");
        var project = File.ReadAllText(ProjectRecord);
        Assert.IsFalse(Directory.Exists(project), $"the temporary project folder {project} stays");
    }

    static void AssertBound(ToolRun run, string what)
    {
        Assert.AreNotEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("", run.Stdout);
        StringAssert.Contains(run.Stderr, $"emulate.py: {what} ran past the bound of 1 s");
        StringAssert.Contains(run.Stderr, Log);
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void AnEmulatorCommandThatNeverReturnsFailsTheCallAfterTheBound()
    {
        var run = Call($"{ScriptOpens}\necho '{Log}'\n{Hang()}", ["rng:0,0,0"], "--timeout", "1");

        AssertBound(run, "command 1 (rng:0,0,0)");
        AssertLaunchGone();
    }

    [TestMethod]
    public void AnImportThatNeverEndsFailsTheCallAndIsNamed()
    {
        var run = Call($"echo '{Log}'\n{Hang()}", ["rng:0,0,0", "rng:1,0,0"], "--timeout", "1");

        AssertBound(run, "the import");
        AssertLaunchGone();
    }

    [TestMethod]
    public void ALaterCommandThatHangsWhileGhidraPrintsFailsTheCallAfterTheBound()
    {
        var run = Call(
            string.Join('\n', ScriptOpens, Script("EMU rng out=1"), Ended(1), $"echo '{Log}'", Hang(printing: true)),
            ["rng:0,0,0", "gen:1:0,0,0", "rng:1,0,0"],
            "--timeout",
            "1"
        );

        AssertBound(run, "command 2 (gen:1:0,0,0)");
        AssertLaunchGone();
    }

    [TestMethod]
    public void TheBoundRestartsWithEachCommand()
    {
        // Each command ends 1.2 s after the one before it, within a bound of 2 s; together they run past it.
        var run = Call(
            string.Join(
                '\n',
                ScriptOpens,
                "/bin/sleep 1.2",
                Script("EMU rng out=1"),
                Ended(1),
                "/bin/sleep 1.2",
                Ended(2),
                "/bin/sleep 1.2",
                Ended(3)
            ),
            ["rng:0,0,0", "peek:0:0", "rng:1,0,0"],
            "--timeout",
            "2"
        );

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("EMU rng out=1\n", run.Stdout.Replace("\r", ""));
    }

    [TestMethod]
    [DataRow("--timeout", "5", "<game>")]
    [DataRow("<game>", "--project", "<project>", "--timeout", "5")]
    [DataRow("--project", "<project>", "--timeout", "5", "<game>")]
    public void TimeoutIsTakenInAnyOrderAmongTheFlags(params string[] order)
    {
        var bin = StandIn(string.Join('\n', ScriptOpens, Script("EMU rng out=1"), Ended(1)));
        var options = new System.Collections.Generic.List<string>();
        foreach (var part in order)
        {
            options.AddRange(
                part switch
                {
                    "<game>" => ["--game", Game],
                    "<project>" => [Path.Combine(_folder, "project")],
                    _ => [part],
                }
            );
        }

        var run = PythonTool.RunOnPath(
            _folder,
            [PythonTool.InRepo("tools", "GhidraDump", "emulate.py"), .. options, "rng:0,0,0"],
            [bin],
            null,
            Limit
        );

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("EMU rng out=1\n", run.Stdout.Replace("\r", ""));
    }

    // Python takes SIGINT as KeyboardInterrupt, and emulate.py takes SIGHUP, only where the signal was not ignored
    // when it started: a test host under nohup, or a background job of a shell without job control, passes that
    // ignore on.
    static void InconclusiveUnlessHeeded(string signal)
    {
        var probe = PythonTool.Run(
            Path.GetTempPath(),
            ["-c", $"import signal; print(signal.getsignal(signal.SIG{signal}) == signal.SIG_IGN)"]
        );
        if (probe.Stdout.Trim() == "True")
        {
            Assert.Inconclusive($"SIG{signal} was ignored when the test host started, so emulate.py keeps ignoring it");
        }
    }

    [TestMethod]
    [DataRow("INT")]
    [DataRow("TERM")]
    [DataRow("HUP")]
    public void ASignalToTheCallWhileGhidraRunsEndsTheLaunch(string signal)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the case sends a POSIX signal");
        }

        if (signal != "TERM")
        {
            InconclusiveUnlessHeeded(signal);
        }

        var call = StartHung($"{ScriptOpens}\n{Hang()}");

        using (var kill = Process.Start("sh", ["-c", "kill -s \"$1\" \"$2\"", "sh", signal, call.Id.ToString()])!)
        {
            kill.WaitForExit();
            Assert.AreEqual(0, kill.ExitCode, $"kill -s {signal} failed");
        }

        var run = PythonTool.Finish(call, Limit);

        Assert.AreNotEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("", run.Stdout);
        AssertLaunchGone();
    }

    // Starts a call whose stand-in runs the body, and returns once the stand-in reached its hang.
    Process StartHung(string body)
    {
        var call = PythonTool.StartOnPath(
            _folder,
            Arguments(["--timeout", "60"], ["rng:0,0,0"]),
            [StandIn(body)],
            null
        );
        var watch = Stopwatch.StartNew();
        while (!File.Exists(Grandchild) || File.ReadAllText(Grandchild).Trim() == "")
        {
            if (watch.Elapsed > Limit)
            {
                call.Kill(true);
                Assert.Fail("the stand-in never reached its hang");
            }

            Thread.Sleep(50);
        }

        return call;
    }

    // A signal that repeats while the call cleans up does not cut the cleanup short. The stand-in fills the project
    // folder with files before it hangs, so removing the folder takes long enough for a burst of SIGTERM to land in it.
    [TestMethod]
    public void SignalsThatRepeatDuringTheCleanupLeaveNoProjectFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the case sends a POSIX signal");
        }

        var call = StartHung(
            $"{ScriptOpens}\n/bin/mkdir \"$1/rep\" && cd \"$1/rep\" && /usr/bin/seq 1 4000 | /usr/bin/xargs /usr/bin/touch && {Hang()}"
        );

        using (
            var burst = Process.Start(
                "sh",
                [
                    "-c",
                    "i=0; while [ $i -lt 400 ] && kill -s TERM \"$1\" 2>/dev/null; do i=$((i+1)); /bin/sleep 0.005; done",
                    "sh",
                    call.Id.ToString(),
                ]
            )!
        )
        {
            burst.WaitForExit();
        }

        var run = PythonTool.Finish(call, Limit);

        Assert.AreNotEqual(0, run.Exit, run.Stderr);
        AssertLaunchGone();
    }

    // The same check as the first case with an analyzeHeadless.cmd, on a Windows host only: a cmd stand-in runs
    // powershell as its foreground child, as analyzeHeadless.bat runs launch.bat and java.
    [TestMethod]
    public void OnWindowsAnEmulatorCommandThatNeverReturnsEndsTheWholeLaunch()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the stand-in analyzeHeadless.cmd runs on Windows only");
        }

        var bin = Path.Combine(_folder, "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(
            Path.Combine(bin, "analyzeHeadless.cmd"),
            $"""
            @echo off
            <nul set /p ="%~1" > "{ProjectRecord}"
            echo INFO  SCRIPT: /scripts/EmulateMapStart.java (HeadlessAnalyzer)
            echo {Log}
            powershell -NoProfile -Command "$PID | Out-File -Encoding ascii '{Grandchild}'; Start-Sleep 60"

            """
        );
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);

        var run = PythonTool.RunOnPath(
            _folder,
            Arguments(["--timeout", "1"], ["rng:0,0,0"]),
            [bin, system, Path.Combine(system, "WindowsPowerShell", "v1.0")],
            null,
            Limit
        );

        AssertBound(run, "command 1 (rng:0,0,0)");
        AssertLaunchGone();
    }
}
