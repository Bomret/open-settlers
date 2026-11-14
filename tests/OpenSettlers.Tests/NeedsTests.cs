using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The focused command (mise.toml, tasks.test) and the full command (tasks.check) filter out exactly the categories of Needs;
// the integration command (tasks."test:integration") selects exactly them.
[TestClass]
public sealed class NeedsTests
{
    static string MiseToml()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "mise.toml")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("mise.toml not found");
        }

        return File.ReadAllText(Path.Combine(dir, "mise.toml"));
    }

    [TestMethod]
    public void TheFocusedFilterNamesExactlyTheCategoriesOfNeeds()
    {
        var expected = typeof(Needs)
            .GetFields()
            .Select(f => $"TestCategory!={f.GetRawConstantValue()}")
            .Order()
            .ToArray();
        var filter = Regex.Match(MiseToml(), @"\[tasks\.test\][^\[]*?--filter '([^']*)'").Groups[1].Value;

        CollectionAssert.AreEqual(expected, filter.Split('&').Order().ToArray());
    }

    // The --filter of the task's section, which ends at the next line that opens a section.
    static string Filter(string task) =>
        Regex.Match(MiseToml().Split($"[tasks.{task}]")[1].Split("\n[")[0], "--filter '([^']*)'").Groups[1].Value;

    static string[] Categories(string prefix) =>
        [.. typeof(Needs).GetFields().Select(f => $"{prefix}{f.GetRawConstantValue()}").Order()];

    [TestMethod]
    public void TheFullCommandRunsTheFocusedFilter()
    {
        CollectionAssert.AreEqual(Categories("TestCategory!="), Filter("check").Split('&').Order().ToArray());
    }

    [TestMethod]
    public void TheIntegrationFilterSelectsExactlyTheCategoriesOfNeeds()
    {
        CollectionAssert.AreEqual(
            Categories("TestCategory="),
            Filter("\"test:integration\"").Split('|').Order().ToArray()
        );
    }
}
