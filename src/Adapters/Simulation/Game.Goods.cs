using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

// The goods part of the flag table's pass (0x46414d, docs/rules/goods.md): a good that lies on a flag gets the castle's flag
// as its destination and then the direction of the first road toward it, and the carrier who waits next to the flag on that
// road is told to fetch it (state 0x42, 0x42ff81).
public sealed partial class Game
{
    /// <summary>The logs the castle holds: the count of its storage item for the good 7.</summary>
    public int CastleLogs => Stock.Count > GoodLog ? Stock[GoodLog] : 0;

    // A good put on a place of a flag (0x436b46): the place's destination is cleared (or set to a given one, for a test that
    // lays a good with a destination, and a way, for a test that lays a good with a direction) and the flag's turn is due.
    internal void LayGood(int flag, int place, int good, int? destination = null, int? way = null)
    {
        var laid = _flags[flag].Places[place];
        laid.Good = new Good(good);
        laid.Destination = destination;
        laid.Way = way;
        _flags[flag].NeedsGoodsTurn = true;
    }

    // The turn of a flag's goods (0x46414d, from the thermometer at 0x46427c to the places' loop ending at 0x4650b9). It runs
    // when the flag's bit is set: the bit goes out, and each place from the last to the first that holds a good without a
    // direction gets a destination, or, with one, a direction.
    private void GoodsTurn(int flag)
    {
        if (!_flags[flag].NeedsGoodsTurn)
        {
            return;
        }

        _flags[flag].NeedsGoodsTurn = false;
        var load = Load(flag);
        var places = _flags[flag].Places;
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
        foreach (var place in _flags[flag].Places)
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

    // The destination of a good with none (0x464364 to 0x46444c, 0x45d3ea). The table at 0x464117 sends a log to the request
    // search first: it looks for a flag whose request word asks for the good, and none does until buildings ask for goods
    // (not ported), so the search that follows is the one that counts: the storage search over the roads that hold a
    // carrier. It gives the castle's flag, or none.
    private void Destine(int flag, int place)
    {
        var found = StorageSearch(flag);
        _flags[flag].NeedsGoodsTurn = true;
        if (found >= 0)
        {
            _flags[flag].Places[place].Destination = found;

            return;
        }

        // No storage is reached over the roads that hold a carrier: the good waits and the flag is asked again at its next
        // turn. **Not ported:** where some road of the flag holds a carrier, the original gives the good a direction at
        // once, chosen from what its registers hold (0x464539); the engine lets it wait as it does with none.
    }

    // 0x45d3ea: the search from a flag over the roads that hold a carrier (from north to east) for
    // the flag of a storage. A flag that is a storage itself finds none. The castle's flag is the only storage.
    private int StorageSearch(int start)
    {
        if (start == 0)
        {
            return -1;
        }

        var marked = new bool[_flags.Count];
        marked[start] = true;
        var queue = new Queue<int>([start]);
        while (queue.Count > 0)
        {
            var flag = queue.Dequeue();
            for (var direction = 5; direction >= 0; direction--)
            {
                if (
                    !_flags[flag].Side(direction).HoldsCarrier
                    || FlagNeighbour(flag, direction) is not (>= 0 and var next)
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
        var roads = RoadSet.Empty;
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            roads = _flags[flag].Side(direction).HoldsCarrier ? roads.With(direction) : roads;
        }

        return roads;
    }

    // A good with a destination gets the direction of the first road toward it (0x464aa8, 0x464c36 to 0x4650b9). The search
    // runs from the flag's roads that hold a carrier, the ones with the fewest goods heading that way first (0x464bb2 seeds
    // one load after the other), and spreads each seed's direction over the roads that hold a carrier, level by level; the
    // direction that reaches the destination first is the good's.
    private void Direct(int flag, int place, RoadSet[] load)
    {
        var good = _flags[flag].Places[place];
        var destination = good.Destination!.Value;
        var marked = new bool[_flags.Count];
        var label = new int[_flags.Count];
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
                if (FlagNeighbour(flag, direction) is >= 0 and var next && !marked[next])
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
            _flags[flag].NeedsGoodsTurn = true;

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
                        _flags[current].Side(direction).HoldsCarrier
                        && FlagNeighbour(current, direction) is >= 0 and var other
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
            good.Destination = null;
            _flags[flag].NeedsGoodsTurn = true;

            return;
        }

        // Where no good waits for the road the good takes the road. Where one waits, the good takes its place only with a
        // higher transport priority (0x46501a): the road is waited for by the new place, and the good that loses the place
        // keeps its direction and waits until the exchange lines it up again.
        var way = label[destination];
        var side = _flags[flag].Side(way);
        if (side.WaitingPlace is not { } waiting)
        {
            WaitForRoad(flag, way, place);
        }
        else if (TransportPriority(_flags[flag].Places[waiting].Good.Code) < TransportPriority(good.Good.Code))
        {
            side.WaitingPlace = place;
        }

        good.Way = way;
    }

    // State 0x42 (0x42ff81): a carrier who waits next to a flag looks for a good that waits to go along its road, at the flag it
    // stands next to (it goes on in the heading it came) and at the flag at the road's far end (it turns round). With one it
    // takes state 0x43 and runs it. **Not ported:** what the carrier does in state 3 and after it with a good to fetch (the
    // next stage); it walks on as it does with none.
    private void Fetch(int slot, Settler settler)
    {
        var waiting = (WaitsAtFlag)settler.Activity;
        var side = _flags[waiting.Flag].Side(waiting.Direction);
        Heading heading;
        if (side.WaitingPlace is not null)
        {
            heading = waiting.Approach.Unmarked;
        }
        else
        {
            if (_flags[FlagNeighbour(waiting.Flag, waiting.Direction)].Side(side.FarDirection).WaitingPlace is null)
            {
                return;
            }

            // The approach is the heading less 6 (Wait): from -6 (heading 0) to -1 (heading 5). The turned heading is the
            // opposite direction: approach + 6 - 3, and 6 more where that went below 0 (checked against the emulator's fetches
            // in road-splits.txt, where the carrier came by 0 and by 3).
            heading = waiting.Approach.Turned;
        }

        Become(settler, new ClaimsNode(heading, waiting.Approach));
        History.RecordSettler(slot, EventType.Fetch, Cause.Goods);
        Claim(slot, settler);
    }

    // State 0x43 (0x43001a), which state 0x42 runs on at once: the carrier takes the node it stands on where no settler stands
    // on it, and goes on in state 3 with its timer run out; else it tries again at its next turn.
    private void Claim(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        if (Occupant(here) != 0)
        {
            return;
        }

        _occupant[here] = slot;
        Become(settler, new WalksRoad(null, ((ClaimsNode)settler.Activity).Heading));
        settler.Stamp = _clock;
        settler.Timer = 0;
        History.RecordSettler(slot, EventType.Claim, Cause.Goods);
    }

    // The good on a place waits for the road that leaves the flag in a direction.
    internal void WaitForRoad(int flag, int way, int place) => _flags[flag].Side(way).WaitingPlace = place;

    // A carrier on a flag (state 3, 0x43af07 to 0x43b410): with the heading it came in, the road it works, it swaps goods with the
    // flag. Where a good waits for that road the carrier takes it, and leaves the good it carries in the same place, destination
    // and all; where none waits and the carrier carries one, the good goes on the first empty place. A flag where something was
    // laid has its goods turn again. The road's waiting place is then made again: the good of the greatest transport priority
    // that was given the road's direction waits for it next.
    internal void Exchange(int slot, Settler settler, int flag)
    {
        var walks = (WalksRoad)settler.Activity;
        var residue = SettlerEncoder.ResidueOf(settler);
        var way = walks.Heading.Direction;
        var places = _flags[flag].Places;
        var side = _flags[flag].Side(way);
        var left = walks.Load?.Good.Code ?? 0;
        var took = 0;
        var changed = false;
        if (side.WaitingPlace is { } waiting)
        {
            var place = places[waiting];
            changed = true;
            if (walks.Load is { } carried)
            {
                _flags[flag].NeedsGoodsTurn = true;
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

            took = walks.Load?.Good.Code ?? 0;
            side.WaitingPlace = null;
            var best = 0;
            var found = -1;
            for (var i = 0; i < places.Count; i++)
            {
                if (!places[i].IsEmpty && places[i].Way == way && TransportPriority(places[i].Good.Code) > best)
                {
                    best = TransportPriority(places[i].Good.Code);
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
            var empty = _flags[flag].FirstEmptyPlace();
            if (empty >= 0)
            {
                _flags[flag].NeedsGoodsTurn = true;
                places[empty].Good = carried.Good;
                places[empty].Way = null;
                walks = walks with { Load = null };
                places[empty].Destination = carried.Destination;
                changed = true;
            }
        }

        // A carrier with no good keeps the destination of the last one in its item: the residue holds it.
        settler.Activity = walks.Carrying(residue);
        if (changed)
        {
            var detail = $"took {took} left {left}";
            History.RecordSettler(slot, EventType.Exchange, Cause.Goods, $"{History.FlagId(flag)} {detail}");
            History.RecordFlag(flag, EventType.Exchange, Cause.Goods, $"{History.SettlerId(slot)} {detail}");
        }
    }

    // The transport priority of a good (a byte of the table in the player's block, +good - 0x55 from the pointer +0x64 + 4 *
    // player, read at 0x43b054): the rank the player's setup at a new game writes for each good 1 to 26 (0x42055e calls
    // 0x445570), read from the program. Good 0 is no good and has none. The bytes for the goods 27 to 31 belong to other fields
    // of the player's block in the original; no good has those numbers, and the engine gives them none.
    private int TransportPriority(int good) => _tables.TransportPriority(good);

    // A carrier on the flag it carries a good to (0x43c45e): it turns to the building north-west of the flag, state 0xe, with the
    // step's frame from the heights and its time on the timer, and goes on at once where the timer has not run out.
    private void Deliver(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (_, _, node) = Neighbour(settler, 4);
        var walks = (WalksRoad)settler.Activity;
        Become(settler, new HandsOver(walks.Load, walks.Heading));
        settler.Frame = (byte)(Height(node) - Height(here) + 0x5d);
        var old = settler.Timer;
        settler.Timer = (ushort)(old + _tables.StepTime(settler.Frame));
        History.RecordSettler(slot, EventType.Deliver, Cause.Goods);
        if (settler.Timer >= old)
        {
            Handover(slot, settler);
        }
    }

    // State 0xe (0x43c4ea) when its timer has run out. The first time the good goes into the building: its byte 8 or 9 (the log
    // and the goods from 9 on but 13 count in byte 9, the others in byte 8) gains 0x0f, and where that carries out of the byte the
    // word of both bytes becomes 0xffff and the good is counted in the building's storage, up to 50000. The castle's bytes are
    // 0xffff, so each good delivered to it counts. The carrier then turns the frame round, takes the other half of the step's
    // time, and the second time goes on in state 3 at the flag, where the exchange runs again.
    private void Handover(int slot, Settler settler)
    {
        while (true)
        {
            var hands = (HandsOver)settler.Activity;
            if (hands.Marked)
            {
                Become(settler, new WalksRoad(hands.Load, hands.Heading));
                History.RecordSettler(slot, EventType.Handover, Cause.Goods);
                WalkRoad(slot, settler);

                return;
            }

            var (_, _, node) = Neighbour(settler, 4);
            var good = hands.Load?.Good.Code ?? 0;
            var given = "";
            if (good != 0)
            {
                // The carrier's hands are empty; its item keeps the destination of the good (the residue).
                Become(settler, hands with { Load = null });
                if (BuildingAt(node) is { IsDemolishing: false } building)
                {
                    var counter = (good >= 9 ? good != 13 : good == 7) ? FillCounter.High : FillCounter.Low;
                    var sum = building.FillOf(counter) + 0x0f;
                    building.SetFill(counter, sum);
                    if (sum > 0xff)
                    {
                        building.FillLow = 0xff;
                        building.FillHigh = 0xff;
                        if (building.Type == BuildingType.Castle)
                        {
                            AddToStock(good);
                        }
                    }

                    var buildingSlot = _buildings.SlotOf(building);
                    History.RecordBuilding(
                        buildingSlot,
                        EventType.Handover,
                        Cause.Goods,
                        $"{History.SettlerId(slot)} good {good}"
                    );
                    given = $"{History.BuildingId(buildingSlot)} good {good}";
                }
            }

            var shape = 0x0d - (settler.Frame - 0x5d);
            settler.Frame = (byte)shape;
            settler.Activity = ((HandsOver)settler.Activity) with { Marked = true };
            var old = settler.Timer;
            settler.Timer = (ushort)(old + (_tables.StepTime(shape) >> 1));
            History.RecordSettler(slot, EventType.Handover, Cause.Goods, given.Length == 0 ? null : given);
            if (settler.Timer < old)
            {
                return;
            }
        }
    }

    private void AddToStock(int good)
    {
        int[] stock = [.. Stock];
        stock[good] = Math.Min(stock[good] + 1, 50000);
        Stock = stock;
    }

    private void MixGoods(ref ulong hash)
    {
        foreach (var word in FlagEncoder.Words(_flags))
        {
            hash = Mix(hash, word);
        }
    }
}
