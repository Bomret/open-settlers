using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The looks the generated registries hold (ADR 0016, INV-0010): each graphics source draws the same eight jobs.
[TestClass]
public sealed class JobLooksTests
{
    private static readonly Job[] Drawn =
    [
        Job.Carrier,
        Job.Woodcutter,
        Job.Stonecutter,
        Job.TypeOneHut,
        Job.Builder,
        Job.Forester,
        Job.Digger,
        Job.Sawmiller,
    ];

    private static RuleTables Tables() =>
        new([.. Enumerable.Repeat(0, RuleTables.ClassCount).Select(value => (ObjectClass)value)], NoStock());

    private static byte[][] NoStock() =>
        [.. Enumerable.Range(0, RuleTables.StartStockRows).Select(_ => new byte[RuleTables.Goods])];

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
                "type-one-hut",
                "builder",
                "forester",
                "digger",
                "sawmiller",
            },
            looks.Select(look => look.Key).ToArray()
        );
    }
}
