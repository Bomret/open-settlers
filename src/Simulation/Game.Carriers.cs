using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

// The carriers: the roads' status bytes in the flag items, the flag table's pass that asks the castle for a carrier for a
// road without one, and the carrier's walk from the castle to its road and along it (docs/rules/carriers.md).
public sealed partial class Game
{
    // The job of a carrier: a settler of job 21 that has no job yet becomes one when a road asks for it.
    private const int CarrierJob = Settler.CarrierJob;
    private const int JoblessJob = 21;

    // The state of a carrier that waits on its road next to the flag at the road's far end.
    private const int WaitingState = 0x42;

    // The bytes of each flag's item that the carriers read and write: the status (byte 5; bit 7 is set when a request
    // for a carrier found none, and cleared by the retry scan, bit d when the road in direction d holds a carrier), and
    // for each direction the road byte (bytes 6 to 11).
    private readonly List<byte> _flagStatus = [];
    private readonly List<byte[]> _roadBytes = [];
    private int _flagCursor;

    /// <summary>
    /// The status byte of a flag's item (byte 5): bit 7 marks a request for a carrier that found none, until the retry
    /// scan clears it, and bit <c>d</c> a road in direction <c>d</c> that holds a carrier.
    /// </summary>
    public byte FlagStatus(int flag) => _flagStatus[flag];

    /// <summary>
    /// The byte of a flag's item for the road that leaves it in a direction (byte 6 + direction): bits 4 to 6 the road's
    /// length class (0 to 7), bit 7 while a carrier is on the way to the road, and the low nibble the carriers on it.
    /// </summary>
    public byte RoadByte(int flag, int direction) => _roadBytes[flag][direction];

    // The slot the next flag takes: the first one a taken-down flag left free, else the end of the table (the allocator 0x45d7ad).
    private int NextFlagSlot()
    {
        var free = _flags.FindIndex(flag => flag.IsFree);

        return free >= 0 ? free : _flags.Count;
    }

    // Puts a flag in the next slot; the item of the slot starts as zeros (the allocator clears it).
    private void AddFlag(Flag flag)
    {
        var slot = NextFlagSlot();
        if (slot == _flags.Count)
        {
            _flags.Add(flag);
            _flagStatus.Add(0);
            _roadBytes.Add(new byte[6]);
            _flagGoods.Add(new byte[8]);
            _goodsBit.Add(false);
            _goodsDestination.Add(new ushort[8]);
            _directionBytes.Add(new byte[6]);

            return;
        }

        _flags[slot] = flag;
        _flagStatus[slot] = 0;
        _roadBytes[slot] = new byte[6];
        _flagGoods[slot] = new byte[8];
        _goodsBit[slot] = false;
        _goodsDestination[slot] = new ushort[8];
        _directionBytes[slot] = new byte[6];
    }

    // Frees a flag's slot (0x45d974): the item keeps its bytes until a new flag takes the slot; the table's count falls past the
    // free slots at its end.
    private void FreeFlagSlot(int slot)
    {
        _flags[slot] = Flag.Free;
        while (_flags.Count > 0 && _flags[^1].IsFree)
        {
            var last = _flags.Count - 1;
            _flags.RemoveAt(last);
            _flagStatus.RemoveAt(last);
            _roadBytes.RemoveAt(last);
            _flagGoods.RemoveAt(last);
            _goodsBit.RemoveAt(last);
            _goodsDestination.RemoveAt(last);
            _directionBytes.RemoveAt(last);
        }
    }

    // The length class of a road (0x4454e7), kept in bits 4 to 6 of the road bytes of both its ends.
    private static byte LengthClass(int steps) =>
        (byte)(
            (
                steps switch
                {
                    <= 3 => 0,
                    <= 5 => 1,
                    6 => 2,
                    <= 9 => 3,
                    <= 12 => 4,
                    <= 17 => 5,
                    <= 23 => 6,
                    _ => 7,
                }
            ) << 4
        );

    private void SetRoadBytes(Road road)
    {
        var length = LengthClass(road.Directions.Count);
        _roadBytes[road.FromSlot][road.FromDirection] = length;
        _roadBytes[road.ToSlot][road.ToDirection] = length;
        SetDirectionBytes(road);
    }

    // The road that leaves a flag in a direction, or null.
    private Road? RoadAt(int flag, int direction)
    {
        foreach (var road in _roads)
        {
            if (
                (road.FromSlot == flag && road.FromDirection == direction)
                || (road.ToSlot == flag && road.ToDirection == direction)
            )
            {
                return road;
            }
        }

        return null;
    }

    // The flag at the other end of a road that leaves a flag in a direction, and the direction the road leaves it in.
    private static (int Flag, int Direction) FarEnd(Road road, int flag, int direction) =>
        road.FromSlot == flag && road.FromDirection == direction
            ? (road.ToSlot, road.ToDirection)
            : (road.FromSlot, road.FromDirection);

