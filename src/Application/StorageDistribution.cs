using System.Collections.Generic;
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
            if (Stocked(step) is { } good && flags[CastleFlag].FirstEmptyPlace() >= 0)
            {
                Serve(step, good);
            }
        }
    }

    // The good of the step the castle holds: the one its stock word names, or for the food step (offset -1) the good of goods 1, 3,
    // and 6 whose count is greatest (the first of equals). None where the stock holds none.
    private Good? Stocked(DistributionStep step)
    {
        if (step.StockOffset >= 0)
        {
            var good = new Good(step.StockOffset / 2 + 1);

            return castle.Count(good) > 0 ? good : null;
        }

        Good[] food = [new(1), new(3), new(6)];
        var best = food[0];
        foreach (var candidate in food)
        {
            if (castle.Count(candidate) > castle.Count(best))
            {
                best = candidate;
            }
        }

        return castle.Count(best) > 0 ? best : null;
    }

    // 0x429a05 to 0x42a0ad for the search, 0x42a0ad to 0x42a2f4 for the hand-out.
    private void Serve(DistributionStep step, Good good)
    {
        if (Search(step) is not { } asking)
        {
            return;
        }

        var request = flags[asking].RequestOf(step.Word);
        flags[asking].SetRequest(step.Word, request with { Rank = 0 });
        if (buildings.OfFlag(asking) is { } building)
        {
            var counter = step.Word == RequestWord.First ? FillCounter.Low : FillCounter.High;
            building.SetFill(counter, building.FillOf(counter) + 1);
        }

        castle.Take(good);
        var place = flags[CastleFlag].FirstEmptyPlace();
        goods.LayGood(CastleFlag, place, good.Code, destination: asking);
        history.RecordFlag(CastleFlag, EventType.Handout, Cause.Goods, $"{history.FlagId(asking)} good {good}");
    }

    // The search from the castle's flag: the flag asking for the step's class with a rank above 15 and above the best so far, which
    // grows by a quarter and one after each level (to 255 where it carries out of the byte, and then no flag is searched further).
    private int? Search(DistributionStep step)
    {
        var marked = new bool[flags.Count];
        marked[CastleFlag] = true;
        var level = new List<int> { CastleFlag };
        var best = 0;
        int? found = null;
        while (best != 0xff)
        {
            var next = new List<int>();
            foreach (var current in level)
            {
                for (var direction = 5; direction >= 0; direction--)
                {
                    if (
                        !flags[current].Side(direction).HoldsCarrier
                        || roads.FlagNeighbour(current, direction) is not (>= 0 and var other)
                        || marked[other]
                    )
                    {
                        continue;
                    }

                    marked[other] = true;
                    next.Add(other);
                    var request = flags[other].RequestOf(step.Word);
                    if (request.Asks(step.Class) && request.Rank > LowestRank && best < request.Rank)
                    {
                        best = request.Rank;
                        found = other;
                    }
                }
            }

            if (next.Count == 0)
            {
                break;
            }

            level = next;
            if (best != 0)
            {
                best = System.Math.Min(best + (best >> 2) + 1, 0xff);
            }
        }

        return best == 0 ? null : found;
    }
}
