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
    public static ContentCatalog Of(byte[]? ranks = null, GoodClass?[]? classes = null, byte[]? kinds = null) =>
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
                )
        );

    /// <summary>The catalog of <see cref="Of"/> with its defaults: what an exporter reads a made-up game's goods from.</summary>
    public static readonly ContentCatalog Default = Of();

    /// <summary>A good that only a made-up catalog data entry gives: it has no original code.</summary>
    public static readonly Good AiToken = new() { Name = "ai-token" };

    /// <summary>The 26 goods and <see cref="AiToken"/>, which has no original code.</summary>
    public static ContentCatalog WithAiToken() =>
        new([
            .. Of().Goods,
            new GoodEntry
            {
                Name = AiToken.Name,
                Counter = FillCounter.Low,
                HandoverCounter = FillCounter.Low,
                CountsAgainstResource1 = false,
                TransportRank = 100,
            },
        ]);

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