    // 0x428728, the second half: each pass clears bit 7 of the status of a window of flags from a cursor that wraps at the
    // end of the table, and stops after 10 flags that had the bit set. A flag whose request found no carrier asks again
    // at its next turn after that.
    private void ClearFlagRetryBits()
    {
        var count = _flags.Count;
        if (count == 0)
        {
            return;
        }

        if (count <= _flagCursor)
        {
            _flagCursor = 0;
        }

        var window = Math.Min(count - _flagCursor, _tables.ScanWindows[Map.Size.Value]);
        var left = 10;
        for (var i = 0; i < window; i++)
        {
            var had = (_flagStatus[_flagCursor] & 0x80) != 0;
            _flagStatus[_flagCursor] &= 0x7f;
            if (had && --left == 0)
            {
                return;
            }

            _flagCursor++;
        }
    }

    // 0x46414d, the flag table's pass: the flags whose slot divided by 32 is the phase each get their turn, once for each
    // cycle of 33 passes: the turn of the flag's goods (docs/rules/goods.md), then the road loop that asks for carriers.
    // **Not ported:** the second carrier of a road with 3 goods waiting to go one way (table 0x462acd), and the loss of
    // the roads' carrier bits at a flag with 7 goods.
    private void FlagPass()
    {
        for (var slot = _phase * 32; slot < Math.Min(_flags.Count, _phase * 32 + 32); slot++)
        {
            if (_flags[slot].IsFree)
            {
                continue;
            }

            GoodsTurn(slot);
            VisitFlag(slot);
        }
    }

    // The turn of a flag (0x464c36): for each direction from north to east that holds a road, a road whose byte shows no
    // carrier on it or on the way asks for one, unless a request of this flag found none since the retry scan. A road
    // with a carrier sets its bit in the status.
    private void VisitFlag(int slot)
    {
        var status = _flagStatus[slot];
        for (var direction = 5; direction >= 0; direction--)
        {
            if (RoadAt(slot, direction) is not { } road)
            {
                continue;
            }

            var value = _roadBytes[slot][direction];
            if ((value & 0xf) != 0)
            {
                status |= (byte)(1 << direction);
            }
            else if (
                (value & 0x80) == 0
                && (status & 0x80) == 0
                && !road.Water
                && !RequestCarrier(slot, direction, road)
            )
            {
                status |= 0x80;
            }
        }

        _flagStatus[slot] = status;
    }

    // 0x42b455: the road asks the castle for a carrier. The search runs over the land roads from both ends of the road
    // (the flag first, then the flag at the far end) and marks each flag it reaches with the end it came from; the castle's
    // flag is the only storage. The castle sends the settler of job 0 that waits there, or else makes a carrier of a settler
    // of job 21 that waits there (no tool). The carrier walks to the end of the road the search came from. **Not ported:**
    // the water roads' boatmen (job 1, 0x42b4bc).
    private bool RequestCarrier(int slot, int direction, Road road)
    {
        var (far, farDirection) = FarEnd(road, slot, direction);
        var end = StorageEnd(slot, far);
        var job = _waiting[CarrierJob] != 0 ? CarrierJob : JoblessJob;
        if (end < 0 || _waiting[job] == 0)
        {
            return false;
        }

        var settler = _settlers[_waiting[job]];
        _waiting[job] = 0;
        settler.Job = CarrierJob;
        var (flag, flagDirection) = end == 0 ? (slot, direction) : (far, farDirection);
        settler.Mode = flagDirection;
        settler.Target = flag;
        settler.State = 0xf;
        _leaving++;
        _roadBytes[slot][direction] |= 0x80;
        _roadBytes[far][farDirection] |= 0x80;

        return true;
    }

    // The end of the road from which the search reaches the castle's flag first: 0 the flag the request comes from, 1 the
    // flag at the far end, or -1 where no land road leads to it. The two ends are queued in that order, then each flag
    // queues the flags its roads lead to, directions from north to east, and keeps the first mark that reaches a flag.
    private int StorageEnd(int start, int far)
    {
        var marks = new int[_flags.Count];
        Array.Fill(marks, -1);
        marks[start] = 0;
        marks[far] = 1;
        var queue = new Queue<int>([start, far]);
        while (queue.Count > 0)
        {
            var flag = queue.Dequeue();
            if (flag == 0)
            {
                return marks[flag];
            }

            for (var direction = 5; direction >= 0; direction--)
            {
                if (
                    RoadAt(flag, direction) is { Water: false } road
                    && FarEnd(road, flag, direction).Flag is var next
                    && marks[next] < 0
                )
                {
                    marks[next] = marks[flag];
                    queue.Enqueue(next);
                }
            }
        }

        return -1;
    }

