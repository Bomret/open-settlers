using System.Linq;

namespace OpenSettlers.Simulation;

// The entry states a layout starts settlers and goods in (docs/rules/layouts.md, ADR 0009, INV-0005). Each places a settler
// of the castle, or a good, in the state the original's own routine leaves it in, and returns why it refused or null. The
// carrier's and the good's are the behaviours' (CarrierBehavior.PlaceCarrierOnRoad, GoodsTransport.PlaceGood).
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

    // The castle's list of waiting settlers no longer names a settler the dispatch takes (0x42be63, 0x42b455 clear the entry).
    private void TakeFromWaiting(Job job, int slot)
    {
        if (_waiting[job.Code] == slot)
        {
            _waiting[job.Code] = 0;
        }
    }

    int ICastleRoster.Waiting(Job job) => _waiting[job.Code];

    int ICastleRoster.CastleSettler(Job job) => CastleSettler(job);

    void ICastleRoster.Take(Job job, int slot) => TakeFromWaiting(job, slot);

    void ICastleRoster.WorkerSent() => _leaving++;

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
}
