using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Tests;

/// <summary>
/// Content catalogs of made-up facts for the tests that build <see cref="RuleTables"/>: 26 goods by stable name, each with the code its
/// position gives it. No fact restates the program's tables: a test gives the ranks, the requests, and the counters it needs, and the
/// defaults are the ones a test can do without.
/// </summary>
static class TestCatalog
{
    /// <summary>The length of an array of a fact by code: the codes 0 to 31, as the made-up class and kind tables hold them.</summary>
    public const int Slots = 32;

    // The building types of the made-up catalogs with the code each has there: the original's positions, so that an exporter writes what
    // it wrote before building types were entries. Declared before the catalogs that read it.
    static readonly (int Code, BuildingType Type)[] BuildingCodes =
    [
        (0x18, BuildingType.Castle),
        (2, BuildingType.Woodcutter),
        (4, BuildingType.Stonecutter),
        (9, BuildingType.Forester),
        (0x11, BuildingType.Sawmill),
        (1, BuildingType.Fisher),
        (0xc, BuildingType.Farm),
        (0xd, BuildingType.Butcher),
        (0xe, BuildingType.PigFarm),
        (0xf, BuildingType.Mill),
        (0x10, BuildingType.Baker),
        (0xa, BuildingType.Storehouse),
    ];

    /// <summary>The names of the goods in the order of their codes, 1 to 26.</summary>
    public static readonly string[] Names =
    [
        "fish",
        "pig",
        "meat",
        "grain",
        "flour",
        "bread",
        "log",
        "plank",
        "boat",
        "stone",
        "iron-ore",
        "iron",
        "coal",
        "gold-ore",
        "gold",
        "shovel",
        "hammer",
        "fishing-rod",
        "butcher-knife",
        "scythe",
        "hatchet",
        "saw",
        "pickaxe",
        "pliers",
        "sword",
        "shield",
    ];

    /// <summary>The good of <see cref="Names"/> at the 1-based position <paramref name="code"/>; no good for any other number.</summary>
    public static Good GoodOf(int code) =>
        code >= 1 && code <= Names.Length ? new Good { Name = Names[code - 1] } : Good.None;

    /// <summary>The 1-based position of the good in <see cref="Names"/>, 0 for no good: the code the made-up catalogs give it.</summary>
    public static int CodeOf(Good good) => Array.IndexOf(Names, good.Name) + 1;

    /// <summary>
    /// A catalog of the 26 goods. <paramref name="ranks"/> gives the rank of each good by index good - 1 (the good's own code where
    /// null), and a good whose rank is 0 has no entry at all. <paramref name="classes"/> gives each good's request by its code (storage
    /// at once where null), and <paramref name="kinds"/> the counter of each good by its code: 0 low, 1 high, anything else none (low
    /// where null). The handover counts in the high byte for the log and the goods 9 on but coal, and goods 14 and 15 count against
    /// resource 1: made-up facts of the tests that deliver goods, chosen to read like a play.
    /// </summary>
    public static ContentCatalog Of(
        byte[]? ranks = null,
        GoodClass?[]? classes = null,
        byte[]? kinds = null,
        IEnumerable<BuildingEntry>? buildings = null
    ) =>
        new(
            Enumerable
                .Range(1, Names.Length)
                .Where(code => ranks is null || ranks[code - 1] != 0)
                .Select(code =>
                    Entry(
                        code,
                        ranks is null ? code : ranks[code - 1],
                        classes is not null && code < classes.Length ? classes[code] : null,
                        kinds is not null && code < kinds.Length ? kinds[code] : (byte)0
                    )
                ),
            buildings ?? Buildings(),
            Jobs()
        );

    /// <summary>The catalog of <see cref="Of"/> with its defaults: what an exporter reads a made-up game's goods from.</summary>
    public static readonly ContentCatalog Default = Of();

    /// <summary>
    /// The 26 goods with the facts a game read for a good its tables did not know before every good was an entry: storage at once, the
    /// low fill byte for the way and the handover, and no count against resource 1. The made-up tables of the emulated and golden
    /// scenarios (<see cref="TestProgram.Made()"/>, <c>LayoutSettlersTests.MadeTables</c>) play on it, so their digests stay.
    /// </summary>
    public static ContentCatalog StandIn(IEnumerable<BuildingEntry>? buildings = null) =>
        new(
            Of()
                .Goods.Select(entry =>
                    entry with
                    {
                        HandoverCounter = FillCounter.Low,
                        CountsAgainstResource1 = false,
                    }
                ),
            buildings ?? Buildings(),
            Jobs()
        );

    /// <summary>
    /// The stand-in catalog of the tests that walk through doors: every door speeds 8, standing and constructing, as the made-up door
    /// tables did. The tables give <c>noBuildingDoor: 8</c> beside it, for the door of a place with no building.
    /// </summary>
    public static ContentCatalog WithDoors(WorkerRequest?[]? workers = null) =>
        StandIn(Buildings(door: 8, workers: workers));

