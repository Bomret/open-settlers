using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// tools/GhidraDump/headless.py, the entry `mise run ghidra:natives|ghidra:import|ghidra:update|ghidra:dump [index-only]`
// runs, run as a process in a fresh temporary folder, which it takes as the repository root. The child's path holds only
// the folders of the stand-in analyzeHeadless and gradle, shell scripts that record their arguments and working directory
// with shell builtins alone. No case reaches Ghidra, Gradle, the install, or the repository's re-local folder.
[TestClass]
public sealed class HeadlessEntryTests
{
    const string Usage = "headless.py: Usage: python3 -I headless.py natives | import | update | dump [index-only]";

    string _folder = "";

    string Root => Path.Combine(_folder, "root");

    string Bin => Path.Combine(_folder, "bin");

    string GradleBin => Path.Combine(_folder, "gradle-bin");

    string Record => Path.Combine(_folder, "record");

    string Game => Path.Combine(_folder, "game");

    string Dump => Path.Combine(Root, "re-local", "dump");

    string Project => Path.Combine(Root, "re-local", "ghidra");

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "headless-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Record);
        Directory.CreateDirectory(Path.Combine(Bin, "gradle"));
        Directory.CreateDirectory(GradleBin);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    static string Entry() => PythonTool.InRepo("tools", "GhidraDump", "headless.py");

    // The stand-in writes its working directory, its arguments one to a line, and whether the dump folder and the file
    // old.txt in it exist, then exits with the given status.
    void StandInHeadless(int exit = 0) =>
        PythonTool.StandIn(
            Bin,
            "analyzeHeadless",
            $"""
            pwd -P > '{Record}/headless.cwd'
            printf '%s\n' "$@" > '{Record}/headless.args'
            if [ -d re-local/dump ]; then echo dump > '{Record}/dump.state'; else echo none > '{Record}/dump.state'; fi
            if [ -e re-local/dump/old.txt ]; then echo old >> '{Record}/dump.state'; fi
            exit {exit}

            """
        );

    void StandInGradle(int exit = 0) =>
        PythonTool.StandIn(
            GradleBin,
            "gradle",
            $"""
            pwd -P > '{Record}/gradle.cwd'
            printf '%s\n' "$@" > '{Record}/gradle.args'
            exit {exit}

            """
        );

    ToolRun Run(string[] arguments, bool headless, bool gradle, string? siedlerDir = null) =>
        PythonTool.RunOnPath(
            Root,
            [Entry(), .. arguments],
            [.. headless ? new[] { Bin } : [], .. gradle ? new[] { GradleBin } : []],
            siedlerDir
        );

    string[] Recorded(string name) => File.ReadAllLines(Path.Combine(Record, name));

    // The root as the entry sees it: the stand-in's physical working directory, the temporary folder with its links
    // resolved. The marker shows it is the same folder.
    string RootSeen()
    {
        var root = Recorded("headless.cwd")[0];
        File.WriteAllText(Path.Combine(Root, "marker"), "");
        Assert.IsTrue(File.Exists(Path.Combine(root, "marker")), root);

        return root;
    }

    void AssertStartedNothing() =>
        Assert.AreEqual(0, Directory.GetFiles(Record).Length, string.Join(", ", Directory.GetFiles(Record)));

    static void AssertRefused(ToolRun run, string message)
    {
        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, message);
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    static string[] ApplyIndex(string root) =>
        [
            "-postScript",
            "CreateMissingFunctions.java",
            "-postScript",
            "ApplyIndexNames.java",
            Path.Combine(root, "docs", "rules", "program-index.md"),
        ];

    static string[] Scripts(string root) => ["-scriptPath", Path.Combine(root, "tools", "GhidraDump")];

    [TestMethod]
    public void NativesRunsGradleBuildNativesInTheGradleFolderNextToAnalyzeHeadless()
    {
        StandInHeadless();
        StandInGradle(exit: 3);

        var run = Run(["natives"], headless: true, gradle: true);

        Assert.AreEqual(3, run.Exit, run.Stderr);
        CollectionAssert.AreEqual(new[] { "buildNatives" }, Recorded("gradle.args"));
        File.WriteAllText(Path.Combine(Bin, "gradle", "marker"), "");
        Assert.IsTrue(File.Exists(Path.Combine(Recorded("gradle.cwd")[0], "marker")), Recorded("gradle.cwd")[0]);
        Assert.IsFalse(File.Exists(Path.Combine(Record, "headless.args")));
    }

    [TestMethod]
    public void ImportPassesTheProjectTheInstallsProgramTheIndexScriptsAndTheScriptPath()
    {
        StandInHeadless(exit: 3);

        var run = Run(["import"], headless: true, gradle: false, siedlerDir: Game);

        Assert.AreEqual(3, run.Exit, run.Stderr);
        var root = RootSeen();
        string[] expected =
        [
            Path.Combine(root, "re-local", "ghidra"),
            "Siedler1",
            "-import",
            Path.Combine(Game, "Siedler1.exe"),
            "-overwrite",
            .. ApplyIndex(root),
            .. Scripts(root),
        ];
        CollectionAssert.AreEqual(expected, Recorded("headless.args"));
        Assert.IsTrue(Directory.Exists(Project));
    }

