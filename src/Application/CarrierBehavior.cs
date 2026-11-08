using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

// The carriers: the roads' state in the flag sides, the flag table's pass that asks the castle for a carrier for a road
// without one, and the carrier's walk from the castle to its road and along it (docs/rules/carriers.md).

/// <summary>
/// The behaviour of the carriers: the flag table's pass with the carrier requests, the carrier's walk into its road and along
/// it, its wait next to the flag at the road's end, the fetch of a good that waits, the delivery of a good to the building at a
/// flag, and the handover into it. It handles the activities <see cref="WalksRoad"/>, <see cref="WaitsAtFlag"/>,
/// <see cref="ClaimsNode"/>, and <see cref="HandsOver"/>.
/// </summary>
/// <param name="movement">The settlers' ground: the node of each settler, the timer, the change of activity.</param>
/// <param name="settlers">The settler table.</param>
/// <param name="buildings">The building table: the building a good is handed into.</param>
/// <param name="flags">The flag table.</param>
/// <param name="roads">The roads between the flags.</param>
/// <param name="history">The history the behaviour records its changes in (no rule reads it).</param>
/// <param name="goods">The goods on the flags: the goods turn and the exchange.</param>
/// <param name="roster">The castle's list of waiting settlers, which a carrier request takes a settler from.</param>
/// <param name="step">The shared walk's step along a road: true where the settler goes on within its turn.</param>
/// <param name="addToStock">A good was handed into the castle: the castle's stock counts it.</param>
/// <param name="inLayout">Whether a layout is being built: its rules record their events with the cause layout.</param>
public sealed class CarrierBehavior(
    SettlerMovement movement,
    SettlerTable settlers,
    BuildingTable buildings,
    FlagTable flags,
    RoadNetwork roads,
    GameHistory history,
    GoodsTransport goods,
    ICastleRoster roster,
    Func<int, Settler, int, bool> step,
    Action<int> addToStock,
    Func<bool> inLayout
) : IRoadCarriers
{
    // The command cause of each road a command built: the carrier sent to it carries it on until it waits on its road. A road no
    // command built (a layout's) has none. The request of a carrier takes the entry.
    private readonly Dictionary<Road, Cause> _roadCauses = [];

    private Map Map => movement.Map;

    private Cause RuleCause => inLayout() ? Cause.Layout : Cause.Settlers;

    /// <summary>The flag the scan that clears the flags' retry bits goes on from at the next pass.</summary>
    public int FlagCursor { get; private set; }

    /// <summary>Registers the behaviour of the carriers' activities.</summary>
    public void Register(ActivityRegistry registry)
    {
        registry.Register<WalksRoad>(
            (slot, settler, _) =>
            {
                if (movement.TimerRan(settler))
                {
                    WalkRoad(slot, settler);
                }
                else
                {
                    history.RecordSettler(slot, EventType.Visited, RuleCause);
                }
            }
        );
        registry.Register<WaitsAtFlag>((slot, settler, _) => Fetch(slot, settler));
        registry.Register<ClaimsNode>((slot, settler, _) => Claim(slot, settler));
        registry.Register<HandsOver>(
            (slot, settler, _) =>
            {
                if (movement.TimerRan(settler))
                {
                    Handover(slot, settler);
                }
                else
                {
                    history.RecordSettler(slot, EventType.Visited, RuleCause);
                }
            }
        );
    }

    /// <summary>
    /// The cause of the command that built a road: the carrier sent to the road carries it on until it waits there. The road's
    /// request takes the entry (<see cref="RequestCarrier"/>).
    /// </summary>
    public void CarryCause(Road road, Cause command) => _roadCauses[road] = command;

    /// <summary>The road holds no command cause any more (it was taken down, or joined or split into other roads).</summary>
    public void ForgetCause(Road road) => _roadCauses.Remove(road);

    /// <summary>A new road gives both its sides the road's length class, no carrier, and the direction of the other end.</summary>
    public void SetSides(Road road)
    {
        var length = LengthClass.OfSteps(road.Directions.Count);
        var from = flags[road.FromSlot].Side(road.FromDirection);
        var to = flags[road.ToSlot].Side(road.ToDirection);
        from.SetLength(length, carrierOnTheWay: false);
        to.SetLength(length, carrierOnTheWay: false);
        from.FarDirection = road.ToDirection;
        to.FarDirection = road.FromDirection;
        from.WaitingPlace = null;
        to.WaitingPlace = null;
    }

    /// <summary>
    /// 0x428728, the second half: each pass clears bit 7 of the status of a window of flags from a cursor that wraps at the
    /// end of the table, and stops after 10 flags that had the bit set. A flag whose request found no carrier asks again
    /// at its next turn after that.
    /// </summary>
    public void ClearFlagRetryBits()
    {
        var count = flags.Count;
        if (count == 0)
        {
            return;
        }

        if (count <= FlagCursor)
        {
            FlagCursor = 0;
        }

        var window = Math.Min(count - FlagCursor, movement.Tables.ScanWindow(Map.Size.Value));
        var left = 10;
        for (var i = 0; i < window; i++)
        {
            var had = flags[FlagCursor].CarrierRequestFailed;
            flags[FlagCursor].CarrierRequestFailed = false;
            if (had)
            {
                history.RecordFlag(FlagCursor, EventType.ClearFlagRetryBits, Cause.Carriers);
            }

            if (had && --left == 0)
            {
                return;
            }

            FlagCursor++;
        }
    }

    /// <summary>
    /// 0x46414d, the flag table's pass: the flags whose slot divided by 32 is the phase each get their turn, once for each
    /// cycle of 33 passes: the turn of the flag's goods (docs/rules/goods.md), then the road loop that asks for carriers.
    /// **Not ported:** the second carrier of a road with 3 goods waiting to go one way (table 0x462acd), and the loss of
    /// the roads' carrier bits at a flag with 7 goods.
    /// </summary>
    public void FlagPass(int phase)
    {
        for (var slot = phase * 32; slot < Math.Min(flags.Count, phase * 32 + 32); slot++)
        {
            if (flags[slot].IsFree)
            {
                continue;
            }

            goods.Turn(slot);
            VisitFlag(slot);
        }
    }

    // The turn of a flag (0x464c36): for each direction from north to east that holds a road, a road whose byte shows no
    // carrier on it or on the way asks for one, unless a request of this flag found none since the retry scan. A road
    // with a carrier sets its bit in the status.
    private void VisitFlag(int slot)
    {
        // The flag takes what the turn found only at its end: a request made in the turn records the flag as it stood before.
        var flag = flags[slot];
        var failed = flag.CarrierRequestFailed;
        var holders = new List<FlagSide>();
        for (var direction = 5; direction >= 0; direction--)
        {
            if (roads.RoadAt(slot, direction) is not { } road)
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
            history.RecordFlag(slot, EventType.VisitFlag, Cause.Carriers);
        }
    }

    // 0x42b455: the road asks the castle for a carrier. The search runs over the land roads from both ends of the road
    // (the flag first, then the flag at the far end) and marks each flag it reaches with the end it came from; the castle's
    // flag is the only storage. The castle sends the settler of job 0 that waits there, or else makes a carrier of a settler
    // of job 21 that waits there (no tool): a settler without a job becomes a carrier, and stays the same settler. The carrier
    // walks to the end of the road the search came from. **Not ported:** the water roads' boatmen (job 1, 0x42b4bc).
    private bool RequestCarrier(int slot, int direction, Road road)
    {
        var (far, farDirection) = RoadNetwork.FarEnd(road, slot, direction);
        var end = StorageEnd(slot, far);
        var job = roster.Waiting(Job.Carrier) != 0 ? Job.Carrier : Job.Jobless;
        if (end < 0 || roster.Waiting(job) == 0)
        {
            return false;
        }

        var carrier = roster.Waiting(job);
        var settler = settlers[carrier];
        roster.Take(job, carrier);
        settler.Job = Job.Carrier;
        var (flag, flagDirection) = end == 0 ? (slot, direction) : (far, farDirection);
        movement.Become(settler, new Leaving(new ToRoad(flag, flagDirection)));
        roster.WorkerSent();
        flags[slot].Side(direction).CarrierOnTheWay = true;
        flags[far].Side(farDirection).CarrierOnTheWay = true;
        var id = history.SettlerId(carrier);
        history.EndCarry(carrier);
        var cause = Cause.Carriers;
        if (_roadCauses.Remove(road, out var placed))
        {
            history.CarryCause(carrier, placed);
            cause = placed;
        }

        history.RecordSettler(carrier, EventType.CarrierRequested, cause, history.FlagId(flag).ToString());
        history.RecordFlag(slot, EventType.CarrierRequested, cause, id.ToString());
        history.RecordFlag(far, EventType.CarrierRequested, cause, id.ToString());

        return true;
    }

    // The end of the road from which the search reaches the castle's flag first: 0 the flag the request comes from, 1 the
    // flag at the far end, or -1 where no land road leads to it. The two ends are queued in that order, then each flag
    // queues the flags its roads lead to, directions from north to east, and keeps the first mark that reaches a flag.
    private int StorageEnd(int start, int far)
    {
        var marks = new int[flags.Count];
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
                    roads.RoadAt(flag, direction) is { Water: false } road
                    && RoadNetwork.FarEnd(road, flag, direction).Flag is var next
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

    /// <summary>
    /// Entry state <c>waiting-on-road</c> of the carrier: the settler the castle sends for the road (<c>0x42b455</c>: the
    /// waiting settler of job 0, else a waiting settler of job 21 made a carrier) has walked its road and waits on the
    /// node next to the flag at the road's far end, the end the castle's flag is not reached from first (<c>0x43bc52</c>:
    /// state <c>0x42</c>, timer <c>0x3f</c>, frame <c>0x6e</c> plus the direction, the node's settler word cleared). Both ends
    /// of the road count the carrier (<c>0x4398ea</c>). Refused for a flag or direction with no land road, a road that has
    /// a carrier or one on the way, a road no land road joins to the castle, and a castle that holds no such settler. It returns
    /// why it refused, or null.
    /// </summary>
    public string? PlaceCarrierOnRoad(int x, int y, int direction)
    {
        var start = flags.SlotAt(x, y);
        if (start < 0 || roads.RoadAt(start, direction) is not { Water: false } road)
        {
            return "no land road leaves a flag there in that direction";
        }

        var (far, farDirection) = RoadNetwork.FarEnd(road, start, direction);
        var startSide = flags[start].Side(direction);
        var farSide = flags[far].Side(farDirection);
        if (startSide.CarrierOnTheWay || startSide.Carriers != 0 || farSide.CarrierOnTheWay || farSide.Carriers != 0)
        {
            return "the road has its carrier already";
        }

        var end = StorageEnd(start, far);
        if (end < 0)
        {
            return "no land road leads from the road to the castle";
        }

        var job = roster.CastleSettler(Job.Carrier) >= 0 ? Job.Carrier : Job.Jobless;
        var slot = roster.CastleSettler(job);
        if (slot < 0)
        {
            return "the castle holds no carrier";
        }

        // The carrier enters the road at the end the castle is reached from and waits at the other.
        var (waitFlag, waitDirection) = end == 0 ? (far, farDirection) : (start, direction);
        var (dx, dy) = Direction.Steps[waitDirection];
        var settler = settlers[slot];
        roster.Take(job, slot);
        settler.Job = Job.Carrier;
        settler.X = (flags[waitFlag].X + dx) & (Map.Size.Width - 1);
        settler.Y = (flags[waitFlag].Y + dy) & (Map.Size.Height - 1);
        settler.Frame = (byte)(waitDirection + 0x6e);
        settler.Timer = 0x3f;
        // The stamp's low byte holds the approach, which the activity keeps.
        settler.Stamp = (ushort)(movement.Clock & 0xff00);
        settler.Activity = new WaitsAtFlag(waitFlag, waitDirection, Heading.Facing(waitDirection).Retried);
        CountCarrier(startSide);
        CountCarrier(farSide);
        history.RecordSettler(slot, EventType.LayoutWaitingOnRoad, Cause.Layout, history.FlagId(waitFlag).ToString());
        history.RecordFlag(start, EventType.LayoutWaitingOnRoad, Cause.Layout, history.FlagId(far).ToString());
        history.RecordFlag(far, EventType.LayoutWaitingOnRoad, Cause.Layout, history.FlagId(start).ToString());

        return null;
    }

    /// <summary>
    /// State 2 at the flag a carrier was sent to (0x4398ea): the carrier takes the road in the direction of its mode. Both
    /// ends of the road lose bit 7 and count it as a carrier, and the carrier takes state 3 with the direction as its
    /// heading and goes on at once. Where the road is gone the carrier stays as it is.
    /// </summary>
    public void EnterRoad(int slot, Settler settler)
    {
        var (flag, direction) = settler.Activity.GoingTo is ToRoad errand ? (errand.At, errand.Direction) : default;
        if (roads.RoadAt(flag, direction) is not { } road)
        {
            return;
        }

        var (far, farDirection) = RoadNetwork.FarEnd(road, flag, direction);
        CountCarrier(flags[far].Side(farDirection));
        CountCarrier(flags[flag].Side(direction));
        movement.Become(settler, new WalksRoad(null, Heading.Facing(direction)));
        var id = history.SettlerId(slot);
        history.RecordSettler(slot, EventType.EnterRoad, Cause.Carriers, history.FlagId(flag).ToString());
        history.RecordFlag(flag, EventType.EnterRoad, Cause.Carriers, id.ToString());
        history.RecordFlag(far, EventType.EnterRoad, Cause.Carriers, id.ToString());
        WalkRoad(slot, settler);
    }

    // A carrier has entered a road: no carrier is on the way to it any more and it holds one more.
    private static void CountCarrier(FlagSide side)
    {
        side.CarrierOnTheWay = false;
        side.Carriers++;
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
                direction = SettlerMovement.RoadContinuation(Map.At(here).Roads, walks.Heading.Direction);
                if (direction < 0)
                {
                    settler.Timer = 0;
                    history.RecordSettler(slot, EventType.WalkRoad, Cause.Carriers);
                    return;
                }
            }
            else
            {
                // A carrier on a flag: with a good for the building at this flag it goes in, else it swaps goods with the flag and
                // goes on along the road it came by.
                if (walks.Load is { Destination: { } destination } && destination == Map.At(here).LinkedSlot)
                {
                    Deliver(slot, settler, walks);
                    return;
                }

                goods.Exchange(slot, settler, Map.At(here).LinkedSlot);
                walks = (WalksRoad)settler.Activity;
                direction = walks.Heading.Direction;
            }

            var (_, _, next) = movement.Neighbour(settler, direction);
            if (
                Map.At(next).FlagMark
                && walks is { Load: null, Marked: false }
                && flags[Map.At(next).LinkedSlot].Side((direction + 3) % 6).WaitingPlace is null
            )
            {
                Wait(slot, settler, walks, (direction + 3) % 6, Map.At(next).LinkedSlot);
                return;
            }

            if (!step(slot, settler, direction))
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
    // the target.
    // **Inferred:** the fourth turn at a road that holds two carriers is read from the code; no emulator or debugger run reaches
    // it. Open lead in docs/milestones/m3-first-loop.md (the fourth turn of a carrier at the end of a road that holds two).
    private void Wait(int slot, Settler settler, WalksRoad walks, int back, int flag)
    {
        settler.Timer = 0x3f;
        settler.Frame = walks.Heading.IdleFrame;
        var approach = walks.Heading.Retried;
        settler.Activity = walks with { Heading = approach };
        var far = roads.FlagNeighbour(flag, back);
        var side = flags[flag].Side(back);
        var farDirection = side.FarDirection;
        if (side.Carriers >= 2)
        {
            var waits = (walks.Waits + 1) & 0xff;
            settler.Activity = walks with { Heading = approach, Waits = waits };
            if (waits >= 4)
            {
                side.Carriers--;
                flags[far].Side(farDirection).Carriers--;
                settler.Activity = walks with { Heading = approach, Waits = 0, Marked = true };
                var id = history.SettlerId(slot);
                history.RecordFlag(flag, EventType.Wait, Cause.Carriers, id.ToString());
                history.RecordFlag(far, EventType.Wait, Cause.Carriers, id.ToString());
            }

            history.RecordSettler(slot, EventType.Wait, Cause.Carriers);

            return;
        }

        if (flags[far].Side(farDirection).WaitingPlace is not null)
        {
            history.RecordSettler(slot, EventType.Wait, Cause.Carriers);

            return;
        }

        // The approach moves into the low byte of the stamp, which the encoder writes from the activity.
        settler.Stamp = (ushort)(settler.Stamp & 0xff00);
        settler.Activity = new WaitsAtFlag(flag, back, approach);
        movement.Release(Map.Index(settler.X, settler.Y));
        history.RecordSettler(slot, EventType.Wait, Cause.Carriers);
    }

    // State 0x42 (0x42ff81): a carrier who waits next to a flag looks for a good that waits to go along its road, at the flag it
    // stands next to (it goes on in the heading it came) and at the flag at the road's far end (it turns round). With one it
    // takes state 0x43 and runs it. **Not ported:** what the carrier does in state 3 and after it with a good to fetch (the
    // next stage); it walks on as it does with none.
    private void Fetch(int slot, Settler settler)
    {
        var waiting = (WaitsAtFlag)settler.Activity;
        var side = flags[waiting.Flag].Side(waiting.Direction);
        Heading heading;
        if (side.WaitingPlace is not null)
        {
            heading = waiting.Approach.Unmarked;
        }
        else
        {
            if (
                flags[roads.FlagNeighbour(waiting.Flag, waiting.Direction)].Side(side.FarDirection).WaitingPlace is null
            )
            {
                return;
            }

            // The approach is the heading less 6 (Wait): from -6 (heading 0) to -1 (heading 5). The turned heading is the
            // opposite direction: approach + 6 - 3, and 6 more where that went below 0 (checked against the emulator's fetches
            // in road-splits.txt, where the carrier came by 0 and by 3).
            heading = waiting.Approach.Turned;
        }

        movement.Become(settler, new ClaimsNode(heading, waiting.Approach));
        history.RecordSettler(slot, EventType.Fetch, Cause.Goods);
        Claim(slot, settler);
    }

    /// <summary>
    /// State 0x43 (0x43001a), which state 0x42 runs on at once (and the rejoin of a carrier whose road was joined): the carrier
    /// takes the node it stands on where no settler stands on it, and goes on in state 3 with its timer run out; else it tries
    /// again at its next turn.
    /// </summary>
    public void Claim(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        if (movement.Occupant(here) != 0)
        {
            return;
        }

        movement.Occupy(here, slot);
        movement.Become(settler, new WalksRoad(null, ((ClaimsNode)settler.Activity).Heading));
        settler.Stamp = movement.Clock;
        settler.Timer = 0;
        history.RecordSettler(slot, EventType.Claim, Cause.Goods);
    }

    // A carrier on the flag it carries a good to (0x43c45e): it turns to the building north-west of the flag, state 0xe, with the
    // step's frame from the heights and its time on the timer, and goes on at once where the timer has not run out.
    private void Deliver(int slot, Settler settler, WalksRoad walks)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (_, _, node) = movement.Neighbour(settler, 4);
        movement.Become(settler, new HandsOver(walks.Load, walks.Heading));
        settler.Frame = (byte)(movement.Height(node) - movement.Height(here) + 0x5d);
        var old = settler.Timer;
        settler.Timer = (ushort)(old + movement.Tables.StepTime(settler.Frame));
        history.RecordSettler(slot, EventType.Deliver, Cause.Goods);
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
                movement.Become(settler, new WalksRoad(hands.Load, hands.Heading));
                history.RecordSettler(slot, EventType.Handover, Cause.Goods);
                WalkRoad(slot, settler);

                return;
            }

            var (_, _, node) = movement.Neighbour(settler, 4);
            var good = hands.Load?.Good.Code ?? 0;
            var given = "";
            if (good != 0)
            {
                // The carrier's hands are empty; its item keeps the destination of the good (the residue).
                movement.Become(settler, hands with { Load = null });
                if (movement.BuildingAt(node) is { IsDemolishing: false } building)
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
                            addToStock(good);
                        }
                    }

                    var buildingSlot = buildings.SlotOf(building);
                    history.RecordBuilding(
                        buildingSlot,
                        EventType.Handover,
                        Cause.Goods,
                        $"{history.SettlerId(slot)} good {good}"
                    );
                    given = $"{history.BuildingId(buildingSlot)} good {good}";
                }
            }

            var shape = 0x0d - (settler.Frame - 0x5d);
            settler.Frame = (byte)shape;
            settler.Activity = ((HandsOver)settler.Activity) with { Marked = true };
            var old = settler.Timer;
            settler.Timer = (ushort)(old + (movement.Tables.StepTime(shape) >> 1));
            history.RecordSettler(slot, EventType.Handover, Cause.Goods, given.Length == 0 ? null : given);
            if (settler.Timer < old)
            {
                return;
            }
        }
    }
}
