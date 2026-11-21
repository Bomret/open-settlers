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

    [TestMethod]
    public void TheCheckJobEndsAfterThirtyMinutes()
    {
        Assert.AreEqual("30", TimeoutMinutes(Workflow));
    }

    [TestMethod]
    public void ACheckJobWithoutATimeoutHasNone()
    {
        var unbounded = Workflow.Replace("    timeout-minutes: 30\n", "");

        Assert.IsNull(TimeoutMinutes(unbounded));
    }

    // GitHub keeps one running and one pending run per concurrency group and cancels the pending one when another
    // queues, whatever cancel-in-progress says. A pull request groups by its ref and cancels its earlier run; a push to
    // main groups by its commit, so it holds that push alone, and is never cancelled.
    [TestMethod]
    public void APullRequestCancelsItsEarlierRunAndEachPushToMainRunsAlone()
    {
        Assert.IsNull(ConcurrencyDrift(Workflow));
    }

    [TestMethod]
    public void AGroupKeyedOnTheCommitAloneDrifts()
    {
        var bySha = Workflow.Replace("github.event_name == 'pull_request' && github.ref || github.sha", "github.sha");

        StringAssert.Contains(ConcurrencyDrift(bySha), "group");
    }

    [TestMethod]
    public void CancellingEveryRunInProgressDrifts()
    {
        var always = Workflow.Replace(
            "cancel-in-progress: ${{ github.event_name == 'pull_request' }}",
            "cancel-in-progress: true"
        );

        StringAssert.Contains(ConcurrencyDrift(always), "cancel-in-progress");
    }

    // A message naming the difference, or null where the ruleset names exactly the contexts of the workflow's job.
    static string? Drift(string workflow, string ruleset)
    {
        if (Job(workflow) is not { } job)
        {
            return "ci.yml holds no job under jobs:";
        }

        var (id, body) = job;

        if (body.Any(l => l.StartsWith("    name:", StringComparison.Ordinal)))
        {
            return $"the job {id} carries a name: key, which renames its check contexts";
        }

        var os = body.Select(l => Regex.Match(l, @"^\s+os:\s*\[(.*)\]\s*$")).FirstOrDefault(m => m.Success);
        if (os is null)
        {
            return $"the job {id} holds no os: [...] matrix list";
        }

        var posted = os.Groups[1].Value.Split(',').Select(o => $"{id} ({o.Trim()})").Order().ToArray();
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

    // A message naming the concurrency key that keys or cancels otherwise than ci.yml's comment states, or null.
    static string? ConcurrencyDrift(string workflow)
    {
        var lines = workflow.Split('\n').Select(l => l.Trim()).ToArray();
        if (
            !lines.Contains(
                "group: ${{ github.workflow }}-${{ github.event_name == 'pull_request' && github.ref || github.sha }}"
            )
        )
        {
            return "the concurrency group keys a pull request otherwise than on its ref, or a push otherwise than on its commit";
        }

        return lines.Contains("cancel-in-progress: ${{ github.event_name == 'pull_request' }}")
            ? null
            : "cancel-in-progress cancels otherwise than only a pull request's earlier run";
    }

    // The value of the workflow job's timeout-minutes: key, or null where the job carries none.
    static string? TimeoutMinutes(string workflow) =>
        Job(workflow)
            ?.Body.Select(l => Regex.Match(l, @"^    timeout-minutes:\s*(\S+)\s*$"))
            .FirstOrDefault(m => m.Success)
            ?.Groups[1]
            .Value;

    // The id and body lines of the first job under jobs:, or null where ci.yml holds none.
    static (string Id, string[] Body)? Job(string workflow)
    {
        var lines = workflow.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var jobs = Array.IndexOf(lines, "jobs:");
        var job =
            jobs < 0
                ? null
                : lines.Skip(jobs + 1).Select(l => Regex.Match(l, @"^  ([\w-]+):\s*$")).FirstOrDefault(m => m.Success);
        if (job is null)
        {
            return null;
        }

        var body = lines
            .SkipWhile(l => l != job.Value)
            .Skip(1)
            .TakeWhile(l => l.Length == 0 || l.StartsWith("   ", StringComparison.Ordinal))
            .ToArray();

        return (job.Groups[1].Value, body);
    }
}