    /// <summary>The length of an array of a fact by building code: the codes 0 to 0x17, as the made-up size and needs tables held them.</summary>
    public const int BuildingSlots = 24;

    /// <summary>The code the made-up catalogs give the building type: an index into an array of a fact by code.</summary>
    public static int CodeOf(BuildingType type) => BuildingCodes.Single(entry => entry.Type == type).Code;

    /// <summary>The building type of the made-up catalogs with this code.</summary>
    public static BuildingType BuildingOf(int code) => BuildingCodes.Single(entry => entry.Code == code).Type;

    /// <summary>
    /// The 12 building types with made-up facts, each given as an array by code (<see cref="BuildingSlots"/> long) where a test needs
    /// its own: <paramref name="sizes"/> (small for every type where null; a value other than 1 or 2 is a mine), <paramref name="needs"/>
    /// (planks then stones for each code), <paramref name="workers"/> (none where null), <paramref name="order"/> (the order word of
    /// materials, a set bit a stone), and <paramref name="blows"/> (two steps for each code). Every door speeds
    /// <paramref name="door"/>, standing and constructing. The sawmill and the storehouse take the second figure and the castle and the
    /// storehouse are storages, as the original has them.
    /// </summary>
    public static IEnumerable<BuildingEntry> Buildings(
        byte door = 0,
        byte[]? sizes = null,
        byte[]? needs = null,
        WorkerRequest?[]? workers = null,
        ushort[]? order = null,
        ushort[]? blows = null
    ) =>
        BuildingCodes.Select(entry =>
        {
            var tabled = entry.Code < BuildingSlots;
            var size = tabled && sizes is not null ? sizes[entry.Code] : (byte)(tabled ? 1 : 0);

            return new BuildingEntry
            {
                Name = entry.Type.Name,
                OriginalCode = entry.Code,
                TieRank = entry.Code,
                Size = size is 1 or 2 ? (BuildingSize)size : BuildingSize.Mine,
                Needs =
                    tabled && needs is not null
                        ? new BuildingNeeds { Planks = needs[2 * entry.Code], Stones = needs[2 * entry.Code + 1] }
                        : new BuildingNeeds { Planks = 0, Stones = 0 },
                StandingDoor = door,
                ConstructingDoor = door,
                Worker = tabled && workers is not null ? workers[entry.Code] : null,
                Stones =
                    tabled && order is not null
                        ? [.. Enumerable.Range(0, 16).Select(turn => (order[entry.Code] >> turn & 1) != 0)]
                        : [],
                EarlyBlowStep = tabled && blows is not null ? blows[2 * entry.Code] : (ushort)0,
                LateBlowStep = tabled && blows is not null ? blows[2 * entry.Code + 1] : (ushort)0,
                SecondFigure = entry.Type == BuildingType.Sawmill || entry.Type == BuildingType.Storehouse,
                IsStorage = entry.Type == BuildingType.Castle || entry.Type == BuildingType.Storehouse,
            };
        });

    /// <summary>
    /// The jobs of the engine as catalog entries, each with the number of its type as its original code, as the History Edition has them:
    /// what an exporter reads a made-up game's jobs from. <paramref name="codes"/> false gives entries with no code, as a set of its own has.
    /// </summary>
    public static IEnumerable<JobEntry> Jobs(bool codes = true) =>
        Job.Ported.Select(job => new JobEntry
        {
            Name = job.Name,
            NameEvidence = codes ? job.NameEvidence : null,
            OriginalCode = codes ? job.OriginalCode : null,
        });

    /// <summary>A good that only a made-up catalog data entry gives: it has no original code.</summary>
    public static readonly Good AiToken = new() { Name = "ai-token" };

    /// <summary>The 26 goods and <see cref="AiToken"/>, which has no original code.</summary>
    public static ContentCatalog WithAiToken(byte door = 0) =>
        new(
            [
                .. Of().Goods,
                new GoodEntry
                {
                    Name = AiToken.Name,
                    Counter = FillCounter.Low,
                    HandoverCounter = FillCounter.Low,
                    CountsAgainstResource1 = false,
                    TransportRank = 100,
                },
            ],
            Buildings(door),
            Jobs()
        );

    /// <summary>One good of <see cref="Names"/> with its code and the given facts.</summary>
    public static GoodEntry Entry(int code, int rank, GoodClass? request = null, byte kind = 0) =>
        new()
        {
            Name = Names[code - 1],
            OriginalCode = code,
            Request = request,
            Counter = KindCounter(kind),
            HandoverCounter = code == 7 || (code >= 9 && code != 13) ? FillCounter.High : FillCounter.Low,
            CountsAgainstResource1 = code is 14 or 15,
            TransportRank = rank,
        };

    static FillCounter KindCounter(byte kind) =>
        kind switch
        {
            0 => FillCounter.Low,
            1 => FillCounter.High,
            _ => FillCounter.None,
        };
}
