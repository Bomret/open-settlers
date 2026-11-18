using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Import.ContentSet;
using OpenSettlers.Launch;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Every layouts/*.jsonc of the repository reads and applies to a game without a refusal, under the basic content set's rules at
// the start stock level its seed draws: the conditions a play run loads it in. A file that does not load fails the test and
// names the file and the entry.
[TestClass]
public sealed class CheckedInLayoutsTests
{
    internal static string LayoutsFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
        {
            folder = folder.Parent;
        }

        return Path.Combine(
            folder?.FullName
                ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output"),
            "layouts"
        );
    }

    // The first problem of a layout's text, or null when it reads and every entry applies.
    static string? Problem(string text, string file, RuleTables? tables = null)
    {
        var read = Layout.Read(text, file);
        if (read.Layout is null)
        {
            return string.Join("; ", read.Problems);
        }

        if (tables is null)
        {
            return read.Layout.Apply(LayoutSettlersTests.NewGame(read.Layout));
        }

        // The game as the launch builds it: the start stock level and the settler rate the layout's seed draws, and the passes
        // of a played game.
        return
            LayoutLoad.Start(
                read.Layout,
                tables,
                read.Layout.BuildMap(),
                StartStock.LevelOf(read.Layout.Seed),
                StartStock.SettlerRateOf(read.Layout.Seed),
                out _,
                out var message
            ) == 0
            ? null
            : message;
    }

    [TestMethod]
    public void EveryLayoutOfTheRepositoryLoadsAndApplies()
    {
        var files = Directory.GetFiles(LayoutsFolder(), "*.jsonc");
        Assert.IsTrue(files.Length >= 2, "layouts/ holds the woodcutter and carrier layouts");

        var problems = files.Select(file => Problem(File.ReadAllText(file), Path.GetFileName(file))).OfType<string>();

        Assert.AreEqual("", string.Join("\n", problems));
    }

    // Every layout applies under the tables of one content set; the problems name each file that a set refuses.
    static string ProblemsUnder(RuleTables tables) =>
        string.Join(
            "\n",
            Directory
                .GetFiles(LayoutsFolder(), "*.jsonc")
                .Select(file => Problem(File.ReadAllText(file), Path.GetFileName(file), tables))
                .OfType<string>()
        );

    [TestMethod]
    public void EveryLayoutOfTheRepositoryAppliesUnderTheBasicSet()
    {
        var basic = Path.Combine(Path.GetDirectoryName(LayoutsFolder())!, "content", "basic");

        Assert.AreEqual("", ProblemsUnder(new OpenSettlersRuleTables(basic).Read()));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryLayoutOfTheRepositoryAppliesUnderTheHistoryEditionTables()
    {
        Assert.AreEqual("", ProblemsUnder(TestProgram.Tables()));
    }

    [TestMethod]
    public void TheForesterPlayScriptReadsWithoutAProblem()
    {
        var path = Path.Combine(Path.GetDirectoryName(LayoutsFolder())!, "play", "forester.play");

        var script = OpenSettlers.MapView.PlayScript.Parse(File.ReadAllText(path), "play/forester.play");

        Assert.AreEqual("", string.Join("\n", script.Problems));
    }

    [TestMethod]
    public void TheFarmPlayScriptReadsWithoutAProblem()
    {
        var path = Path.Combine(Path.GetDirectoryName(LayoutsFolder())!, "play", "farm.play");

        var script = OpenSettlers.MapView.PlayScript.Parse(File.ReadAllText(path), "play/farm.play");

        Assert.AreEqual("", string.Join("\n", script.Problems));
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
