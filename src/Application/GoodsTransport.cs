using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

// The goods part of the flag table's pass (0x46414d, docs/rules/goods.md): a good that lies on a flag gets the castle's flag
// as its destination and then the direction of the first road toward it, and the carrier who waits next to the flag on that
// road is told to fetch it (state 0x42, 0x42ff81, the carrier behaviour's).

/// <summary>
/// The goods on the flags: the put of a good on a place, the turn of a flag's goods that gives each good a destination and the
/// direction of the road toward it, the transport priority that decides which good a road takes first, and the exchange of
/// goods between a carrier and the flag it stands on.
/// </summary>
/// <param name="movement">The settlers' ground: the change of a carrier's activity and the rule tables (the transport priorities).</param>
/// <param name="flags">The flag table: the places of each flag and the sides of its roads.</param>
/// <param name="roads">The roads between the flags.</param>
/// <param name="history">The history the behaviour records its changes in (no rule reads it).</param>
/// <param name="buildings">The building table: the building a request's flag belongs to counts the goods on their way to it.</param>
public sealed class GoodsTransport(
    SettlerMovement movement,
    FlagTable flags,
    RoadNetwork roads,
    GameHistory history,
    BuildingTable? buildings = null
) : ITransportPriorities
{
    /// <summary>
    /// A good put on a place of a flag (0x436b46): the place's destination is cleared (or set to a given one, for a test that
    /// lays a good with a destination, and a way, for a test that lays a good with a direction) and the flag's turn is due.
    /// </summary>
    public void LayGood(int flag, int place, int good, int? destination = null, int? way = null)
    {
        var laid = flags[flag].Places[place];
        laid.Good = new Good(good);
        laid.Destination = destination;
        laid.Way = way;
        flags[flag].NeedsGoodsTurn = true;
    }

    /// <summary>
    /// Puts a good on a place of the flag on the node as the settler's put does (0x436b46, <see cref="LayGood"/>). The
    /// original's put takes the first empty place and checks nothing: this refuses a node that holds no flag and a place
    /// that holds a good. The place is 0 to 7, as the layout's read checks. It returns why it refused, or null.
    /// </summary>
    public string? PlaceGood(int x, int y, int place, int good)
    {
        var flag = flags.SlotAt(x, y);
        if (flag < 0)
        {
            return "no flag stands on the node";
        }

        if (!flags[flag].Places[place].IsEmpty)
        {
            return "the place holds a good already";
        }

        LayGood(flag, place, good);
        history.RecordFlag(flag, EventType.LayoutGood, Cause.Layout);

        return null;
    }

    /// <summary>The good on a place waits for the road that leaves the flag in a direction.</summary>
    public void WaitForRoad(int flag, int way, int place) => flags[flag].Side(way).WaitingPlace = place;

    /// <summary>
    /// The transport priority of a good (a byte of the table in the player's block, +good - 0x55 from the pointer +0x64 + 4 *
    /// player, read at 0x43b054): the rank the player's setup at a new game writes for each good 1 to 26 (0x42055e calls
    /// 0x445570), read from the program. Good 0 is no good and has none. The bytes for the goods 27 to 31 belong to other fields
    /// of the player's block in the original; no good has those numbers, and the engine gives them none.
    /// </summary>
    public int TransportPriority(Good good) => movement.Tables.TransportPriority(good);

    /// <summary>
    /// The turn of a flag's goods (0x46414d, from the thermometer at 0x46427c to the places' loop ending at 0x4650b9). It runs
    /// when the flag's bit is set: the bit goes out, and each place from the last to the first that holds a good without a
    /// direction gets a destination, or, with one, a direction.
    /// </summary>
    public void Turn(int flag)
    {
        if (!flags[flag].NeedsGoodsTurn)
        {
            return;
        }

        flags[flag].NeedsGoodsTurn = false;
        var load = Load(flag);
        var places = flags[flag].Places;
        for (var place = 7; place >= 0; place--)
        {
            if (places[place].IsEmpty || places[place].Way is not null)
            {
                continue;
            }

            if (places[place].Destination is null)
            {
                Destine(flag, place);
            }
            else
            {
                Direct(flag, place, load);
            }
        }
    }

    // The goods that head each direction, as the four sets the original builds (0x46427c): set k holds the directions that more
    // than k goods have been given. Index k of the result is that set.
    private RoadSet[] Load(int flag)
    {
        var count = new int[Direction.Count];
        foreach (var place in flags[flag].Places)
        {
            if (place.Way is { } way)
            {
                count[way]++;
            }
        }

        var sets = new RoadSet[4];
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            for (var k = 0; k < sets.Length && k < count[direction]; k++)
            {
                sets[k] = sets[k].With(direction);
            }
        }

        return sets;
    }

    // The destination of a good with none (0x464364 to 0x46444c, 0x45d3ea). The table at 0x464117 gives a good a class and the
    // request word it is looked for in, or sends it to storage at once. A good with a class looks first for the flag whose request
    // asks for it (the request search); where none does, the storage search follows: over the roads that hold a carrier, to the
    // castle's flag, or none.
    private void Destine(int flag, int place)
    {
        var good = flags[flag].Places[place].Good;
        if (movement.Tables.ClassOfGood(good) is { } goodClass && RequestSearch(flag, goodClass) is >= 0 and var asking)
        {
            Serve(asking, goodClass.Word);
            flags[flag].Places[place].Destination = asking;
            flags[flag].NeedsGoodsTurn = true;

            return;
        }

        var found = StorageSearch(flag);
        flags[flag].NeedsGoodsTurn = true;
        if (found >= 0)
        {
            flags[flag].Places[place].Destination = found;

            return;
        }

        // No storage is reached over the roads that hold a carrier: the good waits and the flag is asked again at its next
        // turn. **Not ported:** where some road of the flag holds a carrier, the original gives the good a direction at
        // once, chosen from what its registers hold (0x464539); the engine lets it wait as it does with none.
    }

    // 0x4644ab to 0x464a3f: the search from a flag, level by level over the roads that hold a carrier (the directions from north
    // to east), for the flag whose request word asks for the class with a rank above the best so far. Each level after one in which
    // a request was found the best rank grows by a quarter and one (0x464906), and the search ends where that carries out of the
    // byte or no flag is left, so a farther request must rank above what the nearer has grown to. It gives the flag, or -1.
    private int RequestSearch(int start, GoodClass goodClass)
    {
        var marked = new bool[flags.Count];
        marked[start] = true;
        var level = new List<int> { start };
        var best = 0;
        var found = -1;
        while (true)
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
                    var request = flags[other].RequestOf(goodClass.Word);
                    if (request.Asks(goodClass.Class) && best < request.Rank)
                    {
                        best = request.Rank;
                        found = other;
                    }
                }
            }

            if (next.Count == 0)
            {
                return best == 0 ? -1 : found;
            }

            level = next;
            if (best == 0)
            {
                continue;
            }

            var grown = best + (best >> 2) + 1;
            best = (byte)grown;
            if (grown > 0xff)
            {
                return found;
            }
        }
    }

    // 0x464999 to 0x4649e0: the flag that was found is served: its rank, shifted right by one where bit 0 is set, else 0, and the
    // building of the flag counts one more good on the way, in the fill byte of the word's kind (the first word is the planks').
    private void Serve(int asking, RequestWord word)
    {
        var request = flags[asking].RequestOf(word);
        flags[asking].SetRequest(
            word,
            request with
            {
                Rank = (request.Rank & 1) != 0 ? (byte)(request.Rank >> 1) : (byte)0,
            }
        );
        if (buildings?.OfFlag(asking) is { } building)
        {
            var counter = word == RequestWord.First ? FillCounter.Low : FillCounter.High;
            building.SetFill(counter, building.FillOf(counter) + 1);
        }
    }

    /// <summary>
    /// A good that was on its way to a request and loses its destination (0x462ca4, <c>docs/rules/construction-materials.md</c>): the
    /// building at the destination flag counts one good less on the way, in the fill byte the good's kind names, unless it is a
    /// storage's building. A good of no kind, or with no destination, changes nothing.
    /// </summary>
    public void ReleaseRequest(Good good, int? destination)
    {
        var kind = movement.Tables.FillCounterOf(good);
        if (
            kind == FillCounter.None
            || destination is not { } flag
            || buildings?.OfFlag(flag) is not { } building
            || (!building.Constructing && (building.Type == BuildingType.Castle || building.Type == BuildingType.Storehouse))
        )
        {
            return;
        }

        building.SetFill(kind, building.FillOf(kind) - 1);
    }

    // 0x45d3ea: the search from a flag over the roads that hold a carrier (from north to east) for
    // the flag of a storage. A flag that is a storage itself finds none. The castle's flag is the only storage.
    private int StorageSearch(int start)
    {
        if (start == 0)
        {
            return -1;
        }

        var marked = new bool[flags.Count];
        marked[start] = true;
        var queue = new Queue<int>([start]);
        while (queue.Count > 0)
        {
            var flag = queue.Dequeue();
            for (var direction = 5; direction >= 0; direction--)
            {
                if (
                    !flags[flag].Side(direction).HoldsCarrier
                    || roads.FlagNeighbour(flag, direction) is not (>= 0 and var next)
                    || marked[next]
                )
                {
                    continue;
                }

                if (next == 0)
                {
                    return next;
                }

                marked[next] = true;
                queue.Enqueue(next);
            }
        }

        return -1;
    }

    // The directions of a flag's roads that hold a carrier.
    private RoadSet CarrierRoads(int flag)
    {
        var held = RoadSet.Empty;
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            held = flags[flag].Side(direction).HoldsCarrier ? held.With(direction) : held;
        }

        return held;
    }

    // A good with a destination gets the direction of the first road toward it (0x464aa8, 0x464c36 to 0x4650b9). The search
    // runs from the flag's roads that hold a carrier, the ones with the fewest goods heading that way first (0x464bb2 seeds
    // one load after the other), and spreads each seed's direction over the roads that hold a carrier, level by level; the
    // direction that reaches the destination first is the good's.
    private void Direct(int flag, int place, RoadSet[] load)
    {
        var good = flags[flag].Places[place];
        var destination = good.Destination!.Value;
        var marked = new bool[flags.Count];
        var label = new int[flags.Count];
        marked[flag] = true;
        label[flag] = 6;
        var queue = new List<int>();
        var carried = CarrierRoads(flag);
        void Seed(RoadSet seeds)
        {
            for (var direction = 5; direction >= 0; direction--)
            {
                if (!seeds.Has(direction))
                {
                    continue;
                }

                carried = carried.Without(direction);
                if (roads.FlagNeighbour(flag, direction) is >= 0 and var next && !marked[next])
                {
                    marked[next] = true;
                    label[next] = direction;
                    queue.Add(next);
                }
            }
        }

        // Each set of the load holds the next one (more than k goods heading a way include more than k + 1), so the
        // directions that gain one good from set k - 1 to set k are the ones in set k - 1 and not in set k.
        Seed(carried.Except(load[0]));
        for (var k = 1; k < 4 && !carried.IsEmpty; k++)
        {
            Seed(load[k - 1].Except(load[k]));
        }

        if (!carried.IsEmpty)
        {
            Seed(load[3]);
        }

        if (queue.Count == 0)
        {
            flags[flag].NeedsGoodsTurn = true;

            return;
        }

        var level = queue;
        while (true)
        {
            var next = new List<int>();
            foreach (var current in level)
            {
                for (var direction = 5; direction >= 0; direction--)
                {
                    if (
                        flags[current].Side(direction).HoldsCarrier
                        && roads.FlagNeighbour(current, direction) is >= 0 and var other
                        && !marked[other]
                    )
                    {
                        marked[other] = true;
                        label[other] = label[current];
                        next.Add(other);
                    }
                }
            }

            level = next;
            if (marked[destination] || level.Count == 0)
            {
                break;
            }
        }

        if (!marked[destination] || label[destination] == 6)
        {
            // The destination cannot be reached: the good gives it up (0x462ca4 releases a request, and a storage counts none).
            ReleaseRequest(good.Good, good.Destination);
            good.Destination = null;
            flags[flag].NeedsGoodsTurn = true;

            return;
        }

        // Where no good waits for the road the good takes the road. Where one waits, the good takes its place only with a
        // higher transport priority (0x46501a): the road is waited for by the new place, and the good that loses the place
        // keeps its direction and waits until the exchange lines it up again.
        var way = label[destination];
        var side = flags[flag].Side(way);
        if (side.WaitingPlace is not { } waiting)
        {
            WaitForRoad(flag, way, place);
        }
        else if (TransportPriority(flags[flag].Places[waiting].Good) < TransportPriority(good.Good))
        {
            side.WaitingPlace = place;
        }

        good.Way = way;
    }

    /// <summary>
    /// A carrier on a flag (state 3, 0x43af07 to 0x43b410): with the heading it came in, the road it works, it swaps goods with
    /// the flag. Where a good waits for that road the carrier takes it, and leaves the good it carries in the same place,
    /// destination and all; where none waits and the carrier carries one, the good goes on the first empty place. A flag where
    /// something was laid has its goods turn again. The road's waiting place is then made again: the good of the greatest
    /// transport priority that was given the road's direction waits for it next.
    /// </summary>
    public void Exchange(int slot, Settler settler, WalksRoad walks, int flag)
    {
        var way = walks.Heading.Direction;
        var places = flags[flag].Places;
        var side = flags[flag].Side(way);
        var left = walks.Load?.Good ?? Good.None;
        var took = Good.None;
        var changed = false;
        if (side.WaitingPlace is { } waiting)
        {
            var place = places[waiting];
            changed = true;
            if (walks.Load is { } carried)
            {
                flags[flag].NeedsGoodsTurn = true;
                var good = place.Good;
                var destination = place.Destination;
                place.Good = carried.Good;
                place.Way = null;
                place.Destination = carried.Destination;
                walks = walks with { Load = new Load(good, destination), Waits = 0, Marked = false };
            }
            else
            {
                walks = walks with { Load = new Load(place.Good, place.Destination), Waits = 0, Marked = false };
                place.Good = Good.None;
                place.Way = null;
            }

            took = walks.Load?.Good ?? Good.None;
            side.WaitingPlace = null;
            var best = 0;
            var found = -1;
            for (var i = 0; i < places.Count; i++)
            {
                if (!places[i].IsEmpty && places[i].Way == way && TransportPriority(places[i].Good) > best)
                {
                    best = TransportPriority(places[i].Good);
                    found = i;
                }
            }

            if (found >= 0)
            {
                WaitForRoad(flag, way, found);
            }
        }
        else if (walks.Load is { } carried)
        {
            var empty = flags[flag].FirstEmptyPlace();
            if (empty >= 0)
            {
                flags[flag].NeedsGoodsTurn = true;
                places[empty].Good = carried.Good;
                places[empty].Way = null;
                walks = walks with { Load = null };
                places[empty].Destination = carried.Destination;
                changed = true;
            }
        }

        // A carrier with no good keeps the destination of the last one in its item: the residue holds it.
        movement.Become(settler, walks);
        if (changed)
        {
            var detail = $"took {took} left {left}";
            history.RecordSettler(slot, EventType.Exchange, Cause.Goods, $"{history.FlagId(flag)} {detail}");
            history.RecordFlag(flag, EventType.Exchange, Cause.Goods, $"{history.SettlerId(slot)} {detail}");
        }
    }
}
