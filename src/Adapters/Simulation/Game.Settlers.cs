using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

// The buildings and the walking settlers: the castle's first settlers, the woodcutter's hut with its request for a
// worker, and the worker's walk from the castle over the roads (docs/rules/settlers.md). One tick is one pass of the
// original's main loop for the item tables (ADR 0005).
public sealed partial class Game
{
    // The phases of a pass cycle (docs/rules/timing.md). **Inferred** for a game with no computer opponent: the
    // original sets 33 where no player's start parameter at +0x1d6 is from 1 to 11 (0x42055e).
    private const int PhaseCount = 33;

    // The phases from this one on run no item table.
    private const int TablePhases = 32;

    // The clock units of one pass (ADR 0005).
    private const int ClockStep = 8;

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

    private readonly BuildingTable _buildings = new();
    private readonly SettlerTable _settlers = new();
    private readonly ActivityRegistry _behaviours = new();
    private readonly TablePass _tablePass;
    private readonly WalkHomeBehavior _walkHome;
    private readonly WoodcutterBehavior _woodcutter;
    private readonly GeneratorDraws _draws;

    // The castle's list of waiting settlers: for each job the slot of the settler that waits there (word at 0x42 + 2
    // times the job of the storage item), 0 for none.
    private readonly ushort[] _waiting = new ushort[32];

    /// <summary>The walk home: which settlers are on it, and the rules of its claim, search, and arrival.</summary>
    internal WalkHomeBehavior WalkHome => _walkHome;

    /// <summary>The behaviour of each activity type that the table pass hands a settler to.</summary>
    internal ActivityRegistry Behaviours => _behaviours;

    // The slot of the settler that waits in the castle's list for a job, 0 for none.
    internal int Waiting(Job job) => _waiting[job.Code];

    // Empties the castle's list of the settlers that wait for a job (a test that has the castle run out of settlers).
    internal void ClearWaiting(Job job) => _waiting[job.Code] = 0;

    // The settlers a request has sent out that have not left the castle yet (word at 0x4a of the storage item).
    private int _leaving;

    // The settler on each node (word at 2 of the second map record), the step onto a node, the timer, and the change of
    // activity: the ground the behaviours of the settlers share.
    private readonly SettlerMovement _movement;

    // The command cause a placed building carries until its worker has entered, the worker turns away, or the building is
    // freed: the events of the building and of the settler sent to it name the placing (history only; no rule reads it).
    private readonly Dictionary<ThingId, Cause> _buildingCommand = [];

    // The cause of a change to the building in the slot: the command that placed it while its worker is on the way, else the rule area.
    private Cause BuildingCause(int slot) =>
        _buildingCommand.TryGetValue(History.BuildingId(slot), out var command) ? command : RuleCause;

    // The cause of a change the settler rules make on their own: the rules the layout runs while it builds its entries
    // act for the layout, so inside a layout the cause is `layout` and not `settlers` (a deliberate choice: what a layout
    // sets up is the layout's, whichever rule area runs it).
    private Cause RuleCause => InLayout ? Cause.Layout : Cause.Settlers;

    // The cause of a change the settler rules make to the settler in the slot: the walk home while it walks to a storage
    // (a command cause it carries takes the place of either in RecordSettler), else the rule area's.
    private Cause SettlerCause(int slot) => _walkHome.IsWalking(slot) ? Cause.WalkHome : RuleCause;

    private void RecordBuildingOf(Building building, string type, string? other = null)
    {
        var slot = _buildings.SlotOf(building);
        History.RecordBuilding(slot, type, BuildingCause(slot), other);
    }

    // The game's random generator: it continues from the map's generation, as the original's does.
    /// <summary>The game's random generator: it starts as the map's generation left it and draws only in the passes.</summary>
    internal RandomGenerator Random => _draws.Generator;

    // The good of a log (the byte 7 the settler writes onto the flag, 0x438011).
    internal const int GoodLog = WoodcutterBehavior.GoodLog;

    // The draw of the game's generator: the new first word, which Rng_Step (0x466ade) and the draw at 0x44257a leave.
    private ushort Draw() => _draws.Draw();

    // The settler takes up another activity. The bytes of the original's item that the new activity does not use stay as they
    // were, in its residue.
    private void Become(Settler settler, Activity next) => _movement.Become(settler, next);

