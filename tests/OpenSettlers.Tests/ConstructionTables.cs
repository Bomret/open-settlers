using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tables of made-up values for the rules of the materials that reach a building site (docs/rules/construction-materials.md): the classes,
// the request words, the order a storage serves in, and the rank the planks start from are chosen so that they differ from the program's
// own and from one another. The program's own values are in ConstructionMaterialsTests and ProgramTablesTests.
internal static class ConstructionTables
{
    // The made-up classes: the plank asks class 3 of the first word, the stone class 6 of the second, the log class 2 of the second;
    // every other good goes to storage at once.
    public static readonly GoodClass PlankClass = new(3, RequestWord.First);
    public static readonly GoodClass StoneClass = new(6, RequestWord.Second);
    public static readonly GoodClass LogClass = new(2, RequestWord.Second);

    public static GoodClass?[] Classes()
    {
        var table = new GoodClass?[RuleTables.GoodClassCount];
        table[Good.Plank.Code] = PlankClass;
        table[Good.Stone.Code] = StoneClass;
        table[Good.Log.Code] = LogClass;

        return table;
    }

    // The made-up order: planks first, then stones (the program's tables hold twelve steps; a storage that holds nothing else
    // serves these two alike).
    public static DistributionStep[] PlanksFirst() => [new(PlankClass, [Good.Plank]), new(StoneClass, [Good.Stone])];

    public static DistributionStep[] StonesFirst() => [new(StoneClass, [Good.Stone]), new(PlankClass, [Good.Plank])];

    // Every draw picks the first table below 0 and 1, so the draw 2 picks PlanksFirst, the draw 0 StonesFirst.
    public static DistributionStep[][] Orders() => [PlanksFirst(), StonesFirst(), PlanksFirst()];

    // The sizes of the four types of M4: the sawmill (0x11) is large, the others small.
    public static byte[] Sizes()
    {
        var sizes = Enumerable.Repeat((byte)1, RuleTables.BuildingTypeCount).ToArray();
        sizes[BuildingType.Sawmill.Code] = 2;

        return sizes;
    }

    // The made-up needs, a planks byte and a stones byte for each type: the forester two planks and no stone, every other type none.
    public static byte[] Needs()
    {
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        needs[2 * BuildingType.Forester.Code] = 2;

        return needs;
    }

    public static RuleTables Tables(byte plankRankBase = 0xff) =>
        LayoutSettlersTests.MadeTables(
            buildingClass: 4,
            // A made-up transport priority for each good (the good's own number): a carrier takes a waiting good only where its
            // priority is above 0.
            transportPriorities: [.. Enumerable.Range(1, RuleTables.Goods).Select(good => (byte)good)],
            buildingSizes: Sizes(),
            buildingNeeds: Needs(),
            goodClasses: Classes(),
            distribution: Orders(),
            plankRankBase: plankRankBase
        );
}
