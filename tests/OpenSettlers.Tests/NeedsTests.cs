using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The focused command (mise.toml, tasks.test) filters out exactly the categories of Needs.
[TestClass]
public sealed class NeedsTests
{
    [TestMethod]
    public void TheFocusedFilterNamesExactlyTheCategoriesOfNeeds()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "mise.toml")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("mise.toml not found");
        }

        var filter = string.Join("&", typeof(Needs).GetFields().Select(c =>$"TestCategory!={c.GetRawConstantValue()}"));

        StringAssert.Contains(File.ReadAllText(Path.Combine(dir, "mise.toml")), $"--filter '{filter}'");
    }
}
