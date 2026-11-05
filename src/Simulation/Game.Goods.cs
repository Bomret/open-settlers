using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

// The goods part of the flag table's pass (0x46414d, docs/rules/goods.md): a good that lies on a flag gets the castle's flag
// as its destination and then the direction of the first road toward it, and the carrier who waits next to the flag on that
// road is told to fetch it (state 0x42, 0x42ff81).
public sealed partial class Game
{
    // The destination word of a place (flag item +0x14 + 2 * place) holds the slot of the destination flag as the original
    // numbers it: its first flag, slot 0, is a dummy the game's start takes (0x421530), so the castle's flag is slot 1 there
    // and the word is 0 for none. The engine's slot 0 is the castle's flag, so the word here is the engine's slot plus one.
    private const int NoDestination = 0;

    // The direction a good has been given is kept in bits 5 to 7 of its byte, as the direction plus one; the bits below hold
    // the good.
    private const int DirectionShift = 5;

    // Byte 4 bit 7 of each flag's item: the flag has goods that still need their turn. A put sets it, a turn clears it, and a
    // turn that could not finish sets it again.
    private readonly List<bool> _goodsBit = [];

    // The destination words of the places of each flag (see NoDestination).
    private readonly List<ushort[]> _goodsDestination = [];

    // Bytes 0x3c to 0x41 of each flag's item, one for each direction: bit 7 a good waits to go that way, bits 0 to 2 its place,
    // bits 3 to 5 the direction in which the road leaves the flag at its far end (written when the road is made).
    private readonly List<byte[]> _directionBytes = [];

    /// <summary>The logs the castle holds: the count of its storage item for the good 7.</summary>
    public int CastleLogs => Stock.Count > GoodLog ? Stock[GoodLog] : 0;

    /// <summary>Whether a flag's item has byte 4 bit 7 set: goods wait for their next turn at the flag pass.</summary>
    public bool FlagGoodsBit(int flag) => _goodsBit[flag];

    /// <summary>The destination words of a flag's 8 places (the original's numbering of the flags: 0 for none, else the slot plus one).</summary>
    public IReadOnlyList<int> FlagDestinations(int flag) => [.. _goodsDestination[flag]];

    /// <summary>The bytes of a flag's item at 0x3c to 0x41, for the directions 0 to 5.</summary>
    public IReadOnlyList<int> FlagDirectionBytes(int flag) => [.. _directionBytes[flag]];

    private void SetDirectionBytes(Road road)
    {
        _directionBytes[road.FromSlot][road.FromDirection] = (byte)(road.ToDirection << 3);
        _directionBytes[road.ToSlot][road.ToDirection] = (byte)(road.FromDirection << 3);
    }

    // A good put on a place of a flag (0x436b46): the place's destination is cleared (or set to a given one, for a test that
    // lays a good with a destination) and the flag's bit set.
    internal void LayGood(int flag, int place, int good, int destination = NoDestination)
    {
        _flagGoods[flag][place] = (byte)good;
        _goodsDestination[flag][place] = (ushort)destination;
        _goodsBit[flag] = true;
    }

    // The flag at the other end of the road that leaves a flag in a direction, or -1 where none does.
    private int FlagNeighbour(int flag, int direction) =>
        RoadAt(flag, direction) is { } road ? FarEnd(road, flag, direction).Flag : -1;

    // The turn of a flag's goods (0x46414d, from the thermometer at 0x46427c to the places' loop ending at 0x4650b9). It runs
    // when the flag's bit is set: the bit goes out, and each place from the last to the first that holds a good without a
    // direction gets a destination, or, with one, a direction.
    private void GoodsTurn(int flag)
    {
        if (!_goodsBit[flag])
        {
            return;
        }

        _goodsBit[flag] = false;
        var load = Load(flag);
        var goods = _flagGoods[flag];
        for (var place = 7; place >= 0; place--)
        {
            if (goods[place] == 0 || (goods[place] >> DirectionShift) != 0)
            {
                continue;
            }

            if (_goodsDestination[flag][place] == NoDestination)
            {
                Destine(flag, place);
            }
            else
            {
                Direct(flag, place, load);
            }
        }
    }

    // The goods that head each direction, as the four bytes the original builds (0x46427c): byte k holds, for each direction,
    // whether more than k goods have been given it. Index k of the result is that byte.
    private byte[] Load(int flag)
    {
        var count = new int[6];
        foreach (var good in _flagGoods[flag])
        {
            if (good >> DirectionShift != 0)
            {
                count[(good >> DirectionShift) - 1]++;
            }
        }

        var bytes = new byte[4];
        for (var direction = 0; direction < 6; direction++)
        {
            for (var k = 0; k < 4 && k < count[direction]; k++)
            {
                bytes[k] |= (byte)(1 << direction);
            }
        }

        return bytes;
    }