    [TestMethod]
    public void UpdatePassesTheProjectTheProgramWithoutAnalysisTheIndexScriptsAndTheScriptPath()
    {
        StandInHeadless(exit: 3);

        var run = Run(["update"], headless: true, gradle: false);

        Assert.AreEqual(3, run.Exit, run.Stderr);
        var root = RootSeen();
        string[] expected =
        [
            Path.Combine(root, "re-local", "ghidra"),
            "Siedler1",
            "-process",
            "Siedler1.exe",
            "-noanalysis",
            .. ApplyIndex(root),
            .. Scripts(root),
        ];
        CollectionAssert.AreEqual(expected, Recorded("headless.args"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DumpPassesTheProgramReadOnlyAndTheDumpScriptsWithTheDumpFolder(bool indexOnly)
    {
        StandInHeadless(exit: 3);

        var run = Run(indexOnly ? ["dump", "index-only"] : ["dump"], headless: true, gradle: false);

        Assert.AreEqual(3, run.Exit, run.Stderr);
        var root = RootSeen();
        var dump = Path.Combine(root, "re-local", "dump");
        string[] expected =
        [
            Path.Combine(root, "re-local", "ghidra"),
            "Siedler1",
            "-process",
            "Siedler1.exe",
            "-noanalysis",
            "-readOnly",
            "-postScript",
            "DumpProgram.java",
            dump,
            .. indexOnly ? new[] { "index-only" } : [],
            "-postScript",
            "StateFields.java",
            dump,
            .. Scripts(root),
        ];
        CollectionAssert.AreEqual(expected, Recorded("headless.args"));
    }

    [TestMethod]
    public void AFullDumpReplacesTheOldDumpAndTheFolderExistsWhenGhidraRuns()
    {
        StandInHeadless();
        Directory.CreateDirectory(Dump);
        File.WriteAllText(Path.Combine(Dump, "old.txt"), "old");

        var run = Run(["dump"], headless: true, gradle: false);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        CollectionAssert.AreEqual(new[] { "dump" }, Recorded("dump.state"));
        Assert.IsFalse(File.Exists(Path.Combine(Dump, "old.txt")));
    }

    [TestMethod]
    public void AnIndexOnlyDumpKeepsTheOldDump()
    {
        StandInHeadless();
        Directory.CreateDirectory(Dump);
        File.WriteAllText(Path.Combine(Dump, "old.txt"), "old");

        var run = Run(["dump", "index-only"], headless: true, gradle: false);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        CollectionAssert.AreEqual(new[] { "dump", "old" }, Recorded("dump.state"));
        Assert.IsTrue(File.Exists(Path.Combine(Dump, "old.txt")));
    }

    [TestMethod]
    public void NoModeIsRefusedWithTheUsage()
    {
        StandInHeadless();
        StandInGradle();

        var run = Run([], headless: true, gradle: true);

        AssertRefused(run, Usage);
        AssertStartedNothing();
    }

    [TestMethod]
    [DataRow("headless.py: unknown mode: bogus", "bogus")]
    [DataRow("headless.py: unknown dump mode: X", "dump", "X")]
    [DataRow("headless.py: unknown dump mode: index-only X", "dump", "index-only", "X")]
    public void AnUnknownModeIsRefusedNamingIt(string message, params string[] arguments)
    {
        StandInHeadless();
        StandInGradle();

        var run = Run(arguments, headless: true, gradle: true, siedlerDir: Game);

        AssertRefused(run, message);
        AssertStartedNothing();
    }

    [TestMethod]
    [DataRow("natives")]
    [DataRow("import")]
    [DataRow("update")]
    [DataRow("dump")]
    [DataRow("dump", "index-only")]
    public void AModeWithoutAnalyzeHeadlessOnThePathIsRefused(params string[] arguments)
    {
        StandInGradle();

        var run = Run(arguments, headless: false, gradle: true, siedlerDir: Game);

        AssertRefused(run, "headless.py: analyzeHeadless is not on PATH");
        AssertStartedNothing();
    }

    [TestMethod]
    public void NativesWithNeitherToolOnThePathNamesAnalyzeHeadless()
    {
        var run = Run(["natives"], headless: false, gradle: false);

        AssertRefused(run, "headless.py: analyzeHeadless is not on PATH");
    }

    [TestMethod]
    public void NativesWithoutGradleOnThePathIsRefused()
    {
        StandInHeadless();

        var run = Run(["natives"], headless: true, gradle: false);

        AssertRefused(run, "headless.py: gradle is not on PATH");
        AssertStartedNothing();
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow(null, false)]
    public void ImportWithoutAnInstallFolderIsRefusedNamingSiedlerDir(string? siedlerDir, bool headless)
    {
        if (headless)
        {
            StandInHeadless();
        }

        var run = Run(["import"], headless, gradle: false, siedlerDir);

        AssertRefused(run, "headless.py: set SIEDLER_DIR in mise.local.toml");
        AssertStartedNothing();
        Assert.IsFalse(Directory.Exists(Project));
    }

    [TestMethod]
    public void ADumpWithoutAnalyzeHeadlessKeepsTheOldDump()
    {
        Directory.CreateDirectory(Dump);
        File.WriteAllText(Path.Combine(Dump, "old.txt"), "old");

        var run = Run(["dump"], headless: false, gradle: false);

        AssertRefused(run, "headless.py: analyzeHeadless is not on PATH");
        Assert.IsTrue(File.Exists(Path.Combine(Dump, "old.txt")));
    }

    [TestMethod]
    [DataRow("import")]
    [DataRow("dump", "index-only")]
    public void AModeWithoutAnalyzeHeadlessCreatesNoFolder(params string[] arguments)
    {
        var run = Run(arguments, headless: false, gradle: false, siedlerDir: Game);

        AssertRefused(run, "headless.py: analyzeHeadless is not on PATH");
        Assert.IsFalse(Directory.Exists(Project));
        Assert.IsFalse(Directory.Exists(Dump));
    }
}
