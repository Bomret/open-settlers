using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The looks the generated registry of the open settlers source holds (ADR 0016, INV-0010): the source draws the jobs every
// graphics source draws. The History Edition source's own check is in its test project, and the comparison of the sources in the
// repository's tests.
[TestClass]
public sealed class JobLooksTests
{
    [TestMethod]
    public void TheOpenSettlersRegistryHoldsOneLookForEachDrawnJobWithItsKey()
    {
        var looks = OpenSettlersLookRegistry.Build();

        CollectionAssert.AreEquivalent(DrawnJobs.All.ToArray(), looks.Select(look => look.Job).ToArray());
        CollectionAssert.AreEquivalent(
            new[]
            {
                "carrier",
                "woodcutter",
                "stonecutter",
                "fisher",
                "builder",
                "forester",
                "digger",
                "sawmiller",
                "miller",
                "baker",
                "farmer",
                "butcher",
                "pig-farmer",
            },
            looks.Select(look => look.Key).ToArray()
        );
    }
}
