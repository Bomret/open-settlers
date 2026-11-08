using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The focused command (mise.toml, tasks.test) skips the tests that need the install or Ghidra by category name; the
// categories the tests carry are the constants of Needs. A name the filter lacks leaves its tests in the focused run.
[TestClass]
public sealed class NeedsTests
{
    private static string Task(string name)
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "mise.toml")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("mise.toml not found");
        }

        var lines = File.ReadAllLines(Path.Combine(dir, "mise.toml"));
        var start = Array.IndexOf(lines, $"[tasks.{name}]");
        Assert.AreNotEqual(-1, start, $"mise.toml has no task {name}");

        return string.Join("\n", lines.Skip(start + 1).TakeWhile(l => !l.StartsWith('[')));
    }

    private static string[] Categories() =>
        typeof(Needs).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToArray();

    [TestMethod]
    public void TheFocusedCommandSkipsEveryCategoryTheTestsCarry()
    {
        var task = Task("test");

        Assert.AreNotEqual(0, Categories().Length);
        foreach (var category in Categories())
        {
            StringAssert.Contains(task, $"TestCategory!={category}");
        }
    }

    [TestMethod]
    public void TheFullCommandFiltersNoCategory()
    {
        StringAssert.DoesNotMatch(Task("check"), new System.Text.RegularExpressions.Regex("--filter"));
    }
}