    // The destination of a good with none (0x464364 to 0x46444c, 0x45d3ea). The table at 0x464117 sends a log to the request
    // search first: it looks for a flag whose request word asks for the good, and none does until buildings ask for goods
    // (not ported), so the search that follows is the one that counts: the storage search over the roads that hold a
    // carrier. It gives the castle's flag, or none.
    private void Destine(int flag, int place)
    {
        var found = StorageSearch(flag);
        _goodsBit[flag] = true;
        if (found >= 0)
        {
            _goodsDestination[flag][place] = (ushort)(found + 1);

            return;
        }

        // No storage is reached over the roads that hold a carrier: the good waits and the flag is asked again at its next
        // turn. **Not ported:** where some road of the flag holds a carrier, the original gives the good a direction at
        // once, chosen from what its registers hold (0x464539); the engine lets it wait as it does with none.
    }

    // 0x45d3ea: the search from a flag over the roads that hold a carrier (the bits of the status byte, from north to east) for
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
                    (_flagStatus[flag] >> direction & 1) == 0
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

    // A good with a destination gets the direction of the first road toward it (0x464aa8, 0x464c36 to 0x4650b9). The search
    // runs from the flag's roads that hold a carrier, the ones with the fewest goods heading that way first (0x464bb2 seeds
    // one load after the other), and spreads each seed's direction over the roads that hold a carrier, level by level; the
    // direction that reaches the destination first is the good's.
    private void Direct(int flag, int place, byte[] load)
    {
        var destination = _goodsDestination[flag][place] - 1;
        var marked = new bool[_flags.Count];
        var label = new int[_flags.Count];
        marked[flag] = true;
        label[flag] = 6;
        var queue = new List<int>();
        var carried = _flagStatus[flag] & 0x3f;
        void Seed(int mask)
        {
            for (var direction = 5; direction >= 0; direction--)
            {
                if ((mask >> direction & 1) == 0)
                {
                    continue;
                }

                carried &= ~(1 << direction);
                if (FlagNeighbour(flag, direction) is >= 0 and var next && !marked[next])
                {
                    marked[next] = true;
                    label[next] = direction;
                    queue.Add(next);
                }
            }
        }

        Seed(~load[0] & 0x3f & carried);
        for (var k = 1; k < 4 && carried != 0; k++)
        {
            Seed(load[k - 1] ^ load[k]);
        }

        if (carried != 0)
        {
            Seed(load[3]);
        }

        if (queue.Count == 0)
        {
            _goodsBit[flag] = true;

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
                        (_flagStatus[current] >> direction & 1) != 0
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
            _goodsDestination[flag][place] = NoDestination;
            _goodsBit[flag] = true;

            return;
        }

        // Where no good waits for the road the good takes the road's byte. Where one waits, the good takes its place only with a
        // higher transport priority (0x46501a): the byte keeps bit 7 and bits 3 to 6 and gets the new place, and the good that
        // loses the place keeps its direction and waits until the exchange lines it up again.
        var way = label[destination];
        var waiting = _directionBytes[flag][way];
        if ((waiting & 0x80) == 0)
        {
            WaitForRoad(flag, way, place);
        }
        else if (
            TransportPriority(_flagGoods[flag][waiting & 7] & 0x1f) < TransportPriority(_flagGoods[flag][place] & 0x1f)
        )
        {
            _directionBytes[flag][way] = (byte)((waiting & 0xf8) | place);
        }

        _flagGoods[flag][place] |= (byte)((way + 1) << DirectionShift);
    }

    // State 0x42 (0x42ff81): a carrier who waits next to a flag looks for a good that waits to go along its road, at the flag it
    // stands next to (it goes on in the heading it came) and at the flag at the road's far end (it turns round). With one it
    // takes state 0x43 and runs it. **Not ported:** what the carrier does in state 3 and after it with a good to fetch (the
    // next stage); it walks on as it does with none.
    private void Fetch(int slot, Settler settler)
    {
        var direction = settler.Mode;
        var waiting = _directionBytes[settler.Target][direction];
        byte heading;
        if ((waiting & 0x80) != 0)
        {
            heading = (byte)((byte)settler.Stamp + 6);
        }
        else
        {
            if ((_directionBytes[FlagNeighbour(settler.Target, direction)][(waiting & 0x38) >> 3] & 0x80) == 0)
            {
                return;
            }

            var turned = (byte)settler.Stamp - 3;
            heading = (byte)(turned < 0 ? turned + 6 : turned);
        }

        settler.Heading = heading;
        settler.State = 0x43;
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
        settler.State = 3;
        settler.Stamp = _clock;
        settler.Timer = 0;
        settler.Mode = 0;
        settler.Next = 0;
    }

    // The good on a place waits for the road that leaves the flag in a direction: bit 7 and the place go into the road's byte.
    internal void WaitForRoad(int flag, int way, int place) => _directionBytes[flag][way] |= (byte)(0x80 | place);

