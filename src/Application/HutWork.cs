using System;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The states the workers who go out from a hut and come back to it share: the woodcutter's, the stonecutter's, the forester's, and the
/// type 1 hut's settler (their rules modules hold what each does). The shared states are the door, the step out onto the hut's flag, the
/// free walk (the walk home's too), the entry into a hut, the search home, and the map's object pass (docs/rules/woodcutting.md). At the
/// door a settler entering a storage goes first, then the start routine of the module of its job runs; at the end of a free walk the
/// module's arrival runs; an event's cause is the layout's or the walk home's before the module's. A settler whose job has no module
/// starts nothing, records as the settlers' rules, and arrives as <see cref="ArriveByDefault"/> does. The behaviour takes the
/// activities of a door, the step onto the flag, the free walk, and the entry into a hut.
/// </summary>
/// <param name="movement">The settlers' ground: the node of each settler, the timer, the change of activity, the tables.</param>
/// <param name="settlers">The settler table.</param>
/// <param name="flags">The flag table.</param>
/// <param name="history">The history the behaviour records its changes in (no rule reads it).</param>
/// <param name="draws">The random generator the object pass draws from.</param>
/// <param name="walkHome">The walk home, where the free walk arrives and the door leads into the storage.</param>
/// <param name="layGood">Lays a good on a place of a flag: the flag slot, the place, and the good.</param>
/// <param name="inLayout">Whether a layout is being built: its rules record their events with the cause layout.</param>
public sealed class HutWork(
    SettlerMovement movement,
    SettlerTable settlers,
    FlagTable flags,
    GameHistory history,
    IRandomDraws draws,
    WalkHomeBehavior walkHome,
    Action<int, int, int> layGood,
    Func<bool> inLayout
)
{
    // The object pass (0x428d10): the clock at its last run, its countdown (a debt that wraps while 20 units are due),
    // and the cursor's column and row.
    ushort _objectStamp;
    ushort _objectTimer;
    int _objectX;
    int _objectY;

    /// <summary>The object pass's state, for the digest: the clock at its last run, its countdown, and the cursor's column and row.</summary>
    public (ushort Stamp, ushort Timer, int X, int Y) ObjectPassState =>
        (_objectStamp, _objectTimer, _objectX, _objectY);

    // The job rules modules by the type of their job: the hooks the shared states call. A job without a module has none.
    readonly System.Collections.Generic.Dictionary<Type, JobRules> _modules = [];

    Map Map => movement.Map;

    /// <summary>
    /// Registers a job rules module: the start routine, the arrival, and the cause the shared states call for a settler of its job.
    /// </summary>
    public void RegisterModule(JobRules rules) => _modules.Add(rules.JobType, rules);

    JobRules? RulesOf(Job job) => _modules.TryGetValue(job.GetType(), out var rules) ? rules : null;

    /// <summary>
    /// The arrival at the end of a free walk of a job whose module overrides none: the generic arrival home where the job arrives home
    /// generically or the walk is the walk home, else nothing. This is <see cref="JobRules.Arrive"/>'s default and the arrival of a job
    /// without a module.
    /// </summary>
    internal void ArriveByDefault(int slot, Settler settler)
    {
        var walk =
            settler.Activity as WalksFree
            ?? throw new InvalidOperationException(
                "an arrival is the end of a free walk, and the settler is not on one"
            );
        if (settler.Job.ArrivesHomeGenerically || walk.Trip.Purpose is Home)
        {
            ArriveHome(slot, settler);
        }
    }

    /// <summary>Registers the behaviour of the shared activities: the door, the entry into a hut, the step onto the flag, and the free walk.</summary>
    public void Register(ActivityRegistry registry)
    {
        registry.Register<Door>((slot, settler, _) => PassDoor(slot, settler));
        registry.Register<EntersBuilding>((slot, settler, _) => EnterHut(slot, settler));
        registry.Register<StepsOntoFlag>((slot, settler, _) => StepOut(slot, settler));
        registry.Register<WalksFree>((slot, settler, _) => FreeWalk(slot, settler));
    }

    // State 4, a settler that stepped through a door (0x43c96b): once its timer is down to the time the door leaves it, it
    // runs the routine of its job when a building stands there that is not marked bit 5; else it is state 0x19.
    void PassDoor(int slot, Settler settler)
    {
        var door = (Door)settler.Activity;
        var delta = (ushort)(movement.Clock - settler.Stamp);
        settler.Stamp = movement.Clock;
        var old = settler.Timer;
        settler.Timer = (ushort)(old - delta);
        if (delta <= old && (short)((ushort)door.TimeLeft - settler.Timer) < 0)
        {
            RecordWork(slot, EventType.Door);

            return;
        }

        var node = Map.Index(settler.X, settler.Y);
        var building = movement.BuildingAt(node);
        if (building is { IsDemolishing: false })
        {
            settler.Timer = (ushort)door.TimeLeft;
            if (door.EnteringStorage)
            {
                // The timer and stamp change is this rule's; EnterStorage records its own.
                RecordWork(slot, EventType.Door);
                walkHome.EnterStorage(slot, node, settler);

                return;
            }

            RulesOf(settler.Job)?.Start(slot, settler, building);
        }
        else
        {
            movement.Become(settler, new SearchesHome());
            settler.Timer = 0;
        }

        RecordWork(slot, EventType.Door);
    }

    /// <summary>
    /// Records the change a rule of the huts' modules made to the settler in the slot (ADR 0011): its state now, the rule's type, and the
    /// rule's area where the settler carries no command cause: the layout while one builds, the walk home for a settler on it, and else
    /// the cause of the settler's job (woodcutting, stonecutting, forestry, or the settlers' rules).
    /// </summary>
    internal void RecordWork(int slot, string type, string? other = null) =>
        history.RecordSettler(
            slot,
            type,
            walkHome.IsWalking(slot) ? Cause.WalkHome
                : inLayout() ? Cause.Layout
                : RulesOf(settlers[slot].Job)?.Cause ?? Cause.Settlers,
            other
        );

    // The generic arrival at the end of a free walk home (0x4385ab); the job rules modules take it through the JobParts.
    internal void ArriveHome(int slot, Settler settler)
    {
        walkHome.ArriveHome(slot, settler);
        RecordWork(slot, EventType.Arrive);
    }

    // State 7, the step from the hut onto its flag (0x43e036): south-east, when the settler's node and that node are free
    // of other settlers; the time of the step is scaled by the door's speed, and the settler waits it out in state 5.
    internal void StepOut(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, next) = movement.Neighbour(settler, 1);
        var standing = movement.Occupant(here);
        if ((standing == slot || standing == 0) && movement.Occupant(next) == 0)
        {
            var door = movement.BuildingAt(here);
            var shape = 0xd + movement.Height(next) - movement.Height(here);
            movement.Move(slot, settler, x, y, next);
            settler.Frame = (byte)shape;
            var speed = (byte)(movement.Tables.DoorSpeed(door) ^ 0x1f);
            settler.Timer = (ushort)((ushort)(movement.Tables.StepTime(shape) * speed) >> 5);
            settler.Stamp = movement.Clock;
            settler.Activity = new WaitsOutStep(((StepsOntoFlag)settler.Activity).Then);
            RecordWork(slot, EventType.StepOut);

            return;
        }

        settler.Frame = 0x52;
        settler.Timer = 0;
        RecordWork(slot, EventType.StepOut);
    }

    // State 0x10, the free walk along a vector (0x436c38). Byte 15 holds the walk's memory: with bit 3 set and the low three
    // bits 0 the vector is used up and the settler has arrived (0x437ddd); the low three bits otherwise name the last
    // step's direction plus one, and bits 4 to 7 a debt of turns. Each step picks a direction (Choose), steps, takes the step's
    // time into the timer, and goes on while that does not wrap it.
    // **Not ported:** the passing of settlers when every direction is taken (the settler waits 0x7f units with the
    // frame 0x52 as it does when a blocked step is tried again), and the checks of a target node marked solid.
    void FreeWalk(int slot, Settler settler)
    {
        var arrived = FreeWalkTurn(slot, settler);
        RecordWork(slot, EventType.FreeWalk);
        if (arrived)
        {
            if (RulesOf(settler.Job) is { } rules)
            {
                rules.Arrive(slot, settler);
            }
            else
            {
                ArriveByDefault(slot, settler);
            }
        }
    }

    // One turn of the free walk; true where the vector is used up and the settler has arrived.
    bool FreeWalkTurn(int slot, Settler settler)
    {
        if (!movement.TimerRan(settler))
        {
            return false;
        }

        RulesOf(settler.Job)?.WalkTurn(settler);
        var walk = (WalksFree)settler.Activity;
        var trip = walk.Trip;
        while (true)
        {
            var memory = trip.Memory;
            if ((memory & 8) != 0 && (memory & 7) == 0)
            {
                return true;
            }

            var direction = Choose(
                settler,
                trip.Remaining,
                ref memory,
                (memory & 8) != 0 ? (memory & 7) + 5 : (memory & 7) - 1
            );
            trip = trip with { Memory = memory };
            if (direction < 0)
            {
                settler.Activity = walk with { Trip = trip };
                settler.Frame = 0x52;
                settler.Timer = 0x7f;

                return false;
            }

            var (x, y, next) = movement.Neighbour(settler, direction);
            trip = trip with { Remaining = trip.Remaining.After(direction) };
            if (trip.Remaining.IsUsedUp)
            {
                trip = trip with { Memory = 8 };
            }

            settler.Activity = walk with { Trip = trip };
            var here = Map.Index(settler.X, settler.Y);
            var shape = 4 + 9 * direction + movement.Height(next) - movement.Height(here);
            movement.Move(slot, settler, x, y, next);
            var old = settler.Timer;
            settler.Timer = (ushort)(old + movement.Tables.StepTime(shape));
            if (settler.Timer < old)
            {
                settler.Frame = (byte)shape;

                return false;
            }
        }
    }

    // A node the walk may step onto: not marked solid (the lake mark) and holding no settler.
    bool Free(int index) => !Map.At(index).LakeMark && movement.Occupant(index) == 0;

    // The direction a row entry names (0 east to 5 north, -1 for an entry that names none: a table with no row) where the
    // step it leads to is free; -1 for such an entry and for a node that is taken.
    int Candidate(Settler settler, int direction) =>
        direction >= 0 && Free(movement.Neighbour(settler, direction).Index) ? direction : -1;

    // The direction of the next step (0 to 5) or -1 where all are taken, and the memory it leaves in byte 15. A settler with a
    // memory (row 0 or more: the low bits less one, or plus five with bit 3) tries the six directions of that row of the
    // table (0x436e96): the candidate k costs 2, 1, 0, -1, -2, or -3 turns of the debt in bits 4 to 7, a debt that would
    // run out takes the step with the memory cleared, one that would overflow starts again from the vector. Else the
    // first direction of the vector's heading class is tried, and then the other five (0x4375ee) with a memory of the
    // step, the parity of the class and the candidate, and half of 6 less the candidate's counter as the debt.
    int Choose(Settler settler, Vector remaining, ref int memory, int memoryRow)
    {
        if (memoryRow >= 0)
        {
            for (var candidate = 0; candidate < 6; candidate++)
            {
                var entry = movement.Tables.WalkDirection(memoryRow, candidate);
                var direction = Candidate(settler, entry);
                if (direction < 0)
                {
                    continue;
                }

                var delta = 2 - candidate;
                if (delta < 0)
                {
                    var sum = memory + (-delta << 4);
                    if (sum > 0xff)
                    {
                        memory = 0;

                        return Fresh(settler, remaining, ref memory);
                    }

                    memory = sum;
                }
                else if (delta > 0)
                {
                    if (memory < delta << 4)
                    {
                        memory = 0;

                        return direction;
                    }

                    memory -= delta << 4;
                }

                memory = (memory & 0xf8) | (direction + 1);

                return direction;
            }

            memory &= 0xf0;

            return -1;
        }

        return Fresh(settler, remaining, ref memory);
    }

    // The choice from the vector's heading class (0x437072).
    int Fresh(Settler settler, Vector remaining, ref int memory)
    {
        var heading = HeadingClass(remaining.East, remaining.South);
        var first = Candidate(settler, movement.Tables.WalkDirection(heading, 0));
        if (first >= 0)
        {
            return first;
        }

        for (var candidate = 1; candidate < 6; candidate++)
        {
            var direction = Candidate(settler, movement.Tables.WalkDirection(heading, candidate));
            if (direction < 0)
            {
                continue;
            }

            var counter = 5 - candidate;
            var parity = ((heading ^ counter) & 1) != 0 ? 8 : 0;
            memory = direction + 1 + parity + ((6 - counter) >> 1 << 4);

            return direction;
        }

        memory &= 0xf0;

        return -1;
    }

    // The class of the vector (east, south) left to walk, which picks the row of directions (0x437072): 12 for a vector
    // that points west and north, 18 for one that points east and south, plus 1 to 5 for how flat it is.
    static int HeadingClass(int east, int south)
    {
        var hint = 0xc;
        if (east < 0)
        {
            var west = (byte)-east;
            if (south < 0)
            {
                var north = (byte)-south;
                if (north < west)
                {
                    hint += 2;
                    if ((byte)(north + north) < west)
                    {
                        hint++;
                    }
                }
                else if (north < (byte)(west + west))
                {
                    hint++;
                }
            }
            else
            {
                hint += 4;
                if (south >= west)
                {
                    hint++;
                }
            }

            return hint;
        }

        hint += 6;
        if (south < 0)
        {
            hint += 4;
            if ((byte)-south >= east)
            {
                hint++;
            }
        }
        else if (south >= east)
        {
            if (south < (byte)(east + east))
            {
                hint++;
            }
        }
        else
        {
            hint += 2;
            if ((byte)(south + south) < east)
            {
                hint++;
            }
        }

        return hint;
    }

    /// <summary>
    /// The settler arrived at the flag on the way back from its work: the vector's east steps are the byte the entry into the hut sets to
    /// 0, and the settler goes into the hut.
    /// </summary>
    internal void ReturnToHut(int slot, Settler settler, Trip trip)
    {
        settler.Activity = new WalksFree(trip with { Remaining = new Vector(0, trip.Remaining.South) });
        EnterHut(slot, settler);
    }

    /// <summary>
    /// The settler lays a good on the first empty place of the flag it stands on (0x436b46). The flag changes and records with
    /// <paramref name="cause"/>; the settler does not.
    /// </summary>
    internal void PutGood(int slot, Settler settler, int good, Cause cause)
    {
        var flag = Map.At(settler.X, settler.Y).LinkedSlot;
        var place = flags[flag].FirstEmptyPlace();
        if (place >= 0)
        {
            layGood(flag, place, good);
            history.RecordFlag(flag, EventType.PutGood, cause, history.SettlerId(slot).ToString());
        }
    }

    // The step from a flag into the building north-west of it (0x43c7c2, and the turn of state 6): the settler takes the
    // node when no settler stands on it, with the time of the step and the part of it the door's speed leaves, and is in
    // state 4; else it waits in state 6.
    internal void EnterHut(int slot, Settler settler)
    {
        if (settler.Activity is not EntersBuilding)
        {
            movement.Become(settler, new EntersBuilding());
        }

        var here = Map.Index(settler.X, settler.Y);
        var (x, y, node) = movement.Neighbour(settler, 4);
        if (movement.Occupant(node) != 0)
        {
            settler.Frame = 0x55;
            settler.Timer = 0;
            RecordWork(slot, EventType.EnterHut);

            return;
        }

        var shape = 0x28 + movement.Height(node) - movement.Height(here);
        movement.Move(slot, settler, x, y, node);
        settler.Frame = (byte)shape;
        var time = movement.Tables.StepTime(shape);
        settler.Timer = time;
        settler.Stamp = movement.Clock;
        var storage = ((EntersBuilding)settler.Activity).Storage;
        movement.Become(
            settler,
            new Door((ushort)(time * movement.Tables.DoorSpeed(movement.BuildingAt(node))) >> 5, storage)
        );
        RecordWork(slot, EventType.EnterHut);
    }

    /// <summary>
    /// The map's object pass (0x428d10, called by the main loop's 0x4286d8 each pass), run once each pass of the main loop by
    /// <see cref="MainPass"/>, before the job modules' pass hooks. Each 20 clock units it takes the next nodes of a cursor that moves 23 columns on and a row down
    /// where it wraps: on a lake node with detail it may move the detail (the draws of the original), a node with an object code of 0x53
    /// or more takes its handler: the cut tree codes 0x5d to 0x66 become 0x53, and 0x53 vanishes when the draw's low two bits are 0; the
    /// saplings 0x67 and 0x68 grow into trees (GrowSapling), so a felled tree turns into nothing in the time the original takes.
    /// **Not ported:** the handlers of the codes 0x69 and up (the original's growth and decay; the generation places none).
    /// </summary>
    public void ObjectPass()
    {
        var delta = (ushort)(movement.Clock - _objectStamp);
        _objectStamp = movement.Clock;
        var timer = _objectTimer;
        _objectTimer = (ushort)(timer - delta);
        if (delta <= timer)
        {
            return;
        }

        var count = 0;
        ushort due;
        do
        {
            count += (Map.Size.Width >> 5) * (Map.Size.Height >> 5);
            due = _objectTimer;
            _objectTimer = (ushort)(due + 20);
        } while (due < 0xffec);

        for (; count > 0; count--)
        {
            _objectX += 23;
            if (_objectX >= Map.Size.Width)
            {
                _objectX -= Map.Size.Width;
                _objectY = (_objectY + 1) & (Map.Size.Height - 1);
            }

            var node = Map.Index(_objectX, _objectY);
            ref var at = ref Map.At(node);
            if (at.WaterTriangle && at.LakeMark && at.LakeDetail != 0)
            {
                MoveLakeDetail(node);
            }

            var kind = at.Object.Kind;
            if (kind == MapObjectKind.Stump)
            {
                if ((draws.Draw() & 3) == 0)
                {
                    at.Object = MapObject.None;
                }
            }
            else if (kind == MapObjectKind.FallingTree)
            {
                at.Object = MapObject.Stump;
            }
            else if (kind == MapObjectKind.Sapling)
            {
                GrowSapling(ref at);
            }
        }
    }

    // The object pass's handler of the saplings (0x4290d5, 0x42910f): one draw; where its bits 8 and 9 are clear the sapling is
    // a tree of its own range, picked by the draw's low three bits, and keeps the node's water mark.
    void GrowSapling(ref Node node)
    {
        var draw = draws.Draw();
        if ((draw & 0x300) == 0)
        {
            node.Object = MapObject.Grown(node.Object, draw);
        }
    }

    // The lake detail of a node (0x428d6e): one draw; below 10 the detail may grow by one (when the draw's bits 8 to 13 are
    // 0), and it moves one to a neighbour that is a lake node too (south-east, south, north-west, or north by the draw's
    // bits 2 and 3).
    void MoveLakeDetail(int node)
    {
        var draw = draws.Draw();
        ref var at = ref Map.At(node);
        if ((sbyte)(at.LakeDetail - 10) >= 0)
        {
            return;
        }

        if ((draw & 0x3f00) == 0)
        {
            at.LakeDetail++;
        }

        var direction = (draw & 0xc) switch
        {
            0 => 1,
            4 => 2,
            8 => 4,
            _ => 5,
        };
        var (dx, dy) = Direction.Steps[direction];
        ref var neighbour = ref Map.At(node % Map.Size.Width + dx, node / Map.Size.Width + dy);
        if (neighbour.LakeMark)
        {
            at.LakeDetail--;
            neighbour.LakeDetail++;
        }
    }
}
