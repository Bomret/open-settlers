using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// docs/agents/CODING.md, "Access modifiers": the build fails on an access modifier that is the default where it stands. The
// rule lives in the repository's configuration, so these tests pin that configuration: IDE0040 with omit_if_default at
// warning severity for every C# file, and code style enforced in the build with warnings as errors.
[TestClass]
public sealed class CodeStyleBuildTests
{
    [TestMethod]
    public void TheEditorConfigRequiresOmittingADefaultModifier()
    {
        var settings = CSharpSection(File.ReadAllLines(Path.Combine(RootOf(), ".editorconfig")));

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "dotnet_style_require_accessibility_modifiers = omit_if_default",
                "dotnet_diagnostic.IDE0040.severity = warning",
            },
            settings
        );
    }

    [TestMethod]
    public void TheBuildEnforcesCodeStyleWithWarningsAsErrors()
    {
        var properties = XDocument.Load(Path.Combine(RootOf(), "Directory.Build.props")).Descendants("PropertyGroup");
        var values = new[] { "EnforceCodeStyleInBuild", "TreatWarningsAsErrors" }
            .Select(name => $"{name}={properties.Elements(name).Select(e => e.Value.Trim()).LastOrDefault()}")
            .ToArray();

        CollectionAssert.AreEqual(new[] { "EnforceCodeStyleInBuild=true", "TreatWarningsAsErrors=true" }, values);
    }

    static string[] CSharpSection(string[] lines) =>
        [
            .. lines
                .SkipWhile(line => line.Trim() != "[*.cs]")
                .Skip(1)
                .TakeWhile(line => !line.TrimStart().StartsWith('['))
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#') && !line.StartsWith(';')),
        ];

    static string RootOf()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "play-run.sh")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("tools/play-run.sh not found");
        }

        return dir;
    }
}
