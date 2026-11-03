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

    // A good put on the first empty place of a flag (0x436b46): the place's destination is cleared and the flag's bit set.
    internal void LayGood(int flag, int place, int good)
    {
        _flagGoods[flag][place] = (byte)good;
        _goodsDestination[flag][place] = NoDestination;
        _goodsBit[flag] = true;
    }

    // The flag at the other end of the road that leaves a flag in a direction, or -1 where none does.
    private int Neighbour(int flag, int direction) =>
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
        if (found >= 0)
        {
            _goodsDestination[flag][place] = (ushort)(found + 1);
            _goodsBit[flag] = true;

            return;
        }

        // No storage is reached over the roads that hold a carrier: the good waits and the flag is asked again at its next
        // turn. **Not ported:** where some road of the flag holds a carrier, the original gives the good a direction at
        // once, chosen from what its registers hold (0x464539); the engine lets it wait as it does with none.
        _goodsBit[flag] = true;
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
                    || Neighbour(flag, direction) is not (>= 0 and var next)
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
                if (Neighbour(flag, direction) is >= 0 and var next && !marked[next])
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
                        && Neighbour(current, direction) is >= 0 and var other
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

        var way = label[destination];
        var waiting = _directionBytes[flag][way];
        if ((waiting & 0x80) == 0)
        {
            _directionBytes[flag][way] = (byte)(waiting | place | 0x80);
        }

        // **Not ported:** a good that waits for the same direction is replaced by this one where its priority is higher
        // (0x46501a, the table in the player's block); the engine has one good and so never replaces.
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
            if ((_directionBytes[Neighbour(settler.Target, direction)][(waiting & 0x38) >> 3] & 0x80) == 0)
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