    // The settler takes up another activity with the bytes of the original's item the new activity does not use set to
    // `residue` (a builder that loses its job: SettlerEncoder.ResidueOfBuilder).
    private void Become(Settler settler, Activity next, Residue residue) => _movement.Become(settler, next, residue);

    // The behaviour of each activity type, for the table pass. The activities whose turn the original does not port have a
    // turn that changes nothing.
    private void RegisterBehaviours()
    {
        _behaviours.Register<WaitsInCastle>((slot, settler, _) => Idle(slot, settler));
        _behaviours.Register<BuildsCastle>((slot, settler, _) => Builder(slot, settler));
        _behaviours.Register<BuiltCastle>((_, _, _) => { });
        _behaviours.Register<RawState>((_, _, _) => { });
        _behaviours.Register<Leaving>((slot, settler, _) => Leave(slot, settler));
        _behaviours.Register<WaitsOutStep>((slot, settler, _) => WaitOutStep(slot, settler));
        _behaviours.Register<Walks>((slot, settler, _) => Walk(slot, settler));
        _behaviours.Register<WalksRoad>((slot, settler, _) => WalkRoadTurn(slot, settler));
        _behaviours.Register<WaitsAtFlag>((slot, settler, _) => Fetch(slot, settler));
        _behaviours.Register<ClaimsNode>((slot, settler, _) => Claim(slot, settler));
        _behaviours.Register<WaitsToGoHome>((slot, settler, _) => Dismissed(slot, settler));
        _behaviours.Register<WaitsToRejoin>((slot, settler, _) => Rejoined(slot, settler));
        _behaviours.Register<HandsOver>((slot, settler, _) => HandoverTurn(slot, settler));
        _woodcutter.Register(_behaviours);
        _walkHome.Register(_behaviours);
        _behaviours.Register<KnightWalks>((_, _, _) => { });
    }

    private ushort _clock;
    private int _phase;
    private int _scanCursor;

    /// <summary>The buildings in the order of their slots: the castle first, then each placed building.</summary>
    public IReadOnlyList<Building> Buildings => _buildings;

    /// <summary>The game clock in clock units (16 bits, 8 for each tick): the settlers' timers and stamps count in it.</summary>
    public ushort Clock => _clock;

    /// <summary>The settlers in the order of their slots: the castle's first 20, then each created one.</summary>
    public IReadOnlyList<Settler> Settlers => _settlers;

    // One pass of the main loop for the item tables: the scan that clears the retry bits (0x428728), the phase step
    // (0x4291c2), then the slices of the flag table (0x46414d, the carriers' requests only), the building table
    // (0x42cb2d), and the settler table (0x42f3d9). The original's other passes (the players' timers, the sound) are not
    // ported.
    private void Pass()
    {
        _clock = (ushort)(_clock + ClockStep);
        ClearRetryBits();
        ClearFlagRetryBits();
        _woodcutter.ObjectPass();
        _phase = (_phase + 1) % PhaseCount;
        if (_phase < TablePhases)
        {
            _tablePass.Run(
                _phase,
                FlagPass,
                (slot, building) =>
                {
                    if (building.IsDemolishing)
                    {
                        Burn(slot, building);
                    }
                    else
                    {
                        HandleBuilding(building);
                    }
                }
            );
        }

        if (!EveryPassTurns)
        {
            return;
        }

        // 0x42f4f1 gives each settler in view one more turn every pass; the engine gives it to every settler in an activity
        // the view draws in motion, so the walk on the screen does not stall between the slice turns and the simulation
        // does not depend on the view. The other activities keep the slice turns alone.
        for (var slot = 0; slot < _settlers.Count; slot++)
        {
            if (
                _settlers[slot].Activity
                is Walks
                    or WalksRoad
                    or Door
                    or WaitsOutStep
                    or HandsOver
                    or WalksFree
                    or Cuts
                    or SearchesHome
            )
            {
                _tablePass.Visit(slot);
            }
        }
    }

    /// <summary>
    /// Whether each pass gives every settler that walks, waits out a step, steps through a door, cuts, or searches
    /// (the activities of the states 2, 3, 4, 5, 0xe, 0x10, 0x11, 0x19) one more turn after the slices, as the original's view
    /// pass (<c>0x42f4f1</c>) does for the settlers in view. Off, a settler takes a turn only in its slice, every 16 or 17
    /// passes, as in the original's passes without a view.
    /// </summary>
    public bool EveryPassTurns { get; set; }

