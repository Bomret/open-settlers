using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The files tools/GhidraDump/program_index.py and tools/open-settlers-art.py write: where they land and what they hold.
// Each run is in a temporary folder on a copy of its input, so nothing in the repository changes.
[TestClass]
public sealed partial class ToolOutputTests
{
    string _folder = "";

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tool-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    static string Section(string text, string name)
    {
        var start = $"<!-- {name}:start -->\n";
        var from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;

        return text[from..text.IndexOf($"<!-- {name}:end -->", from, StringComparison.Ordinal)];
    }

    [GeneratedRegex(" of the \\d+ functions in the dump's game code band")]
    private static partial Regex DumpCount();

    // program_index.py rewrites its input, so it runs on a copy. Its by-address section is the whole table, and its coverage
    // line, run without a dump, holds the same counts as the checked-in line without the clause that counts the dump's
    // functions: only a Ghidra dump reproduces that count.
    [TestMethod]
    public void TheCheckedInProgramIndexSectionsAreWhatTheGeneratorWritesFromItsTopicTables()
    {
        var checkedIn = File.ReadAllText(PythonTool.InRepo("docs", "rules", "program-index.md")).Replace("\r\n", "\n");
        var copy = Path.Combine(_folder, "program-index.md");
        File.WriteAllText(copy, checkedIn);

        var run = PythonTool.Run(_folder, [PythonTool.InRepo("tools", "GhidraDump", "program_index.py"), copy]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var generated = File.ReadAllText(copy);
        Assert.AreEqual(Section(checkedIn, "by-address"), Section(generated, "by-address"));
        Assert.AreEqual(DumpCount().Replace(Section(checkedIn, "coverage"), ""), Section(generated, "coverage"));
    }

    [TestMethod]
    public void TheGeneratorNamesNoCountOfTheDumpWithoutADump()
    {
        var copy = Path.Combine(_folder, "program-index.md");
        File.WriteAllText(
            copy,
            "<!-- by-address:start -->\n<!-- by-address:end -->\n<!-- coverage:start -->\n<!-- coverage:end -->\n"
        );

        var run = PythonTool.Run(_folder, [PythonTool.InRepo("tools", "GhidraDump", "program_index.py"), copy]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var generated = File.ReadAllText(copy);
        StringAssert.Contains(Section(generated, "coverage"), "the index names 0 routines and lists 0 more");
        Assert.IsFalse(Section(generated, "coverage").Contains("functions in the dump"));
    }

    const string EmptyIndex =
        "<!-- by-address:start -->\n<!-- by-address:end -->\n<!-- coverage:start -->\n<!-- coverage:end -->\n";

    static string IndexTool() => PythonTool.InRepo("tools", "GhidraDump", "program_index.py");

    // A function table with two routines in the game code band and one outside it.
    static string DumpWithTwoBandFunctions(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "functions.tsv"),
            "address\tname\n0041da10\tA\n0047b739\tB\n00401000\tC\n"
        );

        return folder;
    }

    // A worktree under a dot folder with its own empty program index. Returns the main checkout and the worktree.
    (string Main, string Worktree) WorktreeWithAnIndex()
    {
        var (_, main, worktree) = PythonTool.Worktree(_folder);
        Directory.CreateDirectory(Path.Combine(worktree, "docs", "rules"));
        File.WriteAllText(Path.Combine(worktree, "docs", "rules", "program-index.md"), EmptyIndex);

        return (main, worktree);
    }

