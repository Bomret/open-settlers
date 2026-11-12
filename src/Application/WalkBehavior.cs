using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The walk of a settler to its errand over the roads, the wait that follows each step, and the entry into the building at the
/// end of the walk (docs/rules/settlers.md). Handles <see cref="Walks"/> and <see cref="WaitsOutStep"/>.
/// </summary>
public sealed class WalkBehavior(
    SettlerMovement movement,
    BuildingTable buildings,
    FlagTable flags,
    RoadNetwork roads,
    GameHistory history,
    Causes causes,
    GameClock clock,
    WalkHomeBehavior walkHome,
    CarrierBehavior carriers,
    RoadStep roadStep,
    CastleRoster roster,
    SettlerTable settlers,
    IReadOnlyList<JobRules> modules
)
{
    /// <summary>Registers the behaviours of the activities that walk to an errand.</summary>
    public void Register(ActivityRegistry registry)
    {
        registry.Register<WaitsOutStep>((slot, settler, _) => WaitOutStep(movement, history, causes, slot, settler));
        registry.Register<Walks>((slot, settler, _) => Walk(slot, settler));
    }

    // State 5 (0x42fc81 dispatches on the state byte through a table of 8-byte entries): when the step's time has run out the
    // settler takes up the activity it waited for.
    // Static so a behaviour's test bench registers this very handler for the wait it meets.
    internal static void WaitOutStep(
        SettlerMovement movement,
        GameHistory history,
        Causes causes,
        int slot,
        Settler settler
    )
    {
        if (movement.TimerRan(settler))
        {
            settler.Timer = 0;
            settler.Activity = ((WaitsOutStep)settler.Activity).Then;
        }

        history.RecordSettler(slot, EventType.Visited, causes.Rule);
    }

    // State 2, a settler on the road (0x4398ea with the step routines at 0x43a64c to 0x43ac8b). When its timer has run
    // out it chooses a direction (on a node of a road the one the road goes on, on a flag the first direction of the
    // route to the flag it walks to), steps, and adds the step's time to its timer. The sum wraps while the step
    // was over already, and the settler then goes on at once; else it waits out the rest. At the flag it walks to
    // it enters the building there, or, as a carrier, the road. A step onto a node another settler stands on is tried
    // again after the timer of 0x7f. The mode 0xfe is the walk home to a storage: the storage is picked at the first flag
    // (StorageFlag), and at its flag the settler enters it as a worker enters its hut. A step east, south-east, or south onto
    // a settler that comes the other way and is blocked passes it (RoadStep). **Not ported:** the states the original reaches
    // from here for other modes.
    private void Walk(int slot, Settler settler)
    {
        // `walked` records the turn of state 2; a step or entry inside it records its own change first (`stepped`, `entered`).
        WalkTurn(slot, settler);
        history.RecordSettler(slot, EventType.Walked, causes.OfSettler(slot));
    }

    private void WalkTurn(int slot, Settler settler)
    {
        var map = movement.Map;
        if (!movement.TimerRan(settler))
        {
            return;
        }

        while (true)
        {
            var walks = (Walks)settler.Activity;
            var here = map.Index(settler.X, settler.Y);
            int direction;
            if (walks.Heading.IsRetried)
            {
                // A step that was blocked: the heading holds the direction less 6, tried again. Each try counts in byte 15;
                // at 10 tries on a flag and 50 elsewhere the original follows the chain of settlers that wait for one another
                // (0x439985) and counts from 0 again. **Not ported:** that the chain, where it comes back to the settler,
                // makes the settler step against it and pass the settler there (as for the swap of Step); every other chain
                // ends with the count at 0 and the same direction tried again.
                var tries = walks.Tries + 1;
                settler.Activity = walks with { Tries = tries >= (map.At(here).FlagMark ? 10 : 50) ? 0 : tries };
                direction = walks.Heading.Direction;
            }
            else if (!map.At(here).FlagMark)
            {
                // A node of a road: the road's other direction than the one the settler came by.
                direction = SettlerMovement.RoadContinuation(map.At(here).Roads, walks.Heading.Direction);
                if (direction < 0)
                {
                    settler.Timer = 0;
                    return;
                }
            }
            else
            {
                var errand = walks.Errand;
                if (errand is ToStorage && walkHome.StorageFlag(map.At(here).LinkedSlot) < 0)
                {
                    // No storage chosen yet (0x45d09b); none a road leads to sends the settler to look for a flag again,
                    // from the far end of the search.
                    movement.Become(settler, new SearchesHome(true));
                    settler.Timer = 0;
                    return;
                }

                if (errand.Flag == map.At(here).LinkedSlot)
                {
                    // An errand to a building or a storage enters the building north-west of the flag; one to a road enters
                    // the road.
                    if (errand is ToRoad)
                    {
                        carriers.EnterRoad(slot, settler);
                    }
                    else
                    {
                        Enter(slot, settler);
                    }

                    return;
                }

                direction = roads.Route(flags.Count, map.At(here).LinkedSlot, errand.Flag);
                if (direction < 0 && errand is ToStorage)
                {
                    // 0x43a24e: a settler on its way to a storage that finds no road leads on looks for a flag again.
                    movement.Become(settler, new SearchesHome(true));
                    settler.Timer = 0;
                    return;
                }

                if (direction < 0)
                {
                    // No road leads on. The worker is not coming any more; it turns to go home (the walk home).
                    var hut = buildings.FirstOrDefault(b => b.Flag == errand.Flag);
                    if (hut is not null)
                    {
                        hut.WorkerOnTheWay = false;
                        causes.RecordBuilding(hut, EventType.Walked, history.SettlerId(slot).ToString());
                        causes.ForgetPlacement(history.BuildingId(buildings.SlotOf(hut)));
                    }

                    settler.Activity = walks with { Errand = new ToStorage() };
                    settler.Timer = 0;
                    return;
                }
            }

            if (!roadStep.Step(slot, settler, direction))
            {
                return;
            }
        }
    }

    /// <summary>
    /// A settler at the flag it walks to goes on to the building north-west of the flag (0x4398ea): the building takes
    /// its worker (status bit 6, and bit 7, the worker on the way, ends), the settler steps onto the building's node,
    /// and takes state 4, the step through the door, with the time of the step and the part of it that the door's speed
    /// leaves. A busy node keeps the settler on the flag in state 6.
    /// </summary>
    public void Enter(int slot, Settler settler)
    {
        var tables = movement.Tables;
        var here = movement.Map.Index(settler.X, settler.Y);
        var (x, y, node) = movement.Neighbour(settler, 4);
        var building = movement.BuildingAt(node);
        if (building is null)
        {
            return;
        }

        building.WorkerInside = true;
        var coming = building.WorkerOnTheWay;
        building.WorkerOnTheWay = false;
        if (coming)
        {
            building.Worker = slot;
        }

        var buildingId = history.BuildingId(buildings.SlotOf(building));
        causes.RecordBuilding(building, EventType.Entered, history.SettlerId(slot).ToString());
        var storage = settler.Activity.GoingTo is ToStorage;
        movement.Become(settler, new EntersBuilding(storage));
        if (movement.Occupant(node) != 0)
        {
            settler.Frame = 0x55;
            settler.Timer = 0;
            history.RecordSettler(slot, EventType.Entered, causes.OfSettler(slot), buildingId.ToString());
            return;
        }

        movement.Move(slot, settler, x, y, node);
        var shape = 0x28 + movement.Height(node) - movement.Height(here);
        settler.Frame = (byte)shape;
        var time = tables.StepTime(shape);
        settler.Timer = time;
        settler.Stamp = clock.Now;
        movement.Become(settler, new Door((ushort)(time * tables.DoorSpeed(building)) >> 5, storage));
        history.RecordSettler(slot, EventType.Entered, causes.OfSettler(slot), buildingId.ToString());
        causes.ForgetPlacement(buildingId);
    }

    /// <summary>
    /// The worker a finished building of a job whose settler stands in the hut (the woodcutter's, stonecutter's, or forester's building) stands
    /// with (docs/rules/layouts.md, ADR 0009, INV-0005), as the
    /// mission start (<c>0x41f2d2</c>) creates a building's settlers inside it: the castle's woodcutter (or stonecutter, or a
    /// settler without a job that becomes the forester, the castle holding none) has
    /// walked from the castle to the hut and stepped through its door (state 4, <c>0x4398ea</c> with the building's status bit
    /// 6 set and bit 7 cleared, then the door's step: timer, frame, target, and stamp). The request that sent him
    /// (<c>0x42be63</c>) is made as the original makes it: the castle's list of waiting woodcutters loses him, the hut is
    /// marked as served so it asks for no second worker. The castle's counter of settlers sent out rises with the request and
    /// falls again where he leaves the castle (<c>0x43dd4a</c>), so it is the same as before. Refused for a node that is no
    /// finished woodcutter's, stonecutter's, or forester's building, a hut that has its worker or has one on the way, a hut whose door node
    /// is taken, and a castle that holds no worker of the job. Returns why it refused, or null.
    /// </summary>
    public string? StandWorkerInHut(int x, int y)
    {
        var node = movement.Map.Index(x, y);
        var hut = movement.BuildingAt(node);
        if (hut is not { IsDemolishing: false, Site: null } || !StandsWorker(hut.Type))
        {
            return NoHutRefusal(modules);
        }

        if (hut.WorkerInside || hut.WorkerOnTheWay)
        {
            return "the hut has its worker already";
        }

        var request = movement.Tables.WorkerOf(hut.Type)!;
        var asked = request.Job;
        var slot = roster.CastleSettler(asked);
        // The castle holds none of the job: the request's second way (0x42be63) makes one of a settler without a job where the
        // request names no tool (the forester's, docs/rules/forestry.md). A job whose request names a tool is not made in a layout.
        var made = slot < 0 && request.Tool.IsNone;
        if (made)
        {
            slot = roster.CastleSettler(new Jobless());
        }

        if (slot < 0)
        {
            return $"the castle holds no {NameOf(modules.First(rules => rules.JobType == asked.GetType()))} for the building";
        }

        var flag = flags[hut.Flag];
        if (movement.Occupant(node) != 0 || movement.Occupant(movement.Map.Index(flag.X, flag.Y)) != 0)
        {
            return "a settler stands on the hut's door or on its flag";
        }

        var settler = settlers[slot];
        roster.Take(made ? new Jobless() : asked, slot);
        if (made)
        {
            settler.TakeUp(asked);
        }

        hut.WorkerOnTheWay = true;
        movement.Become(settler, new Leaving(new ToBuilding(hut.Flag)));
        settler.X = flag.X;
        settler.Y = flag.Y;
        Enter(slot, settler);
        var hutSlot = buildings.SlotOf(hut);
        history.RecordSettler(slot, EventType.LayoutInHut, Cause.Layout, history.BuildingId(hutSlot).ToString());
        history.RecordBuilding(hutSlot, EventType.LayoutInHut, Cause.Layout, history.SettlerId(slot).ToString());

        return null;
    }

    /// <summary>
    /// Whether a layout stands a worker inside a finished building of the type: the building's job has a module whose rules let its
    /// settler stand in the hut (<see cref="JobRules.CanStandInHut"/>).
    /// </summary>
    public bool StandsWorker(BuildingType type) =>
        movement.Tables.WorkerOf(type) is { } request
        && modules.Any(rules => rules.JobType == request.Job.GetType() && rules.CanStandInHut);

    // What a sentence calls a settler of the module's job: the job type's name in lower case.
    private static string NameOf(JobRules rules) => rules.JobType.Name.ToLowerInvariant();

    // The refusal for a node with no building that stands a worker. It names the jobs whose modules let their settler stand in the hut:
    // "no finished woodcutter's, stonecutter's, or forester's building stands on the node" for three, "a's or b's" for two, "a's" for
    // one, and no job for none. The names are in descending ordinal order, which gives today's order of the three.
    internal static string NoHutRefusal(IEnumerable<JobRules> modules)
    {
        var names = modules
            .Where(rules => rules.CanStandInHut)
            .Select(NameOf)
            .OrderByDescending(name => name, System.StringComparer.Ordinal)
            .Select(name => name + "'s")
            .ToList();
        var jobs = names.Count switch
        {
            0 => "",
            1 => names[0] + " ",
            2 => names[0] + " or " + names[1] + " ",
            _ => string.Join(", ", names.Take(names.Count - 1)) + ", or " + names[^1] + " ",
        };

        return $"no finished {jobs}building stands on the node";
    }
}
