using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// docs/agents/CODING.md, "What the build enforces": the build fails on a violation of each rule the table lists. The rules
// live in the repository's configuration, so these tests pin that configuration: each listed rule ID at warning severity
// for every C# file, the options the rules read, and code style enforced in the build with warnings as errors.
[TestClass]
public sealed class CodeStyleBuildTests
{
    // The options whose value decides what a rule reports. when_types_exactly_match keeps the type the code creates.
    static readonly Dictionary<string, string> OptionPins = new()
    {
        ["dotnet_style_require_accessibility_modifiers"] = "omit_if_default",
        ["dotnet_style_prefer_collection_expression"] = "when_types_exactly_match",
    };

    static readonly string[] GeneratedFolders = ["bin", "obj", ".godot", ".git", ".superdev", "node_modules"];

    [TestMethod]
    public void TheEditorConfigSetsEveryBuildEnforcedRuleAndItsOptions()
    {
        var settings = CSharpSection(File.ReadAllLines(Path.Combine(RootOf(), ".editorconfig")));
        var pins = EditorConfigPins().Select(pin => $"{pin.Key} = {pin.Value}").ToList();

        Assert.IsEmpty(pins.Except(settings), string.Join(Environment.NewLine, pins.Except(settings)));
    }

    // The table must list the rules, or the pins above would check nothing.
    [TestMethod]
    public void TheCodingGuideListsTheBuildEnforcedRules()
    {
        CollectionAssert.IsSubsetOf(new[] { "IDE0040", "IDE0300", "IDE0305", "IDE0290" }, BuildEnforcedRuleIds());
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

    // docs/agents/CODING.md, "Document": every source project generates a documentation file, so the compiler reports the
    // doc-comment diagnostics. The test project alone is left out.
    [TestMethod]
    public void EverySourceProjectGeneratesADocumentationFile()
    {
        var settings = XDocument
            .Load(Path.Combine(RootOf(), "Directory.Build.props"))
            .Descendants("PropertyGroup")
            .Elements("GenerateDocumentationFile")
            .Select(e => $"{e.Attribute("Condition")?.Value} => {e.Value.Trim()}")
            .ToArray();

        CollectionAssert.AreEqual(new[] { "'$(MSBuildProjectName)' != 'OpenSettlers.Tests' => true" }, settings);
    }

    // A NoWarn that names a build-enforced rule or a doc-comment diagnostic switches it off for a whole project.
    [TestMethod]
    public void NoMsBuildFileSuppressesABuildEnforcedRule()
    {
        var root = RootOf();
        var guarded = BuildEnforcedRuleIds().Concat(DocCommentDiagnosticIds()).ToHashSet();
        var suppressions = ConfigFiles(root, file => Path.GetExtension(file) is ".csproj" or ".props" or ".targets")
            .SelectMany(relative =>
                XDocument
                    .Load(Path.Combine(root, relative))
                    .Descendants()
                    .Where(e => e.Name.LocalName == "NoWarn")
                    .SelectMany(e => e.Value.Split(';', ',').Select(id => id.Trim()))
                    .Where(guarded.Contains)
                    .Select(id => $"{relative}: NoWarn {id}")
            )
            .ToList();

        Assert.IsEmpty(suppressions, string.Join(Environment.NewLine, suppressions));
    }

    // The guide must list the doc-comment diagnostics, or the NoWarn check would miss them.
    [TestMethod]
    public void TheCodingGuideListsTheDocCommentDiagnostics()
    {
        CollectionAssert.IsSubsetOf(new[] { "CS1591", "CS1570", "CS0419" }, DocCommentDiagnosticIds());
    }

    // A later section, a nested .editorconfig, or a project file could switch a rule off past the pins above, so every
    // place that sets one of the pinned settings must set it to the pinned value.
    [TestMethod]
    public void NoEditorConfigSectionOverridesAPinnedSetting()
    {
        var root = RootOf();
        var pins = EditorConfigPins();
        var overrides = ConfigFiles(root, file => Path.GetFileName(file) == ".editorconfig")
            .SelectMany(relative =>
                File.ReadAllLines(Path.Combine(root, relative))
                    .Select(line => line.Split('=', 2))
                    .Where(pair => pair.Length == 2 && pins.ContainsKey(pair[0].Trim()))
                    .Where(pair => pair[1].Trim() != pins[pair[0].Trim()])
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
                    .Where(e =>
                        e.Name.LocalName
                            is "EnforceCodeStyleInBuild"
                                or "TreatWarningsAsErrors"
                                or "GenerateDocumentationFile"
                    )
                    .Where(e => e.Value.Trim() != "true")
                    .Select(e => $"{relative}: {e.Name.LocalName} = {e.Value.Trim()}")
            )
            .ToList();

        Assert.IsEmpty(overrides, string.Join(Environment.NewLine, overrides));
    }

    // Each rule ID of the table under "What the build enforces" at warning severity, beside the option pins.
    static Dictionary<string, string> EditorConfigPins() =>
        BuildEnforcedRuleIds()
            .Select(id => new KeyValuePair<string, string>($"dotnet_diagnostic.{id}.severity", "warning"))
            .Concat(OptionPins)
            .ToDictionary(pin => pin.Key, pin => pin.Value);

    // The rule IDs of the table, a range such as IDE0300–IDE0305 expanded to each ID in it.
    static string[] BuildEnforcedRuleIds() =>
        [
            .. CodingGuideSection("What the build enforces")
                .Where(line => line.StartsWith('|'))
                .SelectMany(line => Regex.Matches(line, @"IDE(\d{4})(?:–IDE(\d{4}))?"))
                .SelectMany(match =>
                {
                    var first = int.Parse(match.Groups[1].Value);
                    var last = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : first;
                    return Enumerable.Range(first, last - first + 1).Select(n => $"IDE{n:D4}");
                }),
        ];

    // The compiler diagnostics that "Document" names as failing the build of a source project.
    static string[] DocCommentDiagnosticIds() =>
        [.. CodingGuideSection("Document").SelectMany(line => Regex.Matches(line, @"CS\d{4}")).Select(m => m.Value)];

    static IEnumerable<string> CodingGuideSection(string heading) =>
        File.ReadAllLines(Path.Combine(RootOf(), "docs", "agents", "CODING.md"))
            .SkipWhile(line => line.Trim() != $"### {heading}")
            .Skip(1)
            .TakeWhile(line => !line.StartsWith('#'));

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
