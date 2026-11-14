using System;
using System.Collections.Generic;
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
    static readonly Dictionary<string, string> EditorConfigPins = new()
    {
        ["dotnet_style_require_accessibility_modifiers"] = "omit_if_default",
        ["dotnet_diagnostic.IDE0040.severity"] = "warning",
    };

    static readonly string[] GeneratedFolders = ["bin", "obj", ".godot", ".git", ".superdev", "node_modules"];

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

    // A later section, a nested .editorconfig, or a project file could switch the rule off past the pins above, so every
    // place that sets one of the pinned settings must set it to the pinned value.
    [TestMethod]
    public void NoEditorConfigSectionOverridesTheModifierRule()
    {
        var root = RootOf();
        var overrides = ConfigFiles(root, file => Path.GetFileName(file) == ".editorconfig")
            .SelectMany(relative =>
                File.ReadAllLines(Path.Combine(root, relative))
                    .Select(line => line.Split('=', 2))
                    .Where(pair => pair.Length == 2 && EditorConfigPins.ContainsKey(pair[0].Trim()))
                    .Where(pair => pair[1].Trim() != EditorConfigPins[pair[0].Trim()])
                    .Select(pair => $"{relative}: {pair[0].Trim()} = {pair[1].Trim()}")
            )
            .ToList();

        Assert.IsEmpty(overrides, string.Join(Environment.NewLine, overrides));
    }

    [TestMethod]
    public void NoMsBuildFileSwitchesCodeStyleEnforcementOff()
    {
        var root = RootOf();
        var overrides = ConfigFiles(root, file => Path.GetExtension(file) is ".csproj" or ".props" or ".targets")
            .SelectMany(relative =>
                XDocument
                    .Load(Path.Combine(root, relative))
                    .Descendants()
                    .Where(e => e.Name.LocalName is "EnforceCodeStyleInBuild" or "TreatWarningsAsErrors")
                    .Where(e => e.Value.Trim() != "true")
                    .Select(e => $"{relative}: {e.Name.LocalName} = {e.Value.Trim()}")
            )
            .ToList();

        Assert.IsEmpty(overrides, string.Join(Environment.NewLine, overrides));
    }

    static IEnumerable<string> ConfigFiles(string root, Func<string, bool> matches) =>
        Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(matches)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(relative => !relative.Split('/').Any(part => GeneratedFolders.Contains(part)));

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
