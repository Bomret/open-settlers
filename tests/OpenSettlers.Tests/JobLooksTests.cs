using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;

namespace OpenSettlers.Tests;

// The looks the generated registries hold (ADR 0016, INV-0010): each graphics source draws the same eight jobs.
[TestClass]
public sealed class JobLooksTests
{
    static readonly Job[] Drawn =
    [
        new Carrier(),
        new Woodcutter(),
        new Stonecutter(),
        new Fisher(),
        new Builder(),
        new Forester(),
        new Digger(),
        new Sawmiller(),
        new Miller(),
        new Baker(),
        new Farmer(),
        new Butcher(),
        new PigFarmer(),
    ];

    static RuleTables Tables() =>
        new(
            [.. Enumerable.Repeat(0, RuleTables.ClassCount).Select(value => (ObjectClass)value)],
            TestProgram.MadeStartStock(),
            jobData: TestProgram.MadeJobData()
        );

    [TestMethod]
    public void TheHistoryEditionRegistryHoldsOneLookForEachDrawnJob()
    {
        var looks = HistoryEditionLookRegistry.Build(Tables());

        CollectionAssert.AreEquivalent(Drawn, looks.Select(look => look.Job).ToArray());
    }

    [TestMethod]
    public void TheOpenSettlersRegistryHoldsOneLookForEachDrawnJobWithItsKey()
    {
        var looks = OpenSettlersLookRegistry.Build();

        CollectionAssert.AreEquivalent(Drawn, looks.Select(look => look.Job).ToArray());
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
