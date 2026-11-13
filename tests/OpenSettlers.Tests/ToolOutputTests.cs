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
    private string _folder = "";

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tool-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    private static string Section(string text, string name)
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
