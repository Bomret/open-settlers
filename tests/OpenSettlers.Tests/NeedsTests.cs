using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The focused command (mise.toml, tasks.test) filters out exactly the categories of Needs; the full command filters none.
[TestClass]
public sealed class NeedsTests
{
    private static string MiseToml()
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
        var expected = typeof(Needs).GetFields().Select(f => $"TestCategory!={f.GetRawConstantValue()}").Order().ToArray();
        var filter = Regex.Match(MiseToml(), @"\[tasks\.test\][^\[]*?--filter '([^']*)'").Groups[1].Value;

        CollectionAssert.AreEqual(expected, filter.Split('&').Order().ToArray());
    }

    [TestMethod]
    public void TheFullCommandFiltersNoCategory()
    {
        var check = MiseToml().Split("[tasks.check]")[1].Split("\n[")[0];

        Assert.IsFalse(check.Contains("--filter", StringComparison.Ordinal));
    }
}