    // A carrier on a flag (state 3, 0x43af07 to 0x43b410): with the heading it came in, the road it works, it swaps goods with the
    // flag. Where a good waits for that road (byte 0x3c + heading, bit 7) the carrier takes it, and leaves the good it carries
    // in the same place, destination word and all; where none waits and the carrier carries one, the good goes on the first
    // empty place. A flag where something was laid is asked again by its next turn (byte 4, bit 7). The byte of the road is
    // then made again: the good of the greatest transport priority that was given the road's direction waits for it next.
    internal void Exchange(Settler settler, int flag)
    {
        var way = settler.Heading;
        var goods = _flagGoods[flag];
        var waiting = _directionBytes[flag][way];
        if ((waiting & 0x80) != 0)
        {
            var place = waiting & 7;
            settler.Next = 0;
            if (settler.Mode != 0)
            {
                _goodsBit[flag] = true;
                var good = goods[place];
                var destination = _goodsDestination[flag][place];
                goods[place] = (byte)settler.Mode;
                _goodsDestination[flag][place] = (ushort)settler.Target;
                settler.Mode = good & 0x1f;
                settler.Target = destination;
            }
            else
            {
                settler.Mode = goods[place] & 0x1f;
                settler.Target = _goodsDestination[flag][place];
                goods[place] = 0;
            }

            _directionBytes[flag][way] = (byte)(waiting & 0x78);
            var best = 0;
            var found = -1;
            for (var i = 0; i < goods.Length; i++)
            {
                if (
                    goods[i] != 0
                    && (goods[i] & 0xe0) == (way + 1) << DirectionShift
                    && TransportPriority(goods[i] & 0x1f) > best
                )
                {
                    best = TransportPriority(goods[i] & 0x1f);
                    found = i;
                }
            }

            if (found >= 0)
            {
                WaitForRoad(flag, way, found);
            }
        }
        else if (settler.Mode != 0)
        {
            var place = Array.IndexOf(goods, (byte)0);
            if (place >= 0)
            {
                _goodsBit[flag] = true;
                goods[place] = (byte)settler.Mode;
                settler.Mode = 0;
                _goodsDestination[flag][place] = (ushort)settler.Target;
            }
        }
    }

    // The transport priority of a good (a byte of the table in the player's block, +good - 0x55 from the pointer +0x64 + 4 *
    // player, read at 0x43b054): the rank the player's setup at a new game writes for each good 1 to 26 (0x42055e calls
    // 0x445570), read from the program. Good 0 is no good and has none. The bytes for the goods 27 to 31 belong to other fields
    // of the player's block in the original; no good has those numbers, and the engine gives them none.
    private int TransportPriority(int good) =>
        good is >= 1 and <= StartStock.Goods ? _tables.TransportPriorities[good - 1] : 0;

    // A carrier on the flag it carries a good to (0x43c45e): it turns to the building north-west of the flag, state 0xe, with the
    // step's frame from the heights and its time on the timer, and goes on at once where the timer has not run out.
    private void Deliver(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (_, _, node) = Neighbour(settler, 4);
        settler.State = 0xe;
        settler.Next = 0;
        settler.Frame = (byte)(Height(node) - Height(here) + 0x5d);
        var old = settler.Timer;
        settler.Timer = (ushort)(old + _tables.StepTime(settler.Frame));
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
            if (settler.Next != 0)
            {
                settler.Next = 0;
                settler.State = 3;
                WalkRoad(slot, settler);

                return;
            }

            var (_, _, node) = Neighbour(settler, 4);
            var good = settler.Mode;
            if (good != 0)
            {
                settler.Mode = 0;
                if (BuildingAt(node) is { } building && (building.Status & 0x20) == 0)
                {
                    var shift = (good >= 9 ? good != 13 : good == 7) ? 8 : 0;
                    var sum = ((building.Fill >> shift) & 0xff) + 0x0f;
                    building.Fill = (ushort)((building.Fill & ~(0xff << shift)) | ((sum & 0xff) << shift));
                    if (sum > 0xff)
                    {
                        building.Fill = 0xffff;
                        if (building.Type == Building.CastleType)
                        {
                            AddToStock(good);
                        }
                    }
                }
            }

            var shape = 0x0d - (settler.Frame - 0x5d);
            settler.Frame = (byte)shape;
            settler.Next = 0xff;
            var old = settler.Timer;
            settler.Timer = (ushort)(old + (_tables.StepTime(shape) >> 1));
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
        for (var flag = 0; flag < _flags.Count; flag++)
        {
            hash = Mix(hash, _goodsBit[flag] ? 1 : 0);
            foreach (var destination in _goodsDestination[flag])
            {
                hash = Mix(hash, destination);
            }

            foreach (var value in _directionBytes[flag])
            {
                hash = Mix(hash, value);
            }
        }
    }
}
