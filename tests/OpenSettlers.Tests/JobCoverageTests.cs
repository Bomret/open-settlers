using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The layers' registries hold the job modules each layer found (ADR 0016): every job with rules has a look under every graphics
// source, and no job has two rules or two looks under one source.
[TestClass]
public sealed class JobCoverageTests
{
    const string HistoryEdition = "History Edition";
    const string ContentSet = "Open Settlers";

    // What is wrong with the modules: a job with two rules, a job with two looks under a source, and a job with rules and no look
    // under a source. A job without rules may have looks (the carrier's) or none (the castle settler's).
    internal static IReadOnlyList<string> Problems(
        IEnumerable<Type> rulesJobs,
        IReadOnlyDictionary<string, IEnumerable<Type>> looksBySource
    )
    {
        var problems = new List<string>();
        var ruled = rulesJobs.ToList();
        problems.AddRange(Twice(ruled).Select(job => $"{job.Name} has two rules"));
        foreach (var (source, looks) in looksBySource)
        {
            var drawn = looks.ToList();
            problems.AddRange(Twice(drawn).Select(job => $"{job.Name} has two looks under {source}"));
            problems.AddRange(
                ruled
                    .Distinct()
                    .Where(job => !drawn.Contains(job))
                    .Select(job => $"{job.Name} has rules and no look under {source}")
            );
        }

        return problems;
    }

    static IEnumerable<Type> Twice(IEnumerable<Type> jobs) =>
        jobs.GroupBy(job => job).Where(group => group.Count() > 1).Select(group => group.Key);

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

    static Dictionary<string, IEnumerable<Type>> Looks(params Type[] extra) =>
        new()
        {
            [HistoryEdition] =
            [
                .. HistoryEditionLookRegistry.Build(TestProgram.Made()).Select(look => look.Job.GetType()),
                .. extra,
            ],
            [ContentSet] = [.. OpenSettlersLookRegistry.Build().Select(look => look.Job.GetType()), .. extra],
        };

    [TestMethod]
    public void EveryJobWithRulesHasALookUnderEveryGraphicsSourceAndNoJobHasTwo()
    {
        var problems = Problems(RulesJobs(), Looks());

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void AFixtureJobWithRulesAndNoLookIsAProblemUnderEverySource()
    {
        var problems = Problems(RulesJobs().Append(typeof(Domain.Jobless)), Looks());

        CollectionAssert.AreEquivalent(
            new[]
            {
                $"Jobless has rules and no look under {HistoryEdition}",
                $"Jobless has rules and no look under {ContentSet}",
            },
            problems.ToArray()
        );
    }

    [TestMethod]
    public void TwoRulesOrTwoLooksForOneJobAreProblems()
    {
        var problems = Problems(RulesJobs().Append(typeof(Domain.Digger)), Looks(typeof(Domain.Digger)));

        CollectionAssert.AreEquivalent(
            new[]
            {
                "Digger has two rules",
                $"Digger has two looks under {HistoryEdition}",
                $"Digger has two looks under {ContentSet}",
            },
            problems.ToArray()
        );
    }
}
