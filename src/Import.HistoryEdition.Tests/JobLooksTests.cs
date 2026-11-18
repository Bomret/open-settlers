using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The looks the generated registry of the History Edition source holds (ADR 0016, INV-0010): the source draws the jobs every
// graphics source draws. The open settlers source's own check is in its test project, and the comparison of the sources in the
// repository's tests.
[TestClass]
public sealed class JobLooksTests
{
    static RuleTables Tables() =>
        new(
            [.. Enumerable.Repeat(0, RuleTables.ClassCount).Select(value => (ObjectClass)value)],
            MadeProgram.MadeStartStock(),
            jobData: MadeProgram.MadeJobData()
        );

    [TestMethod]
    public void TheHistoryEditionRegistryHoldsOneLookForEachDrawnJob()
    {
        var looks = HistoryEditionLookRegistry.Build(Tables());

        CollectionAssert.AreEquivalent(DrawnJobs.All.ToArray(), looks.Select(look => look.Job).ToArray());
    }
}
