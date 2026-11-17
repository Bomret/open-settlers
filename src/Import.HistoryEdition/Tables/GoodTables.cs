using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The original's goods as the History Edition importer knows them (ADR 0020, INV-0008): the 26 goods with their codes, the stable names
/// Open Settlers gives them with the evidence for each name (INV-0006), and the name-to-code map. The program's tables give each good its
/// request, its counter, and its rank (<see cref="ProgramTablesImporter"/>); the two rules the program holds as comparisons of the good's
/// code, not as a table, are stated here, so that no code reaches the domain.
/// </summary>
static class GoodTables
{
    /// <summary>The number of goods of the original: the codes 1 to 26.</summary>
    public const int Count = 26;

    /// <summary>One good of the original: its code, its stable name, and the evidence for the name.</summary>
    internal readonly struct Known(int code, string name, string evidence)
    {
        /// <summary>The good's code, 1 to 26.</summary>
        public int Code { get; } = code;

        /// <summary>The good's stable name.</summary>
        public string Name { get; } = name;

        /// <summary>The evidence for the name (INV-0006).</summary>
        public string Evidence { get; } = evidence;
    }

    /// <summary>The goods of the original in the order of their codes, 1 to 26.</summary>
    public static IReadOnlyList<Known> All { get; } =
    [
        new(
            1,
            "fish",
            "the put at 0x43837f of job 11's walk; class 0 of the word at 0x42 with goods 3 and 6 (0x464117); docs/rules/food-buildings.md"
        ),
        new(
            2,
            "pig",
            "put by job 12 (0x433577), asked by job 13 (class 3, 0x43d249); drawn as a pig on a flag (entry 322); docs/rules/food-buildings.md"
        ),
        new(
            3,
            "meat",
            "put by job 13 (0x4333a5); class 0 of the word at 0x42 (0x464117); docs/rules/food-buildings.md"
        ),
        new(
            4,
            "grain",
            "the put at 0x43847d of job 14's walk; asked by jobs 12 and 15 (class 4, 0x43d1e1, 0x43d3b0); docs/rules/food-buildings.md"
        ),
        new(
            5,
            "flour",
            "put by job 15 (0x433a1e), asked by job 16 (class 5, 0x43d44c); drawn as a sack on a flag (entry 325); docs/rules/food-buildings.md"
        ),
        new(
            6,
            "bread",
            "put by job 16 (0x43384f); class 0 of the word at 0x42 (0x464117); drawn as loaves on a flag (entry 326); docs/rules/food-buildings.md"
        ),
        new(7, "log", "the woodcutter's put at 0x438013; docs/rules/woodcutting.md"),
        new(
            8,
            "plank",
            "the sawmiller's put at 0x43c6aa; a building site's request of class 1 takes its stock word; docs/rules/sawmilling.md, docs/rules/construction-materials.md"
        ),
        new(
            9,
            "boat",
            "class 0xff, storage (row 9 of 0x464117); no routine read puts or asks it; drawn as a boat hull on a flag (entry 329); the install's manual has a boat builder make boats from planks; inferred, docs/rules/food-buildings.md"
        ),
        new(
            10,
            "stone",
            "the stonecutter's put at 0x438182; a building site's request of class 4 of the second word takes its stock word; docs/rules/stonecutting.md"
        ),
        new(
            11,
            "iron-ore",
            "asked by the start of job 10 in the type 0x12 (class 1, second word, row 11 of 0x464117); the foundry's input; docs/rules/food-buildings.md"
        ),
        new(
            12,
            "iron",
            "put by the work of job 10 (0x4342df), asked by the types 0x13 and 0x14 (class 2, second word, row 12 of 0x464117); docs/rules/food-buildings.md"
        ),
        new(
            13,
            "coal",
            "asked by the types 0x12, 0x14, and 0x17 (class 2, first word, row 13 of 0x464117); docs/rules/food-buildings.md"
        ),
        new(
            14,
            "gold-ore",
            "asked by the type 0x17 (class 0, second word, row 14 of 0x464117); counts against resource 1 with good 15 (0x462c8d); docs/rules/food-buildings.md"
        ),
        new(
            15,
            "gold",
            "put by the work of job 10 (0x4342f1, class 3, second word, row 15 of 0x464117); counts against resource 1 with good 14 (0x462c8d); docs/rules/food-buildings.md"
        ),
        new(
            16,
            "shovel",
            "the tool of the digger's request, job 2 (0x42d328, stock word 0x20); row 16 of 0x464117; the install's manual gives the leveler a shovel; docs/rules/digging.md, docs/rules/food-buildings.md"
        ),
        new(
            17,
            "hammer",
            "the tool of the builder's request (0x42d5fc, stock word 0x22) and the second of the types 0x13 and 0x14; row 17 of 0x464117; docs/rules/food-buildings.md"
        ),
        new(
            18,
            "fishing-rod",
            "the tool of the handler of the type 1 (0x42e0b8, stock word 0x24); row 18 of 0x464117; docs/rules/food-buildings.md"
        ),
        new(
            19,
            "butcher-knife",
            "the tool of the handler of the type 0xd (0x42e1b3, stock word 0x26); row 19 of 0x464117; docs/rules/food-buildings.md"
        ),
        new(
            20,
            "scythe",
            "the tool of the handler of the type 0xc (0x42e25e, stock word 0x28); row 20 of 0x464117; docs/rules/food-buildings.md"
        ),
        new(
            21,
            "hatchet",
            "the tool of the handler of the type 2 (0x42d945, stock word 0x2a); row 21 of 0x464117; docs/rules/woodcutting.md, docs/rules/food-buildings.md"
        ),
        new(
            22,
            "saw",
            "the tool of the handler of the type 0x11 (0x42da0b, stock word 0x2c) and the second tool of the type 0x13; row 22 of 0x464117; docs/rules/food-buildings.md"
        ),
        new(
            23,
            "pickaxe",
            "the tool of the handler of the type 4 (0x42d987, stock word 0x2e); row 23 of 0x464117; docs/rules/stonecutting.md, docs/rules/food-buildings.md"
        ),
        new(
            24,
            "pliers",
            "the second tool of the type 0x14 (stock word 0x30); row 24 of 0x464117; docs/rules/food-buildings.md"
        ),
        new(
            25,
            "sword",
            "class 0xff, storage (row 25 of 0x464117); no routine read puts or asks it; drawn as a blade with a yellow hilt on a flag (entry 345); the manual's weapon smith makes swords and shields; inferred, docs/rules/food-buildings.md"
        ),
        new(
            26,
            "shield",
            "class 0xff, storage (row 26 of 0x464117); no routine read puts or asks it; the second weapon of the manual's weapon smith, so by elimination with good 25; inferred, docs/rules/food-buildings.md"
        ),
    ];

