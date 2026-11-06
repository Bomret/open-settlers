using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Every layouts/*.jsonc of the repository reads and applies to a game without a refusal. A file that does not load fails
// the test and names the file and the entry.
[TestClass]
public sealed class CheckedInLayoutsTests
{
    static string LayoutsFolder()
    {
        var dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "layouts")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("layouts/ not found");
        }

        return Path.Combine(dir, "layouts");
    }

    // The first problem of a layout's text, or null when it reads and every entry applies.
    static string? Problem(string text, string file)
    {
        var read = Layout.Read(text, file);
        if (read.Layout is null)
        {
            return string.Join("; ", read.Problems);
        }

        return read.Layout.Apply(LayoutSettlersTests.NewGame(read.Layout));
    }

    [TestMethod]
    public void EveryLayoutOfTheRepositoryLoadsAndApplies()
    {
        var files = Directory.GetFiles(LayoutsFolder(), "*.jsonc");
        Assert.IsTrue(files.Length >= 2, "layouts/ holds the woodcutter and carrier layouts");

        var problems = files.Select(file => Problem(File.ReadAllText(file), Path.GetFileName(file))).OfType<string>();

        Assert.AreEqual("", string.Join("\n", problems));
    }

    [TestMethod]
    public void ALayoutThatDoesNotLoadIsAProblemNamingTheFile()
    {
        Assert.IsTrue(Problem("{ \"size\": 1 }", "broken.jsonc")!.StartsWith("broken.jsonc: "));
    }

    [TestMethod]
    public void ALayoutWhoseEntryTheGameRefusesIsAProblemNamingTheFileAndEntry()
    {
        var text = LayoutTests.Text(
            extra: "\n\"castle\": {\"x\": 10, \"y\": 10}, \"buildings\": [{\"x\": 0, \"y\": 0}]"
        );

        Assert.IsTrue(Problem(text, "refused.jsonc")!.StartsWith("refused.jsonc: buildings[0]: "));
    }
}
