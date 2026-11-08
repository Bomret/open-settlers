using System.Linq;

namespace OpenSettlers.Simulation;

// The entry states a layout starts settlers and goods in (docs/rules/layouts.md, ADR 0009, INV-0005). Each places a settler
// of the castle, or a good, in the state the original's own routine leaves it in, and returns why it refused or null.
public sealed partial class Game
{
    // The settler the castle's list of waiting settlers names for a job (the dispatch takes that one, 0x42be63, 0x42b455), else
    // the first of the castle's settlers of the job that waits in the castle (state 1), or -1.
    private int CastleSettler(Job job)
    {
        var listed = _waiting[job.Code];
        if (listed != 0 && _settlers[listed] is { Activity: WaitsInCastle } waiting && waiting.Job == job)
        {
            return listed;
        }

        for (var slot = 1; slot < _settlers.Count; slot++)
        {
            if (_settlers[slot].Job == job && _settlers[slot].Activity is WaitsInCastle)
            {
                return slot;
            }
        }

        return -1;
    }

    private int FlagAt(int x, int y) => _flags.SlotAt(x, y);

    // The castle's list of waiting settlers no longer names a settler the dispatch takes (0x42be63, 0x42b455 clear the entry).
    private void TakeFromWaiting(Job job, int slot)
    {
        if (_waiting[job.Code] == slot)
        {
            _waiting[job.Code] = 0;
        }
    }

    /// <summary>
    /// Entry state <c>in-hut</c> of the woodcutter: the castle's woodcutter has walked from the castle to the hut and stepped
    /// through its door (state 4, <c>0x4398ea</c> with the building's status bit 6 set and bit 7 cleared, then the door's step:
    /// timer, frame, target, and stamp). The request that sent him (<c>0x42be63</c>) is made as the original makes it: the
    /// castle's list of waiting woodcutters loses him, the hut is marked as served so it asks for no second worker. The
    /// castle's counter of settlers sent out rises with the request and falls again where he leaves the castle (<c>0x43dd4a</c>),
    /// so it is the same as before. Refused for a node that is no woodcutter's hut, a hut that has its worker or has one on the
    /// way, a hut whose door node is taken, and a castle that holds no woodcutter.
    /// </summary>
    internal string? PlaceWoodcutterInHut(int x, int y)
    {
        var node = Map.Index(x, y);
        var hut = BuildingAt(node);
        if (hut is not { IsDemolishing: false } || hut.Type != BuildingType.Woodcutter)
        {
            return "no woodcutter's hut stands on the node";
        }

        if (hut.WorkerInside || hut.WorkerOnTheWay)
        {
            return "the hut has its worker already";
        }

        var slot = CastleSettler(Job.Woodcutter);
        if (slot < 0)
        {
            return "the castle holds no woodcutter";
        }

        var flag = _flags[hut.Flag];
        if (Occupant(node) != 0 || Occupant(Map.Index(flag.X, flag.Y)) != 0)
        {
            return "a settler stands on the hut's door or on its flag";
        }

        var settler = _settlers[slot];
        TakeFromWaiting(Job.Woodcutter, slot);
        hut.WorkerOnTheWay = true;
        Become(settler, new Leaving(new ToBuilding(hut.Flag)));
        settler.X = flag.X;
        settler.Y = flag.Y;
        Enter(slot, settler);
        var hutSlot = _buildings.SlotOf(hut);
        History.RecordSettler(slot, EventType.LayoutInHut, Cause.Layout, History.BuildingId(hutSlot).ToString());
        History.RecordBuilding(hutSlot, EventType.LayoutInHut, Cause.Layout, History.SettlerId(slot).ToString());

        return null;
    }

    /// <summary>
    /// Entry state <c>waiting-on-road</c> of the carrier: the settler the castle sends for the road (<c>0x42b455</c>: the
    /// waiting settler of job 0, else a waiting settler of job 21 made a carrier) has walked its road and waits on the
    /// node next to the flag at the road's far end, the end the castle's flag is not reached from first (<c>0x43bc52</c>:
    /// state <c>0x42</c>, timer <c>0x3f</c>, frame <c>0x6e</c> plus the direction, the node's settler word cleared). Both ends
    /// of the road count the carrier (<c>0x4398ea</c>). Refused for a flag or direction with no land road, a road that has
    /// a carrier or one on the way, a road no land road joins to the castle, and a castle that holds no such settler.
    /// </summary>
    internal string? PlaceCarrierOnRoad(int x, int y, int direction)
    {
        var start = FlagAt(x, y);
        if (start < 0 || RoadAt(start, direction) is not { Water: false } road)
        {
            return "no land road leaves a flag there in that direction";
        }

        var (far, farDirection) = FarEnd(road, start, direction);
        var startSide = _flags[start].Side(direction);
        var farSide = _flags[far].Side(farDirection);
        if (startSide.CarrierOnTheWay || startSide.Carriers != 0 || farSide.CarrierOnTheWay || farSide.Carriers != 0)
        {
            return "the road has its carrier already";
        }

        var end = StorageEnd(start, far);
        if (end < 0)
        {
            return "no land road leads from the road to the castle";
        }

        var job = CastleSettler(Job.Carrier) >= 0 ? Job.Carrier : Job.Jobless;
        var slot = CastleSettler(job);
        if (slot < 0)
        {
            return "the castle holds no carrier";
        }

        // The carrier enters the road at the end the castle is reached from and waits at the other.
        var (waitFlag, waitDirection) = end == 0 ? (far, farDirection) : (start, direction);
        var (dx, dy) = RoadBuilding.Steps[waitDirection];
        var settler = _settlers[slot];
        TakeFromWaiting(job, slot);
        settler.Job = Job.Carrier;
        settler.X = (_flags[waitFlag].X + dx) & (Map.Size.Width - 1);
        settler.Y = (_flags[waitFlag].Y + dy) & (Map.Size.Height - 1);
        settler.Frame = (byte)(waitDirection + 0x6e);
        settler.Timer = 0x3f;
        // The stamp's low byte holds the approach, which the activity keeps.
        settler.Stamp = (ushort)(_clock & 0xff00);
        settler.Activity = new WaitsAtFlag(waitFlag, waitDirection, Heading.Facing(waitDirection).Retried);
        CountCarrier(startSide);
        CountCarrier(farSide);
        History.RecordSettler(slot, EventType.LayoutWaitingOnRoad, Cause.Layout, History.FlagId(waitFlag).ToString());
        History.RecordFlag(start, EventType.LayoutWaitingOnRoad, Cause.Layout, History.FlagId(far).ToString());
        History.RecordFlag(far, EventType.LayoutWaitingOnRoad, Cause.Layout, History.FlagId(start).ToString());

        return null;
    }

    /// <summary>
    /// Puts a good on a place of the flag on the node as the settler's put does (<c>0x436b46</c>, <see cref="LayGood"/>). The
    /// original's put takes the first empty place and checks nothing: this refuses a node that holds no flag and a place
    /// that holds a good. The place is 0 to 7, as the layout's read checks.
    /// </summary>
    internal string? PlaceGood(int x, int y, int place, int good)
    {
        var flag = FlagAt(x, y);
        if (flag < 0)
        {
            return "no flag stands on the node";
        }

        if (!_flags[flag].Places[place].IsEmpty)
        {
            return "the place holds a good already";
        }

        LayGood(flag, place, good);
        History.RecordFlag(flag, EventType.LayoutGood, Cause.Layout);

        return null;
    }
}