    // 0x428728: each pass clears the request-failed mark of a window of buildings from a cursor that wraps at the end of
    // the table, and stops after 10 buildings that had the mark set. A request that found no worker is made again
    // when its building's turn comes after that.
    private void ClearRetryBits()
    {
        var count = _buildings.Count;
        if (count == 0)
        {
            return;
        }

        if (count <= _scanCursor)
        {
            _scanCursor = 0;
        }

        var window = Math.Min(count - _scanCursor, _tables.ScanWindow(Map.Size.Value));
        var left = 10;
        for (var i = 0; i < window; i++)
        {
            var building = _buildings[_scanCursor];
            var had = building.RequestFailed;
            building.RequestFailed = false;
            if (had)
            {
                History.RecordBuilding(_scanCursor, EventType.RetryBitsCleared, BuildingCause(_scanCursor));
            }

            if (had && --left == 0)
            {
                return;
            }

            _scanCursor++;
        }
    }

    // The handler of a building type (the table at 0x42cd1d). Only the woodcutter asks for a worker; the castle's
    // handler (0x42e7e0, knights and defence) is not ported.
    private void HandleBuilding(Building building)
    {
        if (
            building.Type == BuildingType.Woodcutter
            && !(building.RequestFailed || building.WorkerInside || building.WorkerOnTheWay)
            && !RequestWorker(building, Job.Woodcutter)
        )
        {
            building.RequestFailed = true;
            RecordBuildingOf(building, EventType.BuildingHandled);
        }
    }

    // 0x42be63: the building asks the castle for a settler of a job. The search runs over the roads from the building's
    // flag; the castle's flag is the only storage, so it finds the settler where the flag is reached and the castle
    // holds a waiting settler of the job. **Not ported:** the second way, a new settler made of one that has no job
    // and a tool from the stock; the castle's 20 first settlers hold one woodcutter.
    private bool RequestWorker(Building building, Job job)
    {
        if (Route(building.Flag, 0) < 0 || _waiting[job.Code] == 0)
        {
            return false;
        }

        var slot = _waiting[job.Code];
        var settler = _settlers[slot];
        _waiting[job.Code] = 0;
        Become(settler, new Leaving(new ToBuilding(building.Flag)));
        building.WorkerOnTheWay = true;
        _leaving++;
        var buildingSlot = _buildings.SlotOf(building);
        var buildingId = History.BuildingId(buildingSlot);
        History.EndCarry(slot);
        if (_buildingCommand.TryGetValue(buildingId, out var command))
        {
            History.CarryCause(slot, command);
        }

        History.RecordSettler(slot, EventType.WorkerRequested, BuildingCause(buildingSlot), buildingId.ToString());
        History.RecordBuilding(
            buildingSlot,
            EventType.WorkerRequested,
            BuildingCause(buildingSlot),
            History.SettlerId(slot).ToString()
        );

        return true;
    }

    /// <summary>
    /// The route search over the flags (<c>0x42b4bc</c>, the copy of it in the settlers' walk, and <c>0x42ca7a</c>):
    /// the direction a settler at flag <paramref name="from"/> takes first to reach flag <paramref name="to"/>, or -1 where no
    /// road joins them. The search runs breadth first from <paramref name="from"/>, tries the directions of a flag
    /// from north (5) to east (0), and hands each flag it reaches the first direction of the flag it came from;
    /// of several routes of the same number of flags it keeps the first one it reached.
    /// </summary>
    public int Route(int from, int to)
    {
        if (from < 0 || to < 0 || from >= _flags.Count || to >= _flags.Count || from == to)
        {
            return -1;
        }

        var reached = new bool[_flags.Count];
        var first = new int[_flags.Count];
        var queue = new Queue<int>();
        reached[from] = true;
        for (var direction = 5; direction >= 0; direction--)
        {
            var next = FlagNeighbour(from, direction);
            if (next >= 0)
            {
                reached[next] = true;
                first[next] = direction;
                queue.Enqueue(next);
            }
        }

        while (queue.Count > 0)
        {
            var flag = queue.Dequeue();
            for (var direction = 5; direction >= 0; direction--)
            {
                var next = FlagNeighbour(flag, direction);
                if (next >= 0 && !reached[next])
                {
                    reached[next] = true;
                    first[next] = first[flag];
                    queue.Enqueue(next);
                }
            }
        }

        return reached[to] ? first[to] : -1;
    }

