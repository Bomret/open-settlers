using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The History Edition source's registry holds the job modules it found (ADR 0016): every job with rules has a look under it, and no
// job has two. The jobs with rules are the drawn jobs (DrawnJobs); the repository's cross-source check holds the rules to that list.
[TestClass]
public sealed class JobCoverageTests
{
    const string HistoryEdition = "History Edition";

    // What is wrong with the looks: a job with two looks and a job with rules and no look. A job without rules may have looks (the
    // carrier's) or none (the castle settler's).
    static List<string> Problems(IEnumerable<Type> rulesJobs, IEnumerable<Type> looks)
    {
        var drawn = looks.ToList();
        List<string> problems =
        [
            .. drawn
                .GroupBy(job => job)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.Name} has two looks under {HistoryEdition}"),
            .. rulesJobs
                .Distinct()
                .Where(job => !drawn.Contains(job))
                .Select(job => $"{job.Name} has rules and no look under {HistoryEdition}"),
        ];

        return problems;
    }

    static IEnumerable<Type> RulesJobs() => DrawnJobs.All.Select(job => job.GetType());

    static IEnumerable<Type> Looks(params Type[] extra) =>
        [.. HistoryEditionLookRegistry.Build(MadeProgram.Made()).Select(look => look.Job.GetType()), .. extra];

    [TestMethod]
    public void EveryJobWithRulesHasALookUnderTheHistoryEditionSourceAndNoJobHasTwo()
    {
        var problems = Problems(RulesJobs(), Looks());

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void AFixtureJobWithRulesAndNoLookIsAProblemUnderTheHistoryEditionSource()
    {
        var problems = Problems(RulesJobs().Append(typeof(Jobless)), Looks());

        CollectionAssert.AreEquivalent(
            new[] { $"Jobless has rules and no look under {HistoryEdition}" },
            problems.ToArray()
        );
    }

    [TestMethod]
    public void TwoLooksForOneJobAreAProblemUnderTheHistoryEditionSource()
    {
        var problems = Problems(RulesJobs(), Looks(typeof(Digger)));

        CollectionAssert.AreEquivalent(
            new[] { $"Digger has two looks under {HistoryEdition}" },
            problems.ToArray()
        );
    }
}
