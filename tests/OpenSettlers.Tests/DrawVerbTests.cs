using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The command line of the emulator verb draw (tools/GhidraDump/emulate.py, EmulateDraw.java): it exits non-zero and prints
// nothing, so no recording is written, where the install or its program is missing or is not the reference build, where a pass
// of a window draws no settler, or where the run reaches no window pass. A successful run prints the install line and the
// lines of the recording. The runs that need Ghidra and the install are inconclusive without them.
[TestClass]
public sealed class DrawVerbTests
{
    // The carrier game of the recordings (goods:, a log laid on the hut's flag before pass 5): the carrier is on its road at
    // pass 100, the first pass the view finds a settler in; no settler is out before pass 30.
    private const string Game = "goods:1:1234,5678,9ABC:0:18,20:16,20:19,21,343:16,20,2:102:0::-1:5,0,7";

    private static string _project = "";

    private string _folder = "";

    // One Ghidra project for every run of the class that reaches the install: the first run imports the program, the others
    // run against the import. The Ghidra tests share one batch run in it.
    [ClassInitialize]
    public static void InitializeClass(TestContext _)
    {
        _project = NewFolder();
        _batch = new(() => Lines(Emulate(ReferenceInstall(), Commands, _project).Output));
    }

    [ClassCleanup]
    public static void CleanupClass() => Directory.Delete(_project, true);

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "draw-verb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        return folder;
    }

    [TestInitialize]
    public void Initialize()
    {
        _folder = NewFolder();
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "GhidraDump", "emulate.py")))
        {
            dir =
                Path.GetDirectoryName(dir)
                ?? throw new InvalidOperationException("tools/GhidraDump/emulate.py not found");
        }

        return dir;
    }

    private static (int Exit, string Output) Emulate(
        string game,
        string command,
        string? project = null,
        string? path = null
    ) => Emulate(game, [command], project, path);

    private static (int Exit, string Output) Emulate(
        string game,
        string[] commands,
        string? project = null,
        string? path = null
    )
    {
        var info = new ProcessStartInfo("python3")
        {
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-I");
        info.ArgumentList.Add("tools/GhidraDump/emulate.py");
        info.ArgumentList.Add("--game");
        info.ArgumentList.Add(game);
        if (project is not null)
        {
            info.ArgumentList.Add("--project");
            info.ArgumentList.Add(project);
        }

        foreach (var command in commands)
        {
            info.ArgumentList.Add(command);
        }

        if (path is not null)
        {
            info.Environment["PATH"] = path + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        }

        try
        {
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output.Result);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Inconclusive("python3 is not on the path");
            throw;
        }
    }

    // Ghidra on the path and the reference install: the runs that reach the emulator need both.
    private static string ReferenceInstall()
    {
        if (
            !(Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator)
                .Any(dir => File.Exists(Path.Combine(dir, "analyzeHeadless")))
        )
        {
            Assert.Inconclusive("analyzeHeadless is not on the path");
        }

        var install = TestProgram.OpenInstall();

        return install.Root;
    }

    [TestMethod]
    public void ARunWithNoInstallExitsNonZeroAndPrintsNothing()
    {
        var (exit, output) = Emulate(Path.Combine(_folder, "missing"), $"draw:100-101:17,20:{Game}");

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void ARunOnAnInstallWithNoProgramExitsNonZeroAndPrintsNothing()
    {
        var (exit, output) = Emulate(_folder, $"draw:100-101:17,20:{Game}");

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void ARunOnAProgramOfAnotherBuildExitsNonZeroAndPrintsNothing()
    {
        File.WriteAllText(Path.Combine(_folder, "Siedler1.exe"), "not the history edition");

        var (exit, output) = Emulate(_folder, $"draw:100-101:17,20:{Game}");

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
    }

    // The commands of the Ghidra tests, run as one batch the first time a test that reaches the install asks for it.
    private const string NoSettlerAtPass3 = $"draw:3-4:17,20:{Game}";
    private const string BeyondTheLastPass = $"draw:500-501:17,20:{Game}";
    private const string Reference = $"draw:100-101:17,20:{Game}";

    private static readonly string[] Commands = [NoSettlerAtPass3, BeyondTheLastPass, Reference];

    // Started by the first test that reaches the install, at most once per class run.
    private static Lazy<string[]> _batch = null!;

    // The command's result in the class's batch: whether it ended, and its lines under the install line.
    private static (bool Ended, string[] Lines) BatchResult(string command)
    {
        ReferenceInstall();
        var lines = _batch.Value;
        var index = Array.IndexOf(Commands, command) + 1;
        var start = Array.FindIndex(lines, line => line.StartsWith($"EMU draw command={index} "));
        if (start < 0)
        {
            Assert.Fail($"the batch gave no result for {command}");
        }

        var rest = lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("EMU draw command=")).ToArray();

        return (lines[start].EndsWith(" ended"), [lines[0], .. rest]);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    [TestCategory(Needs.Ghidra)]
    public void ARunWhoseWindowHoldsAPassWithNoSettlerDrawExitsNonZeroAndPrintsNothing()
    {
        var (ended, lines) = BatchResult(NoSettlerAtPass3);

        Assert.IsFalse(ended, "no settler is out at pass 3");
        Assert.AreEqual(1, lines.Length, "no recording under the install line");
        Assert.IsTrue(BatchResult(Reference).Ended, "the batch itself ran");
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    [TestCategory(Needs.Ghidra)]
    public void ARunWhoseWindowsAreBeyondItsLastPassReachesNoDrawPathAndExitsNonZero()
    {
        var (ended, lines) = BatchResult(BeyondTheLastPass);

        Assert.IsFalse(ended);
        Assert.AreEqual(1, lines.Length, "no recording under the install line");
        Assert.IsTrue(BatchResult(Reference).Ended, "the batch itself ran");
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    [TestCategory(Needs.Ghidra)]
    public void ARunOnTheReferenceInstallPrintsTheInstallLineAndOneLineForEachPassOfItsWindow()
    {
        var (ended, lines) = BatchResult(Reference);

        Assert.IsTrue(ended);
        Assert.IsTrue(
            File.Exists(Path.Combine(_project, "program.sha256")),
            "the first real run records the import (Ghidra's \"REPORT: Import succeeded\" line)"
        );
        StringAssert.StartsWith(lines[0], "EMU draw install=History Edition ");
        CollectionAssert.AreEqual(
            new[] { "EMU draw pass=100 clock=808", "EMU draw pass=101 clock=816" },
            lines
                .Where(line => line.Contains(" pass="))
                .Select(line => string.Join(' ', line.Split(' ').Take(4)))
                .ToArray()
        );
        Assert.IsTrue(lines.All(line => !line.Contains(" pass=") || line.Contains(" d=")));
        StringAssert.StartsWith(lines[^1], "EMU draw end");
    }

    // A stand-in for analyzeHeadless that logs its arguments, makes the project file an import makes, and prints a line the
    // script's output filter keeps. Without Ghidra these runs show which program the project holds.
    private string FakeHeadless()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the stand-in is a shell script");
        }

        var bin = Path.Combine(_folder, "bin");
        Directory.CreateDirectory(bin);
        var script = Path.Combine(bin, "analyzeHeadless");
        var log = Path.Combine(_folder, "calls.log");
        File.WriteAllText(
            script,
            $"#!/bin/sh\necho \"$*\" >> \"{log}\"\nfor a in \"$@\"; do [ \"$a\" = -import ] && {{ touch \"$1/Emu.gpr\"; echo 'INFO  REPORT: Import succeeded (HeadlessAnalyzer)'; }}; done\necho 'EmulateMapStart.java> EMU ok'\n"
        );
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return bin;
    }

    private string[] Calls() => File.ReadAllLines(Path.Combine(_folder, "calls.log"));

    private string Install(string content)
    {
        var install = Path.Combine(_folder, "install");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "Siedler1.exe"), content);

        return install;
    }

    [TestMethod]
    public void ARunWithoutAProjectImportsIntoATemporaryProjectAndDeletesIt()
    {
        var bin = FakeHeadless();

        var (exit, output) = Emulate(Install("a"), "rng:0000,0000,0000", path: bin);

        Assert.AreEqual(0, exit);
        Assert.AreEqual("EMU ok\n", output.Replace("\r", ""));
        var call = Calls().Single();
        StringAssert.Contains(call, "-import");
        StringAssert.Contains(call, "-deleteProject");
    }

    [TestMethod]
    public void ASecondRunInAProjectRunsAgainstTheImportedProgram()
    {
        var bin = FakeHeadless();
        var install = Install("a");
        var project = Path.Combine(_folder, "project");

        var (firstExit, firstOutput) = Emulate(install, "rng:0000,0000,0000", project, bin);
        var (exit, output) = Emulate(install, "rng:0000,0000,0000", project, bin);

        Assert.AreEqual(0, firstExit);
        Assert.AreEqual("EMU ok\n", firstOutput.Replace("\r", ""));
        Assert.AreEqual(0, exit);
        Assert.AreEqual("EMU ok\n", output.Replace("\r", ""));
        var calls = Calls();
        Assert.AreEqual(2, calls.Length);
        StringAssert.Contains(calls[0], "-import");
        StringAssert.Contains(calls[1], "-process Siedler1.exe");
        Assert.IsFalse(calls[1].Contains("-import"));
        Assert.IsFalse(calls[1].Contains("-deleteProject"));
        Assert.IsFalse(calls[0].Contains("-deleteProject"));
    }

    [TestMethod]
    public void AProjectOfAnotherProgramIsImportedAgain()
    {
        var bin = FakeHeadless();
        var project = Path.Combine(_folder, "project");

        Emulate(Install("a"), "rng:0000,0000,0000", project, bin);
        Emulate(Install("b"), "rng:0000,0000,0000", project, bin);

        var calls = Calls();
        Assert.AreEqual(2, calls.Length);
        Assert.IsTrue(calls.All(call => call.Contains("-import") && !call.Contains("-process")));
    }

    [TestMethod]
    public void AFirstRunInAFolderWithOtherFilesKeepsThem()
    {
        var bin = FakeHeadless();
        var project = Path.Combine(_folder, "project");
        Directory.CreateDirectory(project);
        var other = Path.Combine(project, "notes.txt");
        File.WriteAllText(other, "mine");

        Emulate(Install("a"), "rng:0000,0000,0000", project, bin);

        Assert.IsTrue(File.Exists(other));
    }

    [TestMethod]
    public void AProjectWithTheMarkerButNoProjectFileIsImportedAgain()
    {
        var bin = FakeHeadless();
        var project = Path.Combine(_folder, "project");

        Emulate(Install("a"), "rng:0000,0000,0000", project, bin);
        File.Delete(Path.Combine(project, "Emu.gpr"));
        Emulate(Install("a"), "rng:0000,0000,0000", project, bin);

        var calls = Calls();
        Assert.AreEqual(2, calls.Length);
        Assert.IsTrue(calls.All(call => call.Contains("-import") && !call.Contains("-process")));
    }

    // A stand-in for analyzeHeadless in a draw batch: it logs its arguments and, for each -postScript, prints the line Ghidra
    // opens a script run with, then a recording that ends, or, for a command holding "fail", a partial one that does not.
    // With crashes, it exits 1 after the first script run, as a Ghidra that dies part-way.
    private string FakeDrawHeadless(bool importFails = false, bool crashes = false)
    {
        var bin = FakeHeadless();
        var log = Path.Combine(_folder, "calls.log");
        var run = importFails
            ? "echo 'ERROR REPORT: Import failed for file' \nexit 1\n"
            : "for a in \"$@\"; do [ \"$a\" = -import ] && { touch \"$1/Emu.gpr\"; echo 'INFO  REPORT: Import succeeded (HeadlessAnalyzer)'; }; done\n"
                + "while [ $# -gt 0 ]; do\n"
                + "  if [ \"$1\" = -postScript ]; then\n"
                + "    echo \"INFO  SCRIPT: /scripts/$2 (HeadlessAnalyzer)  \"\n"
                + "    case \"$3\" in\n"
                + "      *fail*) echo \"INFO  $2> EMU draw pass=1 partial (GhidraScript)\" ;;\n"
                + "      *) echo \"INFO  $2> EMU draw pass=1 $3 (GhidraScript)\"; echo \"INFO  $2> EMU draw end passes=1 (GhidraScript)\" ;;\n"
                + "    esac\n"
                + (crashes ? "    exit 1\n" : "")
                + "    shift 3\n"
                + "  else shift; fi\n"
                + "done\n";
        File.WriteAllText(Path.Combine(bin, "analyzeHeadless"), $"#!/bin/sh\necho \"$*\" >> \"{log}\"\n" + run);

        return bin;
    }

    private static string[] Lines(string output) =>
        output.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ABatchOfDrawCommandsStartsOneLaunchWithOneScriptPerCommandInArgumentOrder()
    {
        var bin = FakeDrawHeadless();

        var (exit, _) = Emulate(TestProgram.OpenInstall().Root, ["draw:1-1:0,0:a", "draw:2-2:0,0:b"], path: bin);

        Assert.AreEqual(0, exit);
        var call = Calls().Single();
        StringAssert.EndsWith(
            call.TrimEnd(),
            "-postScript EmulateDraw.java draw:1-1:0,0:a -postScript EmulateDraw.java draw:2-2:0,0:b"
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ABatchGivesEachCommandItsOwnResultInArgumentOrderAndExitsNonZeroWhenOneFails()
    {
        var bin = FakeDrawHeadless();

        var (exit, output) = Emulate(
            TestProgram.OpenInstall().Root,
            ["draw:1-1:0,0:a", "draw:2-2:0,0:fail", "draw:1-1:0,0:a"],
            path: bin
        );

        Assert.AreNotEqual(0, exit);
        var lines = Lines(output);
        StringAssert.StartsWith(lines[0], "EMU draw install=History Edition ");
        CollectionAssert.AreEqual(
            new[]
            {
                "EMU draw command=1 ended",
                "EMU draw pass=1 draw:1-1:0,0:a",
                "EMU draw end passes=1",
                "EMU draw command=2 failed",
                "EMU draw command=3 ended",
                "EMU draw pass=1 draw:1-1:0,0:a",
                "EMU draw end passes=1",
            },
            lines[1..]
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EachCommandOfABatchRunsInAScriptRunOfItsOwn()
    {
        var bin = FakeDrawHeadless();
        string[] commands = ["draw:1-1:0,0:a", "draw:2-2:0,0:b", "draw:3-3:0,0:c"];

        Emulate(TestProgram.OpenInstall().Root, commands, path: bin);

        var scripts = Calls().Single().Split("-postScript ")[1..].Select(part => part.Trim()).ToArray();
        CollectionAssert.AreEqual(commands.Select(command => $"EmulateDraw.java {command}").ToArray(), scripts);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ABatchWhoseLaunchCrashesPartWayReportsTheRestFailed()
    {
        var bin = FakeDrawHeadless(crashes: true);

        var (exit, output) = Emulate(TestProgram.OpenInstall().Root, ["draw:1-1:0,0:a", "draw:2-2:0,0:b"], path: bin);

        Assert.AreNotEqual(0, exit);
        CollectionAssert.AreEqual(
            new[]
            {
                "EMU draw command=1 ended",
                "EMU draw pass=1 draw:1-1:0,0:a",
                "EMU draw end passes=1",
                "EMU draw command=2 failed",
            },
            Lines(output)[1..]
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ABatchWhoseImportFailsReportsEveryCommandFailed()
    {
        var bin = FakeDrawHeadless(importFails: true);

        var (exit, output) = Emulate(TestProgram.OpenInstall().Root, ["draw:1-1:0,0:a", "draw:2-2:0,0:b"], path: bin);

        Assert.AreNotEqual(0, exit);
        CollectionAssert.AreEqual(
            new[] { "EMU draw command=1 failed", "EMU draw command=2 failed" },
            Lines(output)[1..]
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ABatchInAProjectImportsOnceAndThenRunsAgainstTheImport()
    {
        var bin = FakeDrawHeadless();
        var project = Path.Combine(_folder, "project");
        string[] commands = ["draw:1-1:0,0:a", "draw:2-2:0,0:b"];

        var (firstExit, _) = Emulate(TestProgram.OpenInstall().Root, commands, project, bin);
        var (exit, output) = Emulate(TestProgram.OpenInstall().Root, commands, project, bin);

        Assert.AreEqual(0, firstExit);
        Assert.AreEqual(0, exit);
        Assert.AreEqual(7, Lines(output).Length, "the install line, then two ended results of three lines each");
        var calls = Calls();
        StringAssert.Contains(calls[0], "-import");
        StringAssert.Contains(calls[1], "-process Siedler1.exe");
        Assert.IsTrue(calls.All(call => call.Split("-postScript").Length == 3));
    }

    [TestMethod]
    public void ABatchOnAProgramOfAnotherBuildExitsNonZeroAndPrintsNothing()
    {
        var bin = FakeDrawHeadless();

        var (exit, output) = Emulate(
            Install("not the history edition"),
            ["draw:1-1:0,0:a", "draw:2-2:0,0:b"],
            path: bin
        );

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
        Assert.IsFalse(File.Exists(Path.Combine(_folder, "calls.log")), "no launch");
    }
}
