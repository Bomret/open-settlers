using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// docs/agents/CODING.md, "Records": a record carries its data through init properties, never positional parameters, outside
// the job generator project and the two jobs the generated job lookup builds from a number. The scan parses each source file,
// so a record inside a string literal, such as a generator test's fixture source, is no declaration and is not counted.
[TestClass]
public sealed class PositionalRecordTests
{
    static readonly string[] Exempt = ["src/Domain/Jobs/Knight.cs", "src/Domain/Jobs/UnportedJob.cs"];

    [TestMethod]
    public void NoRecordOutsideTheExemptionsIsPositional()
    {
        var root = RootOf();
        var positional = SourceFiles(root)
            .Where(relative => !relative.StartsWith("src/Generators/", StringComparison.Ordinal))
            .Where(relative => Array.IndexOf(Exempt, relative) < 0)
            .SelectMany(relative => PositionalRecords(root, relative).Select(name => $"{relative}: {name}"))
            .ToList();

        Assert.IsEmpty(positional, string.Join(Environment.NewLine, positional));
    }

    // The positive control: the scan reaches the source and the test projects, and each exempt file still holds the positional
    // record it is exempt for, so the scan finds a positional record where there is one.
    [TestMethod]
    public void TheScanFindsThePositionalRecordsOfTheExemptions()
    {
        var root = RootOf();
        var files = SourceFiles(root).ToList();

        Assert.IsGreaterThan(100, files.Count(file => file.StartsWith("src/", StringComparison.Ordinal)));
        Assert.IsGreaterThan(100, files.Count(file => file.StartsWith("tests/", StringComparison.Ordinal)));
        foreach (var exempt in Exempt)
        {
            Assert.HasCount(1, PositionalRecords(root, exempt), exempt);
        }
    }

    static string[] PositionalRecords(string root, string relative) =>
        [
            .. CSharpSyntaxTree
                .ParseText(File.ReadAllText(Path.Combine(root, relative)))
                .GetRoot()
                .DescendantNodes()
                .OfType<RecordDeclarationSyntax>()
                .Where(record => record.ParameterList is not null)
                .Select(record => record.Identifier.Text),
        ];

    static IEnumerable<string> SourceFiles(string root) =>
        new[] { "src", "tests" }
            .SelectMany(folder =>
                Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
            )
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(relative => !relative.Contains("/obj/") && !relative.Contains("/bin/"));

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
