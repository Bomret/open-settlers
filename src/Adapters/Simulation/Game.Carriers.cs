using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

// The carriers: the roads' state in the flag sides, the flag table's pass that asks the castle for a carrier for a
// road without one, and the carrier's walk from the castle to its road and along it (docs/rules/carriers.md).
public sealed partial class Game
{
    private int _flagCursor;

    // The command cause of each road a command built: the carrier sent to it carries it on until it waits on its road. A road no
    // command built (a layout's) has none. The request of a carrier takes the entry.
    private readonly Dictionary<Road, Cause> _roadCauses = [];

    // Puts a flag in the next slot; the item of the slot starts as zeros (the allocator clears it).
    private int AddFlag(int x, int y)
    {
        var slot = _flags.Add(x, y);
        History.FillFlag(slot);

        return slot;
    }

    // Frees a flag's slot (0x45d974): the item keeps its values until a new flag takes the slot; the table's count falls past the
    // free slots at its end.
    private void FreeFlagSlot(int slot) => _flags.Free(slot);

    private Road? RoadAt(int flag, int direction) => _roads.RoadAt(flag, direction);

    private int FlagNeighbour(int flag, int direction) => _roads.FlagNeighbour(flag, direction);

    // A new road gives both its sides the road's length class, no carrier, and the direction of the other end.
    private void SetSides(Road road)
    {
        var length = LengthClass.OfSteps(road.Directions.Count);
        var from = _flags[road.FromSlot].Side(road.FromDirection);
        var to = _flags[road.ToSlot].Side(road.ToDirection);
        from.SetLength(length, carrierOnTheWay: false);
        to.SetLength(length, carrierOnTheWay: false);
        from.FarDirection = road.ToDirection;
        to.FarDirection = road.FromDirection;
        from.WaitingPlace = null;
        to.WaitingPlace = null;
    }

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

        var window = Math.Min(count - _flagCursor, _tables.ScanWindow(Map.Size.Value));
        var left = 10;
        for (var i = 0; i < window; i++)
        {
            var had = _flags[_flagCursor].CarrierRequestFailed;
            _flags[_flagCursor].CarrierRequestFailed = false;
            if (had)
            {
                History.RecordFlag(_flagCursor, EventType.ClearFlagRetryBits, Cause.Carriers);
            }

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
        // The flag takes what the turn found only at its end: a request made in the turn records the flag as it stood before.
        var flag = _flags[slot];
        var failed = flag.CarrierRequestFailed;
        var holders = new List<FlagSide>();
        for (var direction = 5; direction >= 0; direction--)
        {
            if (RoadAt(slot, direction) is not { } road)
            {
                continue;
            }

            var side = flag.Side(direction);
            if (side.Carriers != 0)
            {
                holders.Add(side);
            }
            else if (!side.CarrierOnTheWay && !failed && !road.Water && !RequestCarrier(slot, direction, road))
            {
                failed = true;
            }
        }

        var changed = failed != flag.CarrierRequestFailed || holders.Any(side => !side.HoldsCarrier);
        flag.CarrierRequestFailed = failed;
        holders.ForEach(side => side.HoldsCarrier = true);
        if (changed)
        {
            History.RecordFlag(slot, EventType.VisitFlag, Cause.Carriers);
        }
    }

    // 0x42b455: the road asks the castle for a carrier. The search runs over the land roads from both ends of the road
    // (the flag first, then the flag at the far end) and marks each flag it reaches with the end it came from; the castle's
    // flag is the only storage. The castle sends the settler of job 0 that waits there, or else makes a carrier of a settler
    // of job 21 that waits there (no tool): a settler without a job becomes a carrier, and stays the same settler. The carrier
    // walks to the end of the road the search came from. **Not ported:** the water roads' boatmen (job 1, 0x42b4bc).
    private bool RequestCarrier(int slot, int direction, Road road)
    {
        var (far, farDirection) = FarEnd(road, slot, direction);
        var end = StorageEnd(slot, far);
        var job = _waiting[Job.Carrier.Code] != 0 ? Job.Carrier : Job.Jobless;
        if (end < 0 || _waiting[job.Code] == 0)
        {
            return false;
        }

        var carrier = _waiting[job.Code];
        var settler = _settlers[carrier];
        _waiting[job.Code] = 0;
        settler.Job = Job.Carrier;
        var (flag, flagDirection) = end == 0 ? (slot, direction) : (far, farDirection);
        Become(settler, new Leaving(new ToRoad(flag, flagDirection)));
        _leaving++;
        _flags[slot].Side(direction).CarrierOnTheWay = true;
        _flags[far].Side(farDirection).CarrierOnTheWay = true;
        var id = History.SettlerId(carrier);
        History.EndCarry(carrier);
        var cause = Cause.Carriers;
        if (_roadCauses.Remove(road, out var placed))
        {
            History.CarryCause(carrier, placed);
            cause = placed;
        }

        History.RecordSettler(carrier, EventType.CarrierRequested, cause, History.FlagId(flag).ToString());
        History.RecordFlag(slot, EventType.CarrierRequested, cause, id.ToString());
        History.RecordFlag(far, EventType.CarrierRequested, cause, id.ToString());

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
        var (flag, direction) = settler.Activity.GoingTo is ToRoad errand ? (errand.At, errand.Direction) : default;
        if (RoadAt(flag, direction) is not { } road)
        {
            return;
        }

        var (far, farDirection) = FarEnd(road, flag, direction);
        CountCarrier(_flags[far].Side(farDirection));
        CountCarrier(_flags[flag].Side(direction));
        Become(settler, new WalksRoad(null, Heading.Facing(direction)));
        var id = History.SettlerId(slot);
        History.RecordSettler(slot, EventType.EnterRoad, Cause.Carriers, History.FlagId(flag).ToString());
        History.RecordFlag(flag, EventType.EnterRoad, Cause.Carriers, id.ToString());
        History.RecordFlag(far, EventType.EnterRoad, Cause.Carriers, id.ToString());
        WalkRoad(slot, settler);
    }

