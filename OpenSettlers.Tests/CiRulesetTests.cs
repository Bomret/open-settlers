using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The ruleset of main (.github/rulesets/main.json) requires the check contexts that the CI job of
// .github/workflows/ci.yml posts. GitHub names each context `<job id> (<os>)` from the job id and its matrix, or after
// the job's `name:` key when one is set. A renamed job or matrix entry that the ruleset still names would leave a
// required result pending forever.
[TestClass]
public sealed class CiRulesetTests
{
    static string Workflow => File.ReadAllText(PythonTool.InRepo(".github", "workflows", "ci.yml"));
    static string Ruleset => File.ReadAllText(PythonTool.InRepo(".github", "rulesets", "main.json"));

    [TestMethod]
    public void TheRulesetRequiresExactlyTheContextsTheCiJobPosts()
    {
        Assert.IsNull(Drift(Workflow, Ruleset));
    }

    [TestMethod]
    public void ARenamedMatrixEntryDrifts()
    {
        var renamed = Workflow.Replace("windows-latest", "windows-2025");

        StringAssert.Contains(Drift(renamed, Ruleset), "check (windows-2025)");
    }

    [TestMethod]
    public void ARenamedJobDrifts()
    {
        var renamed = Workflow.Replace("\n  check:", "\n  verify:");

        StringAssert.Contains(Drift(renamed, Ruleset), "verify (ubuntu-latest)");
    }

    [TestMethod]
    public void ANameKeyOnTheJobDrifts()
    {
        var named = Workflow.Replace("\n  check:\n", "\n  check:\n    name: Check\n");

        StringAssert.Contains(Drift(named, Ruleset), "name:");
    }

    // A message naming the difference, or null where the ruleset names exactly the contexts of the workflow's job.
    static string? Drift(string workflow, string ruleset)
    {
        var lines = workflow.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var jobs = Array.IndexOf(lines, "jobs:");
        if (jobs < 0)
        {
            return "ci.yml holds no jobs: key";
        }

        var job = lines.Skip(jobs + 1).Select(l => Regex.Match(l, @"^  ([\w-]+):\s*$")).FirstOrDefault(m => m.Success);
        if (job is null)
        {
            return "ci.yml holds no job under jobs:";
        }

        var body = lines
            .SkipWhile(l => l != job.Value)
            .Skip(1)
            .TakeWhile(l => l.Length == 0 || l.StartsWith("   ", StringComparison.Ordinal))
            .ToArray();
        if (body.Any(l => l.StartsWith("    name:", StringComparison.Ordinal)))
        {
            return $"the job {job.Groups[1].Value} carries a name: key, which renames its check contexts";
        }

        var os = body.Select(l => Regex.Match(l, @"^\s+os:\s*\[(.*)\]\s*$")).FirstOrDefault(m => m.Success);
        if (os is null)
        {
            return $"the job {job.Groups[1].Value} holds no os: [...] matrix list";
        }

        var posted = os.Groups[1].Value.Split(',').Select(o => $"{job.Groups[1].Value} ({o.Trim()})").Order().ToArray();
        var required = JsonDocument
            .Parse(ruleset)
            .RootElement.GetProperty("rules")
            .EnumerateArray()
            .Where(r => r.GetProperty("type").GetString() == "required_status_checks")
            .SelectMany(r => r.GetProperty("parameters").GetProperty("required_status_checks").EnumerateArray())
            .Select(c => c.GetProperty("context").GetString())
            .Order()
            .ToArray();

        return posted.SequenceEqual(required)
            ? null
            : $"ci.yml posts [{string.Join(", ", posted)}], main.json requires [{string.Join(", ", required)}]";
    }
}
