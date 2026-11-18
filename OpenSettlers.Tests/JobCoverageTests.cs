using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Import.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The comparison of the graphics sources with the rules, through public types only (ADR 0016, ADR 0019): every job with rules is a
// drawn job (DrawnJobs), which each source's own test project holds its looks to, and no job has two rules.
[TestClass]
public sealed class JobCoverageTests
{
    // What is wrong with the rules: a job with two rules and a job with rules that no source is held to draw. A job without rules
    // may have looks (the carrier's) or none (the castle settler's).
    static IReadOnlyList<string> Problems(IEnumerable<Type> rulesJobs)
    {
        var ruled = rulesJobs.ToList();
        var drawn = DrawnJobs.All.Select(job => job.GetType()).ToList();

        return
        [
            .. ruled
                .GroupBy(job => job)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.Name} has two rules"),
            .. ruled.Distinct().Where(job => !drawn.Contains(job)).Select(job => $"{job.Name} has rules and no look"),
        ];
    }

    static IEnumerable<Type> RulesJobs() =>
        JobModules
            .Build(
                new JobParts
                {
                    Movement = null!,
                    Buildings = null!,
                    Flags = null!,
                    History = null!,
                    Draws = null!,
                    LayGood = null!,
                    Hut = null!,
                }
            )
            .Select(rules => rules.JobType);

    [TestMethod]
    public void EveryJobWithRulesHasALookUnderEveryGraphicsSourceAndNoJobHasTwo()
    {
        var problems = Problems(RulesJobs());

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void AFixtureJobWithRulesAndNoLookIsAProblemUnderEverySource()
    {
        var problems = Problems(RulesJobs().Append(typeof(Jobless)));

        CollectionAssert.AreEquivalent(new[] { "Jobless has rules and no look" }, problems.ToArray());
    }

    [TestMethod]
    public void TwoRulesForOneJobAreAProblem()
    {
        var problems = Problems(RulesJobs().Append(typeof(Digger)));

        CollectionAssert.AreEquivalent(new[] { "Digger has two rules" }, problems.ToArray());
    }
}
