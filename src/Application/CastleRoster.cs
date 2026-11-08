using System;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The castle's settlers: the 20 first settlers and their turns while they wait in the castle, stand by while it is built, and
/// leave it by its door, the list of waiting settlers a request takes its worker from, and the building turn that asks the
/// castle for a worker (docs/rules/castle.md, docs/rules/settlers.md).
/// </summary>
public sealed class CastleRoster(
    SettlerMovement movement,
    SettlerTable settlers,
    BuildingTable buildings,
    FlagTable flags,
    RoadNetwork roads,
    GameHistory history,
    Causes causes,
    IRandomDraws draws,
    GameClock clock
) : ICastleRoster
{
    // The jobs of the castle's 20 first settlers in the order of their slots (docs/rules/castle.md); a job is the
    // byte 0 of the original's item shifted right by 2.
    private static readonly Job[] CastleJobs =
    [
        .. new[] { 4, 21, 21, 21, 21, 21, 22, 22, 22, 18, 5, 6, 7, 2, 3, 11, 20, 20, 9, 9 }.Select(code => new Job(
            code
        )),
    ];

    // The job whose waiting settler's turn in the castle takes another branch (not ported): the original's job 10.
    private static readonly Job UnportedWaitingJob = new(10);

    // The number of jobs the castle's list of waiting settlers has an entry for (0x42 + 2 times the job, up to 0x82).
    private const int JobCount = 32;

    // The castle's status bit 4, the only status bit it has besides the bit of its worker inside, with no recovered meaning.
    private const int CastleOtherStatus = 0x10;

    // A fill counter that has overflowed: both fill bytes of the castle read full, as a delivery that carries out of the
    // counter leaves them (docs/rules/goods.md).
    private const int FullFill = 0xff;

    // The frame of a settler that waits for a busy node, as the claims of the walk home and of the woodcutter set it.
    private const int WaitingFrame = 0x52;

    // The clock units a knight's timer is set to after a promotion and grows by for each draw that does not promote it.
    private const ushort KnightTimerStep = 6000;

    // The timer value at and above which adding KnightTimerStep wraps past 16 bits (0x10000 - 6000), which ends the draws.
    private const int KnightTimerWraps = 0x10000 - KnightTimerStep;

    // The draws below which a knight of the level 0 to 3 (the jobs 22 to 25) is made a knight of the next level
    // (0x43910b, 0x4391eb, 0x4392cb, 0x4393ab).
    private static readonly int[] PromotionLimits = [4000, 2000, 1000, 500];

    // The castle's list of waiting settlers: for each job the slot of the settler that waits there (word at 0x42 + 2
    // times the job of the storage item), 0 for none.
    private readonly ushort[] _waiting = new ushort[JobCount];

    // The entry of a job in the castle's list of waiting settlers.
    private ref ushort Entry(Job job) => ref _waiting[job.Code];

    /// <summary>The castle's list of waiting settlers, by job: the slot of the settler that waits there, 0 for none.</summary>
    public ReadOnlySpan<ushort> WaitingList => _waiting;

    /// <summary>The settlers a request has sent out that have not left the castle yet (word at 0x4a of the storage item).</summary>
    public int LeavingCount { get; private set; }

    /// <summary>Registers the behaviours of the activities the castle's settlers have.</summary>
    public void Register(ActivityRegistry registry)
    {
        registry.Register<WaitsInCastle>((slot, settler, _) => Idle(slot, settler));
        registry.Register<BuildsCastle>((slot, settler, _) => Builder(slot, settler));
        registry.Register<BuiltCastle>((_, _, _) => { });
        registry.Register<RawState>((_, _, _) => { });
        registry.Register<Leaving>((slot, settler, _) => Leave(slot, settler));
        registry.Register<KnightWalks>((_, _, _) => { });
    }

    /// <summary>The slot of the settler that waits in the castle's list for a job, 0 for none.</summary>
    public int Waiting(Job job) => Entry(job);

    void ICastleRoster.WorkerSent() => LeavingCount++;

    // Empties the castle's list of the settlers that wait for a job (a test that has the castle run out of settlers).
    internal void ClearWaiting(Job job) => Entry(job) = 0;

    /// <summary>
    /// The settler the castle's list of waiting settlers names for a job (the dispatch takes that one, 0x42be63, 0x42b455), else
    /// the first of the castle's settlers of the job that waits in the castle (state 1), or -1.
    /// </summary>
    public int CastleSettler(Job job)
    {
        var listed = Entry(job);
        if (listed != 0 && settlers[listed] is { Activity: WaitsInCastle } waiting && waiting.Job == job)
        {
            return listed;
        }

        for (var slot = 1; slot < settlers.Count; slot++)
        {
            if (settlers[slot].Job == job && settlers[slot].Activity is WaitsInCastle)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>The castle's list of waiting settlers no longer names a settler the dispatch takes (0x42be63, 0x42b455 clear the entry).</summary>
    public void Take(Job job, int slot)
    {
        if (Entry(job) == slot)
        {
            Entry(job) = 0;
        }
    }

    /// <summary>
    /// The castle's building and its 20 first settlers (0x442f0c with 0x44333d): all in the castle, in state 1 but the first,
    /// which stands by while the castle is built. Their time stamp is the clock of the placement.
    /// </summary>
    public void CreateCastleSettlers(int x, int y)
    {
        // The castle's status 0x50: the worker is inside, and the bit 4 has no recovered meaning.
        buildings.Add(
            new Building(BuildingType.Castle, x, y, 0, constructing: true)
            {
                WorkerInside = true,
                OtherStatus = CastleOtherStatus,
                FillLow = FullFill,
                FillHigh = FullFill,
            }
        );
        history.FillBuilding(0);
        foreach (var job in CastleJobs)
        {
            settlers.Add(
                new Settler(job, 0, x, y, clock.Now, job == Job.Builder ? new BuildsCastle() : new WaitsInCastle())
            );
            history.FillSettler(settlers.Count - 1);
        }
    }

    // The handler of a building type (the table at 0x42cd1d). Only the woodcutter asks for a worker; the castle's
    // handler (0x42e7e0, knights and defence) is not ported.
    public void HandleBuilding(Building building)
    {
        if (
            building.Type == BuildingType.Woodcutter
            && !(building.RequestFailed || building.WorkerInside || building.WorkerOnTheWay)
            && !RequestWorker(building, Job.Woodcutter)
        )
        {
            building.RequestFailed = true;
            causes.RecordBuilding(building, EventType.BuildingHandled);
        }
    }

    // 0x42be63: the building asks the castle for a settler of a job. The search runs over the roads from the building's
    // flag; the castle's flag is the only storage, so it finds the settler where the flag is reached and the castle
    // holds a waiting settler of the job. **Not ported:** the second way, a new settler made of one that has no job
    // and a tool from the stock; the castle's 20 first settlers hold one woodcutter.
    private bool RequestWorker(Building building, Job job)
    {
        if (roads.Route(flags.Count, building.Flag, 0) < 0 || Entry(job) == 0)
        {
            return false;
        }

        var slot = Entry(job);
        var settler = settlers[slot];
        Entry(job) = 0;
        movement.Become(settler, new Leaving(new ToBuilding(building.Flag)));
        building.WorkerOnTheWay = true;
        LeavingCount++;
        var buildingSlot = buildings.SlotOf(building);
        var buildingId = history.BuildingId(buildingSlot);
        history.EndCarry(slot);
        if (causes.TryPlacementOf(buildingSlot, out var command))
        {
            history.CarryCause(slot, command);
        }

        history.RecordSettler(slot, EventType.WorkerRequested, causes.OfBuilding(buildingSlot), buildingId.ToString());
        history.RecordBuilding(
            buildingSlot,
            EventType.WorkerRequested,
            causes.OfBuilding(buildingSlot),
            history.SettlerId(slot).ToString()
        );

        return true;
    }

    // State 1, a settler waiting in the castle (0x438eeb): it enters its slot in the castle's list of waiting settlers
    // each turn. The job 10 takes another branch (not ported); the knights, the jobs 22 to 26, also draw
    // (docs/rules/woodcutting.md, "The random generator").
    private void Idle(int slot, Settler settler)
    {
        if (settler.Job.IsKnightOrAbove)
        {
            KnightIdle(slot, settler);
        }
        else if (settler.Job != UnportedWaitingJob)
        {
            Entry(settler.Job) = (ushort)slot;
        }
    }

    // A knight waiting in the castle (0x43910b and its three siblings). When its timer has run out it draws until a draw is
    // below its level's limit, which promotes it (job plus one, timer 6000, and its entry in the castle's list moves to
    // the new job), adding 6000 to the timer for each draw above it until that wraps. The last level only waits.
    private void KnightIdle(int slot, Settler settler)
    {
        var level = settler.Job.KnightLevel;
        if (level < PromotionLimits.Length)
        {
            var delta = (ushort)(clock.Now - settler.Stamp);
            settler.Stamp = clock.Now;
            var old = settler.Timer;
            settler.Timer = (ushort)(old - delta);
            if (delta > old)
            {
                while (true)
                {
                    if (draws.Draw() < PromotionLimits[level])
                    {
                        Entry(settler.Job) = 0;
                        settler.Job = settler.Job.Promoted;
                        settler.Timer = KnightTimerStep;
                        break;
                    }

                    var timer = settler.Timer;
                    settler.Timer = (ushort)(timer + KnightTimerStep);
                    if (timer >= KnightTimerWraps)
                    {
                        break;
                    }
                }
            }

            history.RecordSettler(slot, EventType.KnightIdled, causes.Rule);
        }

        Entry(settler.Job) = (ushort)slot;
    }

    // State 10, the settler that stands by while the castle is built (0x43f17a): each turn adds the elapsed units
    // times 128 to the castle's building progress, and when it wraps past 65536 the castle stands. The settler then
    // takes state 12, which is not ported.
    private void Builder(int slot, Settler settler)
    {
        var delta = (ushort)(clock.Now - settler.Stamp);
        settler.Stamp = clock.Now;
        var castle = buildings[0];
        var old = castle.Progress;
        castle.Progress = (ushort)(old + (ushort)(delta << 7));
        if (castle.Progress < old)
        {
            movement.Become(settler, new BuiltCastle());
            movement.Release(movement.Map.Index(settler.X, settler.Y));
            castle.Constructing = false;
            castle.Worker = 0;
        }

        history.RecordSettler(slot, EventType.Builder, causes.Rule, history.BuildingId(0).ToString());
        history.RecordBuilding(0, EventType.Builder, causes.OfBuilding(0), history.SettlerId(slot).ToString());
    }

    // State 15, a settler that leaves the castle by its door onto the castle's flag (0x43dd4a): it steps south-east
    // when that node and the node of the building it walks to are free, takes the time of the step times the door's
    // speed, and waits it out in state 5. A busy node keeps it in state 15.
    private void Leave(int slot, Settler settler)
    {
        var here = movement.Map.Index(settler.X, settler.Y);
        var (x, y, next) = movement.Neighbour(settler, 1);
        var leaving = (Leaving)settler.Activity;
        var target = leaving.Errand is ToBuilding building
            ? buildings.FirstOrDefault(b => b.Flag == building.At)
            : null;
        if (
            movement.Occupant(here) != 0
            || movement.Occupant(next) != 0
            || (target is not null && movement.Occupant(movement.Map.Index(target.X, target.Y)) != 0)
        )
        {
            settler.Frame = WaitingFrame;
            settler.Timer = 0;
            history.RecordSettler(slot, EventType.Left, causes.Rule);
            return;
        }

        LeavingCount--;
        var door = movement.BuildingAt(here);
        movement.Move(slot, settler, x, y, next);
        var shape = movement.Height(next) - movement.Height(here) + 13;
        settler.Frame = (byte)shape;
        var speed = (byte)(movement.Tables.DoorSpeed(door) ^ 0x1f);
        settler.Timer = (ushort)((ushort)(movement.Tables.StepTime(shape) * speed) >> 5);
        settler.Stamp = clock.Now;
        settler.Activity = new WaitsOutStep(new Walks(leaving.Errand, new Heading(0)));
        history.RecordSettler(slot, EventType.Left, causes.Rule);
    }
}