    /// <summary>The goods of the original by stable name: the name-to-code map.</summary>
    public static IReadOnlyDictionary<string, int> CodeOfName { get; } =
        All.ToDictionary(good => good.Name, good => good.Code);

    static readonly IReadOnlyDictionary<int, Known> ByCode = All.ToDictionary(good => good.Code);

    /// <summary>
    /// The good of the original with the code <paramref name="code"/>, with its stable name. Throws an <see cref="InvalidDataException"/>
    /// that names <paramref name="where"/> for a code with no entry: the program then names a good this importer does not know.
    /// </summary>
    public static Good GoodOf(int code, string where) =>
        ByCode.TryGetValue(code, out var known)
            ? new Good { Code = code, Name = known.Name }
            : throw new InvalidDataException(
                $"Siedler1.exe names the good {code} at {where}, which has no entry: the original's goods are 1 to {Count}."
            );

    /// <summary>
    /// Which byte of a building's fill the carrier's handover of the good <paramref name="code"/> counts in (<c>0x43c4ea</c>,
    /// <c>docs/rules/goods.md</c>, "The delivery into the castle"): the program compares the good's code, and the log (7) and the goods from
    /// 9 on but coal (13) count in the high byte.
    /// </summary>
    public static FillCounter HandoverCounter(int code) =>
        code == 7 || (code >= 9 && code != 13) ? FillCounter.High : FillCounter.Low;

    /// <summary>
    /// Whether a lost good of the code <paramref name="code"/> counts against the total of resource 1 (<c>0x462c8d</c>,
    /// <c>docs/rules/removal.md</c>, "A good that is lost"): the program compares the good's code with 14 and 15, gold ore and gold.
    /// </summary>
    public static bool CountsAgainstResource1(int code) => code is 14 or 15;
}
