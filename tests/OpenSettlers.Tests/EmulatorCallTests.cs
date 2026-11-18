using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// A call of tools/GhidraDump/emulate.py with non-draw commands exits 0 only where every command ended:
// EmulateMapStart.java prints the line "EMU command=N ended" after the verb of the command N returns. A failed call
// prints nothing on stdout and names the first command that did not end on stderr. A stand-in analyzeHeadless prints
// canned Ghidra output, with a dummy Siedler1.exe in a temporary install folder; no case reaches Ghidra or the install.
[TestClass]
public sealed class EmulatorCallTests
{
    const string Log = "STANDIN-LOG";

    string _folder = "";

    string Game => Path.Combine(_folder, "game");

    string Project => Path.Combine(_folder, "project");

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "emulator-call-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Game);
        File.WriteAllBytes(Path.Combine(Game, "Siedler1.exe"), [1, 2, 3]);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    // A line of the script, as Ghidra prints it.
    static string Script(string line) => $"echo 'INFO  EmulateMapStart.java> {line} (GhidraScript)'";

    static string Ended(int command) => Script($"EMU command={command} ended");

    // The stand-in prints the import report where emulate.py imports, as Ghidra does before the script runs, then the
    // canned lines, a line of Ghidra's own log, and exits with the given status.
    ToolRun Call(string[] commands, string output, int exit = 0, bool project = false)
    {
        var bin = PythonTool.StandInHeadless(
            Path.Combine(_folder, "bin"),
            $"""
            for a in "$@"; do [ "$a" = -import ] && echo 'INFO  REPORT: Import succeeded (HeadlessAnalyzer)'; done
            {output}
            echo '{Log}'
            exit {exit}

            """
        );

        return PythonTool.RunOnPath(
            _folder,
            [
                PythonTool.InRepo("tools", "GhidraDump", "emulate.py"),
                "--game",
                Game,
                .. project ? new[] { "--project", Project } : [],
                .. commands,
            ],
            [bin],
            null
        );
    }

    static void AssertFailed(ToolRun run, int command, string text)
    {
        Assert.AreNotEqual(0, run.Exit, run.Stdout);
        Assert.AreEqual("", run.Stdout);
        StringAssert.Contains(run.Stderr, $"command {command} ({text}) did not end");
        StringAssert.Contains(run.Stderr, Log);
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void ACallWhoseCommandsAllEndPrintsTheirLinesInOrderWithoutTheMarkers()
    {
        // The second command, peek, ends with no line of its own: its end marker stands alone and it counts as ended.
        var run = Call(
            ["rng:0000,0000,0000", "peek:0:0", "rng:0001,0000,0000"],
            string.Join(
                '\n',
                Script("EMU rng in=0000,0000,0000 out=1"),
                Ended(1),
                Ended(2),
                Script("EMU rng in=0001,0000,0000 out=2"),
                Script("EMU rng second line"),
                Ended(3)
            )
        );

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual(
            "EMU rng in=0000,0000,0000 out=1\nEMU rng in=0001,0000,0000 out=2\nEMU rng second line\n",
            run.Stdout.Replace("\r", "")
        );
    }

    [TestMethod]
    public void ALaterCommandThatThrowsFailsTheCallAndIsNamed()
    {
        var run = Call(
            ["rng:0000,0000,0000", "nosuchverb:1", "rng:0001,0000,0000"],
            string.Join('\n', Script("EMU rng in=0000,0000,0000 out=1"), Ended(1))
        );

        AssertFailed(run, 2, "nosuchverb:1");
    }

    [TestMethod]
    public void TheOnlyCommandThatPrintsLinesAndThenThrowsFailsTheCall()
    {
        var run = Call(["gen:1:0000,0000,0000"], Script("EMU gen size=1 pass=0"));

        AssertFailed(run, 1, "gen:1:0000,0000,0000");
    }

    [TestMethod]
    public void AFirstCommandThatDoesNotEndFailsTheCallBeforeLaterCommands()
    {
        var run = Call(["gen:1:0000,0000,0000", "rng:0000,0000,0000"], Script("EMU gen size=1 pass=0"));

        AssertFailed(run, 1, "gen:1:0000,0000,0000");
    }

    [TestMethod]
    public void AGhidraThatStopsBeforeTheScriptFailsTheCallAtTheFirstCommand()
    {
        var run = Call(["rng:0000,0000,0000"], "echo 'ERROR REPORT: Import failed'", exit: 1);

        AssertFailed(run, 1, "rng:0000,0000,0000");
    }

    [TestMethod]
    public void AGhidraThatExitsNonZeroFailsTheCallThoughEveryCommandEnded()
    {
        var run = Call(["rng:0000,0000,0000"], string.Join('\n', Script("EMU rng out=1"), Ended(1)), exit: 3);

        AssertFailed(run, 1, "rng:0000,0000,0000");
    }

    [TestMethod]
    public void ALineThatOnlyQuotesAMarkerDoesNotEndACommand()
    {
        var run = Call(
            ["rng:0000,0000,0000", "rng:0001,0000,0000", "rng:0002,0000,0000"],
            string.Join(
                '\n',
                Ended(1),
                Ended(1),
                "echo 'ERROR SCRIPT ERROR: EMU command=2 ended'",
                Script("EMU command=3 ended twice")
            )
        );

        AssertFailed(run, 2, "rng:0001,0000,0000");
    }

    [TestMethod]
    public void WithAKeptProjectAFailedCommandFailsTheCallOnTheImportTheReuseAndTheReimport()
    {
        string[] commands = ["rng:0000,0000,0000", "nosuchverb:1"];
        var output = string.Join('\n', Script("EMU rng out=1"), Ended(1));

        var imported = Call(commands, output, project: true);
        var digest = File.ReadAllText(Path.Combine(Project, "program.sha256"));
        File.WriteAllText(Path.Combine(Project, "Emu.gpr"), "");
        var reused = Call(commands, output, project: true);
        File.WriteAllBytes(Path.Combine(Game, "Siedler1.exe"), [4, 5, 6]);
        var reimported = Call(commands, output, project: true);

        AssertFailed(imported, 2, "nosuchverb:1");
        AssertFailed(reused, 2, "nosuchverb:1");
        AssertFailed(reimported, 2, "nosuchverb:1");
        Assert.AreNotEqual(digest, File.ReadAllText(Path.Combine(Project, "program.sha256")));
    }
}