    // State 2 at the flag a carrier was sent to (0x4398ea): the carrier takes the road in the direction of its mode. Both
    // ends of the road lose bit 7 and count it as a carrier, and the carrier takes state 3 with the direction as its
    // heading and goes on at once.
    private void EnterRoad(int slot, Settler settler)
    {
        var direction = settler.Mode;
        if (RoadAt(settler.Target, direction) is not { } road)
        {
            return;
        }

        var (far, farDirection) = FarEnd(road, settler.Target, direction);
        _roadBytes[far][farDirection] = (byte)((_roadBytes[far][farDirection] & 0x7f) + 1);
        _roadBytes[settler.Target][direction] = (byte)((_roadBytes[settler.Target][direction] & 0x7f) + 1);
        settler.State = 3;
        settler.Heading = direction;
        settler.Mode = 0;
        settler.Next = 0;
        WalkRoad(slot, settler);
    }

    // State 3, a carrier on its road (0x43ad78 and the step routines of 0x43b508): when its timer has run out it steps along
    // the road as a settler does in state 2, from a flag in the direction of its heading, from a node of the road in the
    // direction the road goes on. A step that would end at a flag is its last: the carrier stays on the node and waits.
    private void WalkRoad(int slot, Settler settler)
    {
        var flags = Map.Layer(MapLayer.Flags);
        while (true)
        {
            var here = Map.Index(settler.X, settler.Y);
            int direction;
            if (settler.Heading < 0)
            {
                direction = settler.Heading + 6;
            }
            else if ((flags[here] & 0x80) == 0)
            {
                var rest = flags[here] & 0x3f & ~(1 << settler.Heading);
                if (rest == 0 || (rest & (rest - 1)) != 0)
                {
                    settler.Timer = 0;
                    return;
                }

                direction = Enumerable.Range(0, 6).First(d => (rest >> d & 1) != 0);
            }
            else
            {
                // A carrier on a flag: with a good for the building at this flag it goes in, else it swaps goods with the flag and
                // goes on along the road it came by.
                if (settler.Mode != 0 && settler.Target == Map.Layer(MapLayer.Resource)[here] + 1)
                {
                    Deliver(slot, settler);
                    return;
                }

                Exchange(settler, Map.Layer(MapLayer.Resource)[here]);
                direction = settler.Heading;
            }

            var (_, _, next) = Neighbour(settler, direction);
            if (
                (flags[next] & 0x80) != 0
                && settler.Mode == 0
                && (sbyte)settler.Next >= 0
                && (_directionBytes[Map.Layer(MapLayer.Resource)[next]][(direction + 3) % 6] & 0x80) == 0
            )
            {
                Wait(settler, (direction + 3) % 6, Map.Layer(MapLayer.Resource)[next]);
                return;
            }

            if (!Step(slot, settler, direction))
            {
                return;
            }
        }
    }

    // The carrier next to the flag at the end of its road (0x43b788 and its siblings, then 0x43bc52): its timer takes 0x3f, its
    // frame the idle frame of the heading, and its heading goes down by 6 (the marker of a step that is tried again at its next
    // turn). With a road that already holds two carriers it counts the tries in the byte after the state; at the fourth it
    // leaves the road (both ends count one carrier less) and takes the next state 0xff, which the flag's walk reads as a carrier
    // that was sent away. With a good that waits at the flag at the far end it does nothing more. Else it leaves the node (the
    // node holds no settler for the others to wait for) and takes state 0x42 with the flag and the direction of the road at
    // that flag as its target. The dword at 12 holds a pointer to the flag's item in the original; the engine keeps the slot in
    // the target. **Not ported:** the work at the flag in state 0x42 (the goods of the next slice).
    private void Wait(Settler settler, int back, int flag)
    {
        settler.Timer = 0x3f;
        settler.Frame = (byte)(settler.Heading + 0x6e);
        settler.Heading -= 6;
        var far = Neighbour(flag, back);
        var farDirection = (_directionBytes[flag][back] & 0x38) >> 3;
        if ((_roadBytes[flag][back] & 0xf) >= 2)
        {
            settler.Next = (settler.Next + 1) & 0xff;
            if (settler.Next >= 4)
            {
                settler.Next = 0;
                if ((_roadBytes[flag][back] & 0xf) >= 2)
                {
                    _roadBytes[flag][back]--;
                    _roadBytes[far][farDirection]--;
                    settler.Next = 0xff;
                }
            }

            return;
        }

        if ((_directionBytes[far][farDirection] & 0x80) != 0)
        {
            return;
        }

        settler.Stamp = (ushort)((settler.Stamp & 0xff00) | (byte)settler.Heading);
        settler.Mode = back;
        settler.Target = flag;
        settler.Heading = 0;
        settler.Next = 0;
        settler.State = WaitingState;
        _occupant.Remove(Map.Index(settler.X, settler.Y));
    }
}
