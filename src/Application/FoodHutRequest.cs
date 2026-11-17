using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The request of an indoor food building for the good it takes in, which the mill, the baker, the pig farmer, and the butcher make the same
/// way (<c>docs/rules/milling.md</c>, <c>baking.md</c>, <c>pig-farming.md</c>, <c>butchering.md</c>): the building's handler keeps the rank of
/// the flag's request, and the start routine of the job at the door writes the request. What differs between the jobs stays with each job's
/// rules: the rank base (the mill's reads a player word, the pig farm's another, the baker's and the butcher's are the immediate 0xff), the
/// status bit the work sets (<see cref="Building.AtWork"/>: the miller and the baker only), and the pig farm's turns while the worker is on the way.
/// The rules keep the rank on every turn while the building's request-failed bit holds, where the program writes it only on the turn whose
/// request failed (inferred as unobservable: the written value equals the stored one while the fill is unchanged, and no input class is
/// asked before the worker's first start).
/// </summary>
static class FoodHutRequest
{
    // The most goods the building's fill counts (on site and on the way) before it asks for no more: 0x42e33e (mill), 0x42e3f7 (baker),
    // 0x42e173 (pig farm), 0x42e1f4 (butcher).
    const int StoreFull = 8;

    /// <summary>
    /// The rank of the flag's request: <paramref name="rankBase"/> shifted right by the goods the building holds, on site and on the way (the
    /// two nibbles of the low fill byte), and 0 where it holds eight or more.
    /// </summary>
    public static byte RankOf(Building hut, byte rankBase)
    {
        var held = (hut.FillLow >> 4) + (hut.FillLow & 0xf);

        return held < StoreFull ? (byte)(rankBase >> held) : (byte)0;
    }

    /// <summary>Sets the rank of the flag's request for <paramref name="asked"/>, the class bits and the other word as they are.</summary>
    public static void SetRank(SettlerMovement movement, FlagTable flags, Building hut, Good asked, byte rank)
    {
        if (movement.Tables.ClassOfGood(asked) is { } asks)
        {
            var flag = flags[hut.Flag];
            flag.SetRequest(asks.Word, flag.RequestOf(asks.Word) with { Rank = rank });
        }
    }

    /// <summary>
    /// The write of the start routine where the mode byte is not 0: both request words lose their class and the first word's rank (the second
    /// word's rank stays), and the word of the good's class asks for <paramref name="asked"/> with rank 0.
    /// </summary>
    public static void Ask(SettlerMovement movement, FlagTable flags, Building hut, Good asked)
    {
        var flag = flags[hut.Flag];
        foreach (var word in new[] { RequestWord.First, RequestWord.Second })
        {
            flag.SetRequest(word, flag.RequestOf(word) with { Asks = null });
        }

        if (movement.Tables.ClassOfGood(asked) is { } asks)
        {
            flag.SetRequest(asks.Word, new Request { Asks = asks, Rank = 0 });
        }
    }
}