    // A carrier has entered a road: no carrier is on the way to it any more and it holds one more.
    private static void CountCarrier(FlagSide side)
    {
        side.CarrierOnTheWay = false;
        side.Carriers++;
    }

    // On a node of a road, the one direction of the road other than the one the settler came by (its heading); -1
    // where the road has none or has more than one.
    private static int RoadContinuation(RoadSet roads, int heading)
    {
        var rest = roads.Without(heading);

        return rest.Count == 1 ? rest.First : -1;
    }

    // State 3, a carrier on its road (0x43ad78 and the step routines of 0x43b508): when its timer has run out it steps along
    // the road as a settler does in state 2, from a flag in the direction of its heading, from a node of the road in the
    // direction the road goes on. A step that would end at a flag is its last: the carrier stays on the node and waits.
    private void WalkRoad(int slot, Settler settler)
    {
        while (true)
        {
            var walks = (WalksRoad)settler.Activity;
            var here = Map.Index(settler.X, settler.Y);
            int direction;
            if (walks.Heading.IsRetried)
            {
                direction = walks.Heading.Direction;
            }
            else if (!Map.At(here).FlagMark)
            {
                direction = RoadContinuation(Map.At(here).Roads, walks.Heading.Direction);
                if (direction < 0)
                {
                    settler.Timer = 0;
                    History.RecordSettler(slot, EventType.WalkRoad, Cause.Carriers);
                    return;
                }
            }
            else
            {
                // A carrier on a flag: with a good for the building at this flag it goes in, else it swaps goods with the flag and
                // goes on along the road it came by.
                if (walks.Load is { Destination: { } destination } && destination == Map.At(here).LinkedSlot)
                {
                    Deliver(slot, settler);
                    return;
                }

                Exchange(slot, settler, Map.At(here).LinkedSlot);
                direction = ((WalksRoad)settler.Activity).Heading.Direction;
            }

            var (_, _, next) = Neighbour(settler, direction);
            if (
                Map.At(next).FlagMark
                && ((WalksRoad)settler.Activity) is { Load: null, Marked: false }
                && _flags[Map.At(next).LinkedSlot].Side((direction + 3) % 6).WaitingPlace is null
            )
            {
                Wait(slot, settler, (direction + 3) % 6, Map.At(next).LinkedSlot);
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
    // **Inferred:** the fourth turn at a road that holds two carriers is read from the code; no emulator or debugger run reaches
    // it. Open lead in docs/milestones/m3-first-loop.md (the fourth turn of a carrier at the end of a road that holds two).
    private void Wait(int slot, Settler settler, int back, int flag)
    {
        var walks = (WalksRoad)settler.Activity;
        settler.Timer = 0x3f;
        settler.Frame = (byte)(walks.Heading.Value + 0x6e);
        var approach = walks.Heading.Retried;
        settler.Activity = walks with { Heading = approach };
        var far = FlagNeighbour(flag, back);
        var side = _flags[flag].Side(back);
        var farDirection = side.FarDirection;
        if (side.Carriers >= 2)
        {
            var waits = (walks.Waits + 1) & 0xff;
            settler.Activity = walks with { Heading = approach, Waits = waits };
            if (waits >= 4)
            {
                side.Carriers--;
                _flags[far].Side(farDirection).Carriers--;
                settler.Activity = walks with { Heading = approach, Waits = 0, Marked = true };
                var id = History.SettlerId(slot);
                History.RecordFlag(flag, EventType.Wait, Cause.Carriers, id.ToString());
                History.RecordFlag(far, EventType.Wait, Cause.Carriers, id.ToString());
            }

            History.RecordSettler(slot, EventType.Wait, Cause.Carriers);

            return;
        }

        if (_flags[far].Side(farDirection).WaitingPlace is not null)
        {
            History.RecordSettler(slot, EventType.Wait, Cause.Carriers);

            return;
        }

        // The approach moves into the low byte of the stamp, which the encoder writes from the activity.
        settler.Stamp = (ushort)(settler.Stamp & 0xff00);
        settler.Activity = new WaitsAtFlag(flag, back, approach);
        _occupant.Remove(Map.Index(settler.X, settler.Y));
        History.RecordSettler(slot, EventType.Wait, Cause.Carriers);
    }
}
