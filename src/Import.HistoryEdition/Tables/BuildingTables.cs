using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The original's building types as the History Edition importer knows them (ADR 0020, INV-0008): the 12 types with their codes, the
/// stable names Open Settlers gives them with the evidence for each name (INV-0006), and the name-to-code map. The program's tables give
/// each type its size, needs, doors, worker, materials, and blows (<see cref="ProgramTablesImporter"/>); the two rules the program
/// holds as comparisons of the type's code, not as a table, are stated here, so that no code reaches the domain.
/// </summary>
static class BuildingTables
{
    /// <summary>The number of building types of the original that the catalog holds.</summary>
    public const int Count = 12;

    /// <summary>
    /// The number of types the size, needs, order, and blow tables cover: the types 0 to 0x17 (the tables at <c>0x42d307</c>,
    /// <c>0x449f18</c>, <c>0x43f0ba</c>, and <c>0x43f0fa</c> hold no entry of the castle, type 0x18).
    /// </summary>
    public const int TableTypes = 24;

    /// <summary>The number of door speeds the table at <c>0x43c92b</c> covers: each type twice, the second time with the constructing bit.</summary>
    public const int DoorSlots = 64;

    /// <summary>The bit the door table's index and the building table's type byte add while a building is built.</summary>
    public const int ConstructingBit = 0x20;

    /// <summary>
    /// The types whose builder takes the second figure: the mask tests the type's bit (<c>0x43ceb4</c> in <c>Builder_Start</c>,
    /// <c>docs/rules/construction.md</c>). It also names the types 0x13 and 0x16, which the catalog does not hold.
    /// </summary>
    const int SecondFigureMask = 0x4a0400;

    const int CastleCode = 0x18;
    const int StorehouseCode = 0xa;

    /// <summary>One building type of the original: its code, its stable name, and the evidence for the name.</summary>
    internal readonly struct Known(int code, string name, string evidence)
    {
        /// <summary>The type's code.</summary>
        public int Code { get; } = code;

        /// <summary>The type's stable name.</summary>
        public string Name { get; } = name;

        /// <summary>The evidence for the name (INV-0006).</summary>
        public string Evidence { get; } = evidence;
    }

    /// <summary>The building types of the original.</summary>
    public static IReadOnlyList<Known> All { get; } =
    [
        new(
            CastleCode,
            "castle",
            "the type 0x18 of the building table: its type byte holds 0xe0 while the castle is built and 0x60 when it stands, and the door table reads it at 0x18 and 0x38 (0x43c92b); docs/rules/build-sites.md"
        ),
        new(
            2,
            "woodcutter-hut",
            "handler 0x42d945 asks for the job of the woodcutter, whose work cuts trees (docs/rules/woodcutting.md); sprite object 0xa8 (0x44e63b); size and needs 0x42d307, 0x449f18"
        ),
        new(
            4,
            "stonecutter-hut",
            "handler 0x42d987 asks for job 7 (tool stock word 0x2e, good 23), whose work puts the stone (good 10) on the flag (0x438182); size and needs 0x42d307, 0x449f18; docs/rules/build-sites.md, docs/rules/stonecutting.md"
        ),
        new(
            9,
            "forester-hut",
            "handler 0x42d9c9 asks for job 8 (no tool), whose work plants the saplings 0x67 and 0x68 (0x4389bf); size and needs 0x42d307, 0x449f18; docs/rules/build-sites.md, docs/rules/forestry.md"
        ),
        new(
            0x11,
            "sawmill",
            "handler 0x42da0b asks for job 6 (tool stock word 0x2c, good 22), whose work turns the logs it takes in into planks (good 8, 0x43c6aa); large, needs 3 planks and 2 stones (0x42d307, 0x449f18); docs/rules/build-sites.md, docs/rules/sawmilling.md"
        ),
        new(
            1,
            "fisher-hut",
            "handler 0x42e0b8 asks for job 11 (tool stock word 0x24, good 18), whose walk ends by the water with the put of fish (good 1, 0x43837f); small, needs 2 planks (0x42d307, 0x449f18); docs/rules/food-buildings.md"
        ),
        new(
            0xc,
            "farm",
            "handler 0x42e25e asks for job 14 (tool stock word 0x28, good 20), whose walk ends with the put of grain (good 4, 0x43847d); large, needs 4 planks and 1 stone (0x42d307, 0x449f18); docs/rules/food-buildings.md"
        ),
        new(
            0xd,
            "butcher",
            "handler 0x42e1b3 asks for job 13 (tool stock word 0x26, good 19, the butcher's knife of the install's manual), whose start asks for a pig (class 3, 0x43d249) and whose work puts meat (good 3, 0x4333a5); large, needs 2 planks and 1 stone (0x42d307, 0x449f18); docs/rules/food-buildings.md"
        ),
        new(
            0xe,
            "pig-farm",
            "handler 0x42e0fa asks for job 12 (no tool), whose start asks for grain (class 4, 0x43d1e1) and whose work puts a pig (good 2, 0x433577); large with the scaffold, size, needs, and order of the farm (0x42d307, 0x449f18, 0x44fcfe); docs/rules/food-buildings.md"
        ),
        new(
            0xf,
            "mill",
            "handler 0x42e2a0 asks for job 15 (no tool), whose start asks for grain (class 4, 0x43d3b0) and whose work puts flour (good 5, 0x433a1e); small, needs 3 planks and 1 stone (0x42d307, 0x449f18), drawn as a windmill (object 188, 0x44fd16); docs/rules/food-buildings.md"
        ),
        new(
            0x10,
            "baker",
            "handler 0x42e37e asks for job 16 (no tool), whose start asks for flour (class 5, 0x43d44c) and whose work puts bread (good 6, 0x43384f); large, needs 2 planks and 1 stone (0x42d307, 0x449f18); docs/rules/food-buildings.md"
        ),
        new(
            StorehouseCode,
            "storehouse",
            "the storage types of the removal rules: the castle and the type 0xa; docs/rules/removal.md"
        ),
    ];

    /// <summary>The code of each stable name.</summary>
    public static IReadOnlyDictionary<string, int> CodeOfName { get; } =
        All.ToDictionary(type => type.Name, type => type.Code);

    /// <summary>The code of the building type <paramref name="type"/>; throws for a type the original lacks.</summary>
    public static int CodeOf(BuildingType type) => CodeOfName[type.Name];

    /// <summary>Whether the builder of a site of the type with this code takes the second figure (the mask <c>0x4a0400</c>, <c>0x43ceb4</c>).</summary>
    public static bool TakesSecondFigure(int code) => (SecondFigureMask >> code & 1) != 0;

    /// <summary>Whether the type with this code is a storage: the castle and the storehouse (<c>docs/rules/removal.md</c>).</summary>
    public static bool IsStorage(int code) => code is CastleCode or StorehouseCode;

    /// <summary>Whether the size, needs, order, and blow tables hold an entry of the type with this code.</summary>
    public static bool HasTableEntry(int code) => code < TableTypes;
}