    // State 5 (0x42fc81 dispatches on the state byte through a table of 8-byte entries): when the step's time has run out the
    // settler takes up the activity it waited for.
    private void WaitOutStep(int slot, Settler settler)
    {
        if (TimerRan(settler))
        {
            settler.Timer = 0;
            settler.Activity = ((WaitsOutStep)settler.Activity).Then;
        }

        History.RecordSettler(slot, EventType.Visited, RuleCause);
    }

    // State 3: a carrier on its road takes its turn when its timer has run out.
    private void WalkRoadTurn(int slot, Settler settler)
    {
        if (TimerRan(settler))
        {
            WalkRoad(slot, settler);
        }
        else
        {
            History.RecordSettler(slot, EventType.Visited, RuleCause);
        }
    }

    // State 0xe: a carrier handing a good over takes its turn when its timer has run out.
    private void HandoverTurn(int slot, Settler settler)
    {
        if (TimerRan(settler))
        {
            Handover(slot, settler);
        }
        else
        {
            History.RecordSettler(slot, EventType.Visited, RuleCause);
        }
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
            _waiting[settler.Job.Code] = (ushort)slot;
        }
    }

    // The draws below which a knight of the level 0 to 3 (the jobs 22 to 25) is made a knight of the next level
    // (0x43910b, 0x4391eb, 0x4392cb, 0x4393ab).
    private static readonly int[] PromotionLimits = [4000, 2000, 1000, 500];

    // A knight waiting in the castle (0x43910b and its three siblings). When its timer has run out it draws until a draw is
    // below its level's limit, which promotes it (job plus one, timer 6000, and its entry in the castle's list moves to
    // the new job), adding 6000 to the timer for each draw above it until that wraps. The last level only waits.
    private void KnightIdle(int slot, Settler settler)
    {
        var level = settler.Job.KnightLevel;
        if (level < PromotionLimits.Length)
        {
            var delta = (ushort)(_clock - settler.Stamp);
            settler.Stamp = _clock;
            var old = settler.Timer;
            settler.Timer = (ushort)(old - delta);
            if (delta > old)
            {
                while (true)
                {
                    if (Draw() < PromotionLimits[level])
                    {
                        _waiting[settler.Job.Code] = 0;
                        settler.Job = settler.Job.Promoted;
                        settler.Timer = 6000;
                        break;
                    }

                    var timer = settler.Timer;
                    settler.Timer = (ushort)(timer + 6000);
                    if (timer >= 0xe890)
                    {
                        break;
                    }
                }
            }

            History.RecordSettler(slot, EventType.KnightIdled, RuleCause);
        }

        _waiting[settler.Job.Code] = (ushort)slot;
    }

    // State 10, the settler that stands by while the castle is built (0x43f17a): each turn adds the elapsed units
    // times 128 to the castle's building progress, and when it wraps past 65536 the castle stands. The settler then
    // takes state 12, which is not ported.
    private void Builder(int slot, Settler settler)
    {
        var delta = (ushort)(_clock - settler.Stamp);
        settler.Stamp = _clock;
        var castle = _buildings[0];
        var old = castle.Progress;
        castle.Progress = (ushort)(old + (ushort)(delta << 7));
        if (castle.Progress < old)
        {
            Become(settler, new BuiltCastle());
            _movement.Release(Map.Index(settler.X, settler.Y));
            castle.Constructing = false;
            castle.Worker = 0;
        }

        History.RecordSettler(slot, EventType.Builder, RuleCause, History.BuildingId(0).ToString());
        History.RecordBuilding(0, EventType.Builder, BuildingCause(0), History.SettlerId(slot).ToString());
    }

    private bool TimerRan(Settler settler) => _movement.TimerRan(settler);

    private int Occupant(int index) => _movement.Occupant(index);

    private int Height(int index) => _movement.Height(index);

    private Building? BuildingAt(int index) => _movement.BuildingAt(index);

    private (int X, int Y, int Index) Neighbour(Settler settler, int direction) =>
        _movement.Neighbour(settler, direction);

    // State 15, a settler that leaves the castle by its door onto the castle's flag (0x43dd4a): it steps south-east
    // when that node and the node of the building it walks to are free, takes the time of the step times the door's
    // speed, and waits it out in state 5. A busy node keeps it in state 15.
    private void Leave(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, next) = Neighbour(settler, 1);
        var leaving = (Leaving)settler.Activity;
        var target = leaving.Errand is ToBuilding building
            ? _buildings.FirstOrDefault(b => b.Flag == building.At)
            : null;
        if (
            Occupant(here) != 0
            || Occupant(next) != 0
            || (target is not null && Occupant(Map.Index(target.X, target.Y)) != 0)
        )
        {
            settler.Frame = 0x52;
            settler.Timer = 0;
            History.RecordSettler(slot, EventType.Left, RuleCause);
            return;
        }

        _leaving--;
        var door = BuildingAt(here);
        Move(slot, settler, x, y, next);
        var shape = Height(next) - Height(here) + 13;
        settler.Frame = (byte)shape;
        var speed = (byte)(_tables.DoorSpeed(door) ^ 0x1f);
        settler.Timer = (ushort)((ushort)(_tables.StepTime(shape) * speed) >> 5);
        settler.Stamp = _clock;
        settler.Activity = new WaitsOutStep(new Walks(leaving.Errand, new Heading(0)));
        History.RecordSettler(slot, EventType.Left, RuleCause);
    }

    private void Move(int slot, Settler settler, int x, int y, int index) => _movement.Move(slot, settler, x, y, index);

    // State 2, a settler on the road (0x4398ea with the step routines at 0x43a64c to 0x43ac8b). When its timer has run
    // out it chooses a direction (on a node of a road the one the road goes on, on a flag the first direction of the
    // route to the flag it walks to), steps, and adds the step's time to its timer. The sum wraps while the step
    // was over already, and the settler then goes on at once; else it waits out the rest. At the flag it walks to
    // it enters the building there, or, as a carrier, the road. A step onto a node another settler stands on is tried
    // again after the timer of 0x7f. The mode 0xfe is the walk home to a storage: the storage is picked at the first flag
    // (StorageFlag), and at its flag the settler enters it as a worker enters its hut. A step east, south-east, or south onto
    // a settler that comes the other way and is blocked passes it (Step). **Not ported:** the states the original reaches
    // from here for other modes.
    private void Walk(int slot, Settler settler)
    {
        // `walked` records the turn of state 2; a step or entry inside it records its own change first (`stepped`, `entered`).
        WalkTurn(slot, settler);
        History.RecordSettler(slot, EventType.Walked, SettlerCause(slot));
    }

    private void WalkTurn(int slot, Settler settler)
    {
        if (!TimerRan(settler))
        {
            return;
        }

        while (true)
        {
            var walks = (Walks)settler.Activity;
            var here = Map.Index(settler.X, settler.Y);
            int direction;
            if (walks.Heading.IsRetried)
            {
                // A step that was blocked: the heading holds the direction less 6, tried again. Each try counts in byte 15;
                // at 10 tries on a flag and 50 elsewhere the original follows the chain of settlers that wait for one another
                // (0x439985) and counts from 0 again. **Not ported:** that the chain, where it comes back to the settler,
                // makes the settler step against it and pass the settler there (as for the swap of Step); every other chain
                // ends with the count at 0 and the same direction tried again.
                var tries = walks.Tries + 1;
                settler.Activity = walks with { Tries = tries >= (Map.At(here).FlagMark ? 10 : 50) ? 0 : tries };
                direction = walks.Heading.Direction;
            }
            else if (!Map.At(here).FlagMark)
            {
                // A node of a road: the road's other direction than the one the settler came by.
                direction = RoadContinuation(Map.At(here).Roads, walks.Heading.Direction);
                if (direction < 0)
                {
                    settler.Timer = 0;
                    return;
                }
            }
            else
            {
                var errand = walks.Errand;
                if (errand is ToStorage && _walkHome.StorageFlag(Map.At(here).LinkedSlot) < 0)
                {
                    // No storage chosen yet (0x45d09b); none a road leads to sends the settler to look for a flag again,
                    // from the far end of the search.
                    Become(settler, new SearchesHome(true));
                    settler.Timer = 0;
                    return;
                }

                if (errand.Flag == Map.At(here).LinkedSlot)
                {
                    // An errand to a building or a storage enters the building north-west of the flag; one to a road enters
                    // the road.
                    if (errand is ToRoad)
                    {
                        EnterRoad(slot, settler);
                    }
                    else
                    {
                        Enter(slot, settler);
                    }

                    return;
                }

                direction = Route(Map.At(here).LinkedSlot, errand.Flag);
                if (direction < 0 && errand is ToStorage)
                {
                    // 0x43a24e: a settler on its way to a storage that finds no road leads on looks for a flag again.
                    Become(settler, new SearchesHome(true));
                    settler.Timer = 0;
                    return;
                }

                if (direction < 0)
                {
                    // No road leads on. The worker is not coming any more; it turns to go home (the walk home).
                    var hut = _buildings.FirstOrDefault(b => b.Flag == errand.Flag);
                    if (hut is not null)
                    {
                        hut.WorkerOnTheWay = false;
                        RecordBuildingOf(hut, EventType.Walked, History.SettlerId(slot).ToString());
                        _buildingCommand.Remove(History.BuildingId(_buildings.SlotOf(hut)));
                    }

                    settler.Activity = walks with { Errand = new ToStorage() };
                    settler.Timer = 0;
                    return;
                }
            }

            if (!Step(slot, settler, direction))
            {
                return;
            }
        }
    }

    // The heading of a settler that walks the roads (state 2), a carrier on its road (state 3), or one that hands a good over
    // (state 0xe); null for any other activity.
    private static Heading? HeadingOf(Activity activity) =>
        activity switch
        {
            Walks walks => walks.Heading,
            WalksRoad road => road.Heading,
            HandsOver handover => handover.Heading,
            _ => null,
        };

    // The good a carrier carries on its road or hands over; null for a carrier with none and for any other activity.
    private static Load? LoadOf(Activity activity) =>
        activity switch
        {
            WalksRoad road => road.Load,
            HandsOver handover => handover.Load,
            _ => null,
        };

    // The activity of a walking settler with a new heading.
    private static Activity WithHeading(Activity activity, Heading heading) =>
        activity switch
        {
            Walks walks => walks with { Heading = heading },
            WalksRoad road => road with { Heading = heading },
            _ => activity,
        };

    // The activity of a walking settler with the count of its blocked tries, or the mark that it is to go, cleared (byte 15 = 0).
    private static Activity Cleared(Activity activity) =>
        activity switch
        {
            Walks walks => walks with { Tries = 0 },
            WalksRoad road => road with { Waits = 0, Marked = false },
            HandsOver handover => handover with { Marked = false },
            _ => activity,
        };

    // Whether a carrier is marked to go: its road was taken down or joined (byte 15 = 0xff).
    private static bool IsMarkedToGo(Activity activity) =>
        activity is WalksRoad { Marked: true } or HandsOver { Marked: true };

    // One step on a road; false where the settler waits out the rest of the step's time or the step is blocked.
    private bool Step(int slot, Settler settler, int direction)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, next) = Neighbour(settler, direction);
        int shape;
        var other = Occupant(next);
        var forward = Heading.Facing(new Direction(direction).Opposite);
        if (other == 0)
        {
            shape = 4 + 9 * direction + Height(next) - Height(here);
            settler.Activity = Cleared(WithHeading(settler.Activity, forward));
            Move(slot, settler, x, y, next);
        }
        else if (
            direction <= 2
            && _settlers[other] is { Activity: Walks or WalksRoad } passer
            && HeadingOf(passer.Activity) == forward.Retried
        )
        {
            // A step east, south-east, or south onto a settler that walks (state 2 or 3) and is blocked on this very node (its
            // heading holds the opposite direction less 6): the two pass, each taking the node of the other (0x43a6c1 and its
            // siblings; the steps west, north-west, and north have no such branch). The settler goes on with the shape of its
            // swap step (the shapes 4 + 9 * (direction + 6) and up), the other on with the one of its opposite step, and its
            // timer is that step's time.
            var rise = Height(next) - Height(here);
            var back = 4 + 9 * ((direction + 3) % 6) - rise;
            passer.X = settler.X;
            passer.Y = settler.Y;
            _movement.Occupy(here, other);
            passer.Activity = WithHeading(passer.Activity, Heading.Facing(direction));
            passer.Frame = (byte)back;
            passer.Timer = _tables.StepTime(back);
            History.RecordSettler(other, EventType.Stepped, SettlerCause(other), History.SettlerId(slot).ToString());
            shape = 4 + 9 * (direction + 6) + rise;
            settler.Activity = WithHeading(settler.Activity, forward);
            settler.X = x;
            settler.Y = y;
            _movement.Occupy(next, slot);
        }
        else
        {
            // The settler there waits, or waits for this node. The settler marks the direction it tried in its heading
            // (the direction less 6) and tries it again when its timer of 0x7f has run out (0x43a791).
            settler.Timer = 0x7f;
            settler.Frame = (byte)(0x51 + direction);
            settler.Activity = WithHeading(settler.Activity, Heading.Facing(direction).Retried);
            History.RecordSettler(slot, EventType.Stepped, SettlerCause(slot));

            return false;
        }

        settler.Activity = Cleared(settler.Activity);
        var old = settler.Timer;
        settler.Timer = (ushort)(old + _tables.StepTime(shape));
        var finished = settler.Timer < old;
        if (finished)
        {
            settler.Frame = (byte)shape;
        }

        History.RecordSettler(slot, EventType.Stepped, SettlerCause(slot));

        return !finished;
    }

    // A settler at the flag it walks to goes on to the building north-west of the flag (0x4398ea): the building takes
    // its worker (status bit 6, and bit 7, the worker on the way, ends), the settler steps onto the building's node,
    // and takes state 4, the step through the door, with the time of the step and the part of it that the door's speed
    // leaves. A busy node keeps the settler on the flag in state 6.
    private void Enter(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, node) = Neighbour(settler, 4);
        var building = BuildingAt(node);
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

        var buildingId = History.BuildingId(_buildings.SlotOf(building));
        RecordBuildingOf(building, EventType.Entered, History.SettlerId(slot).ToString());
        var storage = settler.Activity.GoingTo is ToStorage;
        Become(settler, new EntersBuilding(storage));
        if (Occupant(node) != 0)
        {
            settler.Frame = 0x55;
            settler.Timer = 0;
            History.RecordSettler(slot, EventType.Entered, SettlerCause(slot), buildingId.ToString());
            return;
        }

        Move(slot, settler, x, y, node);
        var shape = 0x28 + Height(node) - Height(here);
        settler.Frame = (byte)shape;
        var time = _tables.StepTime(shape);
        settler.Timer = time;
        settler.Stamp = _clock;
        Become(settler, new Door((ushort)(time * _tables.DoorSpeed(building)) >> 5, storage));
        History.RecordSettler(slot, EventType.Entered, SettlerCause(slot), buildingId.ToString());
        _buildingCommand.Remove(buildingId);
    }

    // The castle's 20 first settlers (0x442f0c with 0x44333d): all in the castle, in state 1 but the first, which stands by
    // while the castle is built. Their time stamp is the clock of the placement.
    private void CreateCastleSettlers(int x, int y)
    {
        // The castle's status 0x50: the worker is inside, and the bit 4 has no recovered meaning.
        _buildings.Add(
            new Building(BuildingType.Castle, x, y, 0, constructing: true)
            {
                WorkerInside = true,
                OtherStatus = 0x10,
                FillLow = 0xff,
                FillHigh = 0xff,
            }
        );
        History.FillBuilding(0);
        foreach (var job in CastleJobs)
        {
            _settlers.Add(
                new Settler(job, 0, x, y, _clock, job == Job.Builder ? new BuildsCastle() : new WaitsInCastle())
            );
            History.FillSettler(_settlers.Count - 1);
        }
    }

    // The woodcutter's hut at a building node tied to the flag south-east of it. The hut stands finished at once: a
    // **stand-in** until the construction slice, not a rule of the original. The map takes the marks the castle's
    // placement leaves, with the object code 2 (**inferred** for a small building; no run places one).
    private bool PlaceBuilding(int x, int y)
    {
        var width = Map.Size.Width;
        var height = Map.Size.Height;
        var flagX = (x + 1) & (width - 1);
        var flagY = (y + 1) & (height - 1);
        var flag = _flags.SlotAt(flagX, flagY);
        var site = Castle is null ? default : BuildSite.Evaluate(_tables, Map, x, y, 0, hasCastle: true);
        // **Inferred:** a small hut needs the site of a small or a large building (3 or 4), and either the flag standing
        // south-east of it (node state 5), a road's bits on that node (6), or nothing on or around it (7); the flag button
        // takes the same states for its flag (4, 6, 7 of the flag's own node).
        var withFlag = site.Node == 5 && flag >= 0 && BuildingOfFlag(flag) is null;
        if (site.Site < 3 || !(withFlag || site.Node is 6 or 7 && !_flags.IsFull))
        {
            return false;
        }

        // The building's placement (0x449a63) puts its flag down on the south-east node where none stands there, as the
        // flag placement does (0x4422c5): the flag object and bit, the deposit moved, the link to the new slot.
        if (!withFlag)
        {
            FlagPlacement.Apply(Map, flagX, flagY, _flags.NextSlot);
            flag = AddFlag(flagX, flagY);
            History.RecordFlag(flag, EventType.PlaceFlag, CommandCause(History.FlagId(flag)));

            // A flag on a road's node splits the road (0x4662e2), before the hut's mark at the flag is set below: the mark
            // (bit 4 of the flag's direction byte) is no road bit.
            if (site.Node == 6)
            {
                SplitRoad(Map.Index(flagX, flagY), flag, CommandCause(History.FlagId(flag)));
            }
        }

        var node = Map.Index(x, y);
        var slot = _buildings.NextSlot;
        ref var hutNode = ref Map.At(node);
        hutNode.Roads = hutNode.Roads.With(1);
        hutNode.LakeMark = true;
        hutNode.Object = MapObject.Hut;
        hutNode.LinkTo(slot);
        ref var flagNode = ref Map.At(flagX, flagY);
        flagNode.Roads = flagNode.Roads.With(4);
        _buildings.Add(new Building(BuildingType.Woodcutter, x, y, flag));

        History.FillBuilding(slot);
        var cause = CommandCause(History.BuildingId(slot));
        if (cause.IsCommand)
        {
            _buildingCommand[History.BuildingId(slot)] = cause;
        }

        History.RecordBuilding(slot, EventType.PlaceBuilding, cause, History.FlagId(flag).ToString());

        return true;
    }

    // The building a flag is tied to: the one that stands at it, not one that burns (0x4617fd takes the pointer from the flag's item).
    private Building? BuildingOfFlag(int flag) =>
        _buildings.FirstOrDefault(building => !building.IsFree && !building.IsDemolishing && building.Flag == flag);

    private void MixWoodcutting(ref ulong hash)
    {
        MixGoods(ref hash);
        var random = _draws.Generator;
        hash = Mix(Mix(Mix(hash, random.W0), random.W1), random.W2);
        var (stamp, timer, x, y) = _woodcutter.ObjectPassState;
        hash = Mix(Mix(Mix(Mix(hash, stamp), timer), x), y);
        foreach (var good in FlagEncoder.Places(_flags))
        {
            hash = Mix(hash, good);
        }
    }

    private void MixSettlers(ref ulong hash)
    {
        hash = Mix(Mix(Mix(Mix(hash, _clock), _phase), _scanCursor), _leaving);
        foreach (var building in _buildings)
        {
            foreach (
                var value in new[]
                {
                    BuildingEncoder.TypeIndex(building),
                    building.X,
                    building.Y,
                    building.Flag,
                    BuildingEncoder.Status(building),
                    BuildingEncoder.Worker(building),
                    building.Progress,
                    BuildingEncoder.Fill(building),
                    building.Stamp,
                }
            )
            {
                hash = Mix(hash, value);
            }
        }

        foreach (var settler in _settlers)
        {
            var raw = SettlerEncoder.Fields(settler);
            foreach (
                var value in new[]
                {
                    settler.Job.Code,
                    settler.Player,
                    settler.Frame,
                    settler.Timer,
                    settler.X,
                    settler.Y,
                    raw.Stamp,
                    raw.State,
                    raw.Mode,
                    raw.Target,
                    raw.Heading,
                    raw.Next,
                }
            )
            {
                hash = Mix(hash, value);
            }
        }

        foreach (var slot in _waiting)
        {
            hash = Mix(hash, slot);
        }

        foreach (var (index, slot) in _movement.Occupied)
        {
            hash = Mix(Mix(hash, index), slot);
        }
    }
}
