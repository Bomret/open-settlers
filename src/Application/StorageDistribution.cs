using System.Linq;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The castle's hand-out of its goods to the requests of building sites: the part of the phase routine (<c>0x42965c</c>, run at the last
/// phase of each phase cycle) that serves the requests of the flags from the stock of the storages (<c>docs/rules/construction-materials.md</c>).
/// Each cycle the routine draws once from the random generator (the draw taken to 0 to 7 picks the order of the steps, the program's
/// three tables), and for each step it hands one good, if the stock holds it, to the flag whose request of the step's class
/// ranks highest above 15: a search from the storage's flag, level by level over the roads that hold a carrier, in which the best
/// rank so far grows by a quarter and one after each level, so a nearer request has a lead over a farther one. The good's count falls
/// by one, the site counts one more good on its way, and the served request keeps no rank until the building pass gives it one again.
/// <para>
/// **Stand-in:** the original puts the good in a slot of the storage item, and the castle's first settler, when no settler is leaving
/// the castle, carries it out and lays it on the castle's flag (the settler's states 12 and 11, about 130 passes after the hand-out
/// in the emulator's games); the engine lays it on the castle's flag at once, on its first empty place, and hands nothing out where the
/// castle's flag holds no empty place. Upgrade path: the settler's states.
/// </para>
/// </summary>
/// <param name="tables">The rule tables: the order of the steps.</param>
/// <param name="castle">The castle and its stock.</param>
/// <param name="flags">The flag table.</param>
/// <param name="roads">The roads between the flags.</param>
/// <param name="buildings">The building table: a served flag's building counts the good on its way.</param>
/// <param name="goods">The goods on the flags: the put of the good on the castle's flag.</param>
/// <param name="draws">The game's random generator: the draw of the order.</param>
/// <param name="history">The history the hand-out is recorded in (no rule reads it).</param>
public sealed class StorageDistribution(
    RuleTables tables,
    CastleStock castle,
    FlagTable flags,
    RoadNetwork roads,
    BuildingTable buildings,
    GoodsTransport goods,
    IRandomDraws draws,
    GameHistory history
)
{
    // The castle's flag is the first of the flag table, and the only storage's.
    private const int CastleFlag = 0;

    // The lowest rank a request serves above (0x42965c compares 0xf below the rank).
    private const int LowestRank = 0xf;

    /// <summary>Runs the routine of one phase cycle: the draw, then each step of the drawn order in turn.</summary>
    public void Run()
    {
        var order = tables.DistributionOrder(draws.Draw());
        if (castle.Castle is null || flags.Count == 0)
        {
            return;
        }

        foreach (var step in order)
        {
            // The good of the step the castle holds most of (the first of equals); none where the stock holds none.
            var good = step.Goods.MaxBy(castle.Count);
            if (castle.Count(good) > 0 && flags[CastleFlag].FirstEmptyPlace() >= 0)
            {
                Serve(step, good);
            }
        }
    }

    // 0x429a05 to 0x42a0ad for the search, 0x42a0ad to 0x42a2f4 for the hand-out.
    private void Serve(DistributionStep step, Good good)
    {
        var asking = FlagRequestSearch.Find(flags, roads, CastleFlag, step.Class, LowestRank);
        if (asking < 0)
        {
            return;
        }

        var word = step.Class.Word;
        flags[asking].SetRequest(word, flags[asking].RequestOf(word) with { Rank = 0 });
        if (buildings.OfFlag(asking) is { } building)
        {
            building.SetFill(word.Counter(), building.FillOf(word.Counter()) + 1);
        }

        castle.Take(good);
        var place = flags[CastleFlag].FirstEmptyPlace();
        goods.LayGood(CastleFlag, place, good.Code, destination: asking);
        history.RecordFlag(CastleFlag, EventType.Handout, Cause.Goods, $"{history.FlagId(asking)} good {good}");
    }
}
