using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tables of made-up values for the rules of the materials that reach a building site (docs/rules/construction-materials.md): no
// original value is restated here. A good's class and request word, the order a storage serves in, and the rank the planks start
// from are chosen so that the made-up values differ from one another.
internal static class ConstructionTables
{
    // The made-up classes: the plank (good 8) asks class 1 of the first word, the stone (good 10) class 4 of the second, the log
    // (good 7) class 5 of the second; every other good goes to storage at once.
    public static byte[] Classes()
    {
        var table = Enumerable.Repeat((byte)0xff, 2 * RuleTables.GoodClassCount).ToArray();
        void Set(int good, byte goodClass, byte word)
        {
            table[2 * good] = goodClass;
            table[2 * good + 1] = word;
        }

        Set(Good.Plank.Code, 1, 0x42);
        Set(Good.Stone.Code, 4, 0x44);
        Set(Good.Log.Code, 5, 0x44);

        return table;
    }

    // The made-up order: planks first, then stones (the program's tables hold twelve steps; a storage that holds nothing else
    // serves these two alike).
    public static DistributionStep[] PlanksFirst() => [new(RequestWord.First, 1, 14), new(RequestWord.Second, 4, 18)];

    public static DistributionStep[] StonesFirst() => [new(RequestWord.Second, 4, 18), new(RequestWord.First, 1, 14)];

    // Every draw picks the first table below 0 and 1, so the draw 2 picks PlanksFirst, the draw 0 StonesFirst.
    public static DistributionStep[][] Orders() => [PlanksFirst(), StonesFirst(), PlanksFirst()];

    // The sizes of the four types of M4: the sawmill (0x11) is large, the others small.
    public static byte[] Sizes()
    {
        var sizes = Enumerable.Repeat((byte)1, RuleTables.BuildingTypeCount).ToArray();
        sizes[BuildingType.Sawmill.Code] = 2;

        return sizes;
    }

    public static RuleTables Tables(byte plankRankBase = 0xff) =>
        LayoutSettlersTests.MadeTables(
            buildingSizes: Sizes(),
            goodClasses: Classes(),
            distribution: Orders(),
            plankRankBase: plankRankBase
        );
}