    [TestMethod]
    public void InAWorktreeTheGeneratorRewritesTheWorktreesIndexAndCountsTheMainCheckoutsDump()
    {
        var (main, worktree) = WorktreeWithAnIndex();
        DumpWithTwoBandFunctions(Path.Combine(main, "re-local", "dump"));

        var run = PythonTool.Run(worktree, [IndexTool(), "docs/rules/program-index.md", "re-local/dump"]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var generated = File.ReadAllText(Path.Combine(worktree, "docs", "rules", "program-index.md"));
        StringAssert.Contains(Section(generated, "coverage"), " of the 2 functions in the dump's game code band");
        Assert.IsFalse(Directory.Exists(Path.Combine(worktree, "re-local")));
    }

    [TestMethod]
    public void TheGeneratorReadsAnAbsoluteDumpFolderAsGiven()
    {
        var (_, worktree) = WorktreeWithAnIndex();
        var dump = DumpWithTwoBandFunctions(Path.Combine(_folder, "elsewhere"));

        var run = PythonTool.Run(worktree, [IndexTool(), "docs/rules/program-index.md", dump]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var generated = File.ReadAllText(Path.Combine(worktree, "docs", "rules", "program-index.md"));
        StringAssert.Contains(Section(generated, "coverage"), " of the 2 functions in the dump's game code band");
    }

    // A bare repository's common dir is repo.git, whose parent is no checkout. The stand-in git prints it and exits 0.
    [TestMethod]
    public void AGitCommonDirNotNamedDotGitLeavesTheDumpUnderTheCurrentFolder()
    {
        File.WriteAllText(Path.Combine(_folder, "program-index.md"), EmptyIndex);
        DumpWithTwoBandFunctions(Path.Combine(_folder, "re-local", "dump"));
        var git = PythonTool.StandIn(
            Path.Combine(_folder, "git-bin"),
            "git",
            $"printf '%s\\n' '{Path.Combine(_folder, "other", "repo.git")}'\n"
        );

        var run = PythonTool.Run(_folder, [IndexTool(), "program-index.md", "re-local/dump"], prependPath: git);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var generated = File.ReadAllText(Path.Combine(_folder, "program-index.md"));
        StringAssert.Contains(Section(generated, "coverage"), " of the 2 functions in the dump's game code band");
    }

    // A failed ghidra:dump leaves an empty dump folder: headless.py creates it before Ghidra runs.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AMissingOrEmptyDumpFolderStopsTheGeneratorNamingTheFolderAndKeepsTheIndex(bool emptyFolderExists)
    {
        var (main, worktree) = WorktreeWithAnIndex();
        if (emptyFolderExists)
        {
            Directory.CreateDirectory(Path.Combine(main, "re-local", "dump"));
        }

        var run = PythonTool.Run(worktree, [IndexTool(), "docs/rules/program-index.md", "re-local/dump"]);

        Assert.AreNotEqual(0, run.Exit);
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
        var message = run.Stderr.TrimEnd('\n');
        Assert.IsFalse(message.Contains('\n'), run.Stderr);
        StringAssert.Contains(message, Path.Combine("main", "re-local", "dump"));
        Assert.AreEqual(EmptyIndex, File.ReadAllText(Path.Combine(worktree, "docs", "rules", "program-index.md")));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    public void TooFewOrTooManyArgumentsStopTheGeneratorWithTheUsageLine(int count)
    {
        var copy = Path.Combine(_folder, "program-index.md");
        File.WriteAllText(copy, EmptyIndex);
        string[] arguments = count == 0 ? [] : [copy, _folder, "extra"];

        var run = PythonTool.Run(_folder, [IndexTool(), .. arguments]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "Usage: python3 -I program_index.py <program-index.md> [<dump directory>]");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
        Assert.AreEqual(EmptyIndex, File.ReadAllText(copy));
        CollectionAssert.AreEqual(new[] { copy }, Directory.GetFileSystemEntries(_folder));
    }

    // The docstring of tools/open-settlers-art.py: "It writes into content/open-settlers, or into <folder> where given",
    // with the paths relative to the working directory.
    [TestMethod]
    public void WithNoArgumentTheArtToolWritesTheContentSetUnderTheWorkingDirectory()
    {
        var run = PythonTool.Run(_folder, [PythonTool.InRepo("tools", "open-settlers-art.py")]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var set = Path.Combine(_folder, "content", "open-settlers");
        Assert.IsTrue(File.Exists(Path.Combine(set, "manifest.json")), "manifest.json");
        Assert.IsTrue(File.Exists(Path.Combine(set, "building", "castle.png")), "building/castle.png");
        Assert.AreEqual(
            "content",
            string.Join("|", Directory.GetFileSystemEntries(_folder).Select(Path.GetFileName)),
            "nothing is written outside content/open-settlers"
        );
        Assert.AreEqual(
            "open-settlers",
            string.Join("|", Directory.GetFileSystemEntries(Path.Combine(_folder, "content")).Select(Path.GetFileName))
        );
    }

    [TestMethod]
    public void WithAFolderTheArtToolWritesThereAndNotUnderTheWorkingDirectory()
    {
        var target = Path.Combine(_folder, "out");

        var run = PythonTool.Run(_folder, [PythonTool.InRepo("tools", "open-settlers-art.py"), target]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.IsTrue(File.Exists(Path.Combine(target, "manifest.json")));
        Assert.IsFalse(Directory.Exists(Path.Combine(_folder, "content")));
    }
}
