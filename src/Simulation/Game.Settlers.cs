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
    private static readonly int[] CastleJobs = [4, 21, 21, 21, 21, 21, 22, 22, 22, 18, 5, 6, 7, 2, 3, 11, 20, 20, 9, 9];

    // The job of the settler that stands by while the castle is built, and the job of the woodcutter (the job the handler
    // 0x42d945 of the building type 2 asks for, whose work is to cut trees: docs/rules/woodcutting.md).
    private const int BuilderJob = 4;
    private const int WoodcutterJob = Settler.WoodcutterJob;

    // The first job of the knights. Knights and the castle's own handler are not ported.
    private const int FirstKnightJob = 22;

    private readonly List<Building> _buildings = [];
    private readonly List<Settler> _settlers = [];

    // The castle's list of waiting settlers: for each job the slot of the settler that waits there (word at 0x42 + 2
    // times the job of the storage item), 0 for none.
    private readonly ushort[] _waiting = new ushort[32];

    // The slot of the settler that waits in the castle's list for a job, 0 for none.
    internal int Waiting(int job) => _waiting[job];

    // The settlers a request has sent out that have not left the castle yet (word at 0x4a of the storage item).
    private int _leaving;

    // The settler on each node by node index (word at 2 of the second map record), 0 for none. A node of settler 0
    // reads as free, as in the original.
    private readonly Dictionary<int, int> _occupant = [];

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
        ObjectPass();
        _phase = (_phase + 1) % PhaseCount;
        if (_phase >= TablePhases)
        {
            return;
        }

        FlagPass();
        for (var slot = 0; slot < _buildings.Count; slot++)
        {
            if (slot / 32 % 32 != _phase || _buildings[slot].IsFree)
            {
                continue;
            }

            if (_buildings[slot].IsDemolishing)
            {
                Burn(slot, _buildings[slot]);
            }
            else
            {
                HandleBuilding(_buildings[slot]);
            }
        }

        for (var slot = _phase & 15; slot < _settlers.Count; slot += 16)
        {
            Visit(slot, _settlers[slot]);
        }
    }

    // 0x428728: each pass clears bit 2 of the status of a window of buildings from a cursor that wraps at the end of
    // the table, and stops after 10 buildings that had the bit set. A request that found no worker is made again
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

        var window = Math.Min(count - _scanCursor, _tables.ScanWindows[Map.Size.Value]);
        var left = 10;
        for (var i = 0; i < window; i++)
        {
            var building = _buildings[_scanCursor];
            var had = (building.Status & 4) != 0;
            building.Status &= ~4;
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
            building.Type == Building.WoodcutterType
            && (building.Status & 0xc4) == 0
            && !RequestWorker(building, WoodcutterJob)
        )
        {
            building.Status |= 4;
        }
    }

    // 0x42be63: the building asks the castle for a settler of a job. The search runs over the roads from the building's
    // flag; the castle's flag is the only storage, so it finds the settler where the flag is reached and the castle
    // holds a waiting settler of the job. **Not ported:** the second way, a new settler made of one that has no job
    // and a tool from the stock; the castle's 20 first settlers hold one woodcutter.
    private bool RequestWorker(Building building, int job)
    {
        if (Route(building.Flag, 0) < 0 || _waiting[job] == 0)
        {
            return false;
        }

        var settler = _settlers[_waiting[job]];
        _waiting[job] = 0;
        settler.Mode = 0xff;
        settler.Target = building.Flag;
        settler.State = 0xf;
        building.Status |= 0x80;
        _leaving++;

        return true;
    }

    // The flag a road leads to from a flag in a direction, or -1 where no road leaves that way.
    private int Link(int flag, int direction)
    {
        foreach (var road in _roads)
        {
            if (road.FromSlot == flag && road.FromDirection == direction)
            {
                return road.ToSlot;
            }

            if (road.ToSlot == flag && road.ToDirection == direction)
            {
                return road.FromSlot;
            }
        }

        return -1;
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
            var next = Link(from, direction);
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
                var next = Link(flag, direction);
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

    // The turn of a settler (0x42fc81 dispatches on the state byte through a table of 8-byte entries). The states the
    // walk needs are ported; the others leave the settler as it is.
    private void Visit(int slot, Settler settler)
    {
        switch (settler.State)
        {
            case 1:
                Idle(slot, settler);
                break;
            case 10:
                Builder(settler);
                break;
            case 0xf:
                Leave(slot, settler);
                break;
            case 5:
                if (!TimerRan(settler))
                {
                    break;
                }

                settler.Timer = 0;
                settler.State = settler.Next;
                settler.Next = 0;
                break;
            case 2:
                Walk(slot, settler);
                break;
            case 3:
                if (TimerRan(settler))
                {
                    WalkRoad(slot, settler);
                }

                break;
            case 0x42:
                Fetch(slot, settler);
                break;
            case 0x43:
                Claim(slot, settler);
                break;
            case 0x44:
                Dismissed(slot, settler);
                break;
            case 0x45:
                Rejoined(slot, settler);
                break;
            case 0xe:
                if (TimerRan(settler))
                {
                    Handover(slot, settler);
                }

                break;
            case 4:
                Door(settler);
                break;
            case 6:
                EnterHut(slot, settler);
                break;
            case 7:
                StepOut(slot, settler);
                break;
            case 0x10:
                FreeWalk(slot, settler);
                break;
            case 0x11:
                Cut(settler);
                break;
            case 0x12:
                SearchTree(slot, settler);
                break;
            case 0x19:
                HomeSearch(settler);
                break;
            case 0x1c:
                HomeClaim(slot, settler);
                break;
        }
    }

    // State 1, a settler waiting in the castle (0x438eeb): it enters its slot in the castle's list of waiting settlers
    // each turn. The job 10 takes another branch (not ported); the knights, the jobs 22 to 26, also draw
    // (docs/rules/woodcutting.md, "The random generator").
    private void Idle(int slot, Settler settler)
    {
        if (settler.Job >= FirstKnightJob)
        {
            KnightIdle(slot, settler);
        }
        else if (settler.Job != 10)
        {
            _waiting[settler.Job] = (ushort)slot;
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
        var level = settler.Job - FirstKnightJob;
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
                        _waiting[settler.Job] = 0;
                        settler.Job++;
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
        }

        _waiting[settler.Job] = (ushort)slot;
    }

    // State 10, the settler that stands by while the castle is built (0x43f17a): each turn adds the elapsed units
    // times 128 to the castle's building progress, and when it wraps past 65536 the castle stands. The settler then
    // takes state 12, which is not ported.
    private void Builder(Settler settler)
    {
        var delta = (ushort)(_clock - settler.Stamp);
        settler.Stamp = _clock;
        var castle = _buildings[0];
        var old = castle.Progress;
        castle.Progress = (ushort)(old + (ushort)(delta << 7));
        if (castle.Progress < old)
        {
            settler.State = 0xc;
            _occupant.Remove(Map.Index(settler.X, settler.Y));
            castle.Constructing = false;
            castle.Worker = 0;
        }
    }

    // The timer of the states that wait: the time since the settler's last turn comes off its timer; true where the
    // timer ran out, that is where the elapsed time was more than the time left (0x43dceb).
    private bool TimerRan(Settler settler)
    {
        var delta = (ushort)(_clock - settler.Stamp);
        settler.Stamp = _clock;
        var old = settler.Timer;
        settler.Timer = (ushort)(old - delta);

        return delta > old;
    }

    private int Occupant(int index) => _occupant.TryGetValue(index, out var slot) ? slot : 0;

    private int Height(int index) => Map.Layer(MapLayer.Height)[index] & Map.HeightMask;

    // The building on a node: the link byte of the node names its slot, and the building must stand on the node, since
    // the byte also holds a flag's slot or a resource deposit.
    private Building? BuildingAt(int index)
    {
        var slot = Map.Layer(MapLayer.Resource)[index];

        return
            slot < _buildings.Count
            && !_buildings[slot].IsFree
            && Map.Index(_buildings[slot].X, _buildings[slot].Y) == index
            ? _buildings[slot]
            : null;
    }

    // The index of the node a step in a direction leads to, and the settler's new column and row.
    private (int X, int Y, int Index) Neighbour(Settler settler, int direction)
    {
        var (dx, dy) = RoadBuilding.Steps[direction];
        var x = (settler.X + dx) & (Map.Size.Width - 1);
        var y = (settler.Y + dy) & (Map.Size.Height - 1);

        return (x, y, Map.Index(x, y));
    }

    // State 15, a settler that leaves the castle by its door onto the castle's flag (0x43dd4a): it steps south-east
    // when that node and the node of the building it walks to are free, takes the time of the step times the door's
    // speed, and waits it out in state 5. A busy node keeps it in state 15.
    private void Leave(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, next) = Neighbour(settler, 1);
        var target = settler.Mode == 0xff ? _buildings.FirstOrDefault(b => b.Flag == settler.Target) : null;
        if (
            Occupant(here) != 0
            || Occupant(next) != 0
            || (target is not null && Occupant(Map.Index(target.X, target.Y)) != 0)
        )
        {
            settler.Frame = 0x52;
            settler.Timer = 0;
            return;
        }

        _leaving--;
        var door = BuildingAt(here);
        settler.Heading = 0;
        settler.Next = settler.Mode == 0xfd ? 0x49 : 2;
        Move(slot, settler, x, y, next);
        var shape = Height(next) - Height(here) + 13;
        settler.Frame = (byte)shape;
        var speed = (byte)(_tables.DoorSpeeds[door?.TypeIndex ?? 0] ^ 0x1f);
        settler.Timer = (ushort)((ushort)(_tables.StepTime(shape) * speed) >> 5);
        settler.Stamp = _clock;
        settler.State = 5;
    }

    private void Move(int slot, Settler settler, int x, int y, int index)
    {
        _occupant.Remove(Map.Index(settler.X, settler.Y));
        settler.X = x;
        settler.Y = y;
        _occupant[index] = slot;
    }

    // State 2, a settler on the road (0x4398ea with the step routines at 0x43a64c to 0x43ac8b). When its timer has run
    // out it chooses a direction (on a node of a road the one the road goes on, on a flag the first direction of the
    // route to the flag it walks to), steps, and adds the step's time to its timer. The sum wraps while the step
    // was over already, and the settler then goes on at once; else it waits out the rest. At the flag it walks to
    // it enters the building there, or, as a carrier, the road. A step onto a node another settler stands on is tried
    // again after the timer of 0x7f. The mode 0xfe is the walk home to a storage: the storage is picked at the first flag
    // (StorageFlag), and at its flag the settler enters it as a worker enters its hut. **Not ported:** the swap with a
    // settler that comes the other way and the states the original reaches from here for other modes.
    private void Walk(int slot, Settler settler)
    {
        if (!TimerRan(settler))
        {
            return;
        }

        var flags = Map.Layer(MapLayer.Flags);
        var links = Map.Layer(MapLayer.Resource);
        while (true)
        {
            var here = Map.Index(settler.X, settler.Y);
            int direction;
            if (settler.Heading < 0)
            {
                // A step that was blocked: the heading holds the direction less 6, tried again. Each try counts in byte 15;
                // at 10 tries on a flag and 50 elsewhere the original follows the chain of settlers that wait for one another
                // (0x439985) and counts from 0 again. **Not ported:** that the chain, where it comes back to the settler,
                // makes the settler step against it and pass the settler there (as for the swap of Step); every other chain
                // ends with the count at 0 and the same direction tried again.
                settler.Next++;
                if (settler.Next >= ((flags[here] & 0x80) != 0 ? 10 : 50))
                {
                    settler.Next = 0;
                }

                direction = settler.Heading + 6;
            }
            else if ((flags[here] & 0x80) == 0)
            {
                // A node of a road: the road's other direction than the one the settler came by.
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
                if (settler.Mode == StorageWalk && settler.Target == 0)
                {
                    // No storage chosen yet (0x45d09b); none a road leads to sends the settler to look for a flag again,
                    // from the far end of the search.
                    var storage = StorageFlag(links[here]);
                    if (storage < 0)
                    {
                        settler.State = 0x19;
                        settler.Mode = 1;
                        settler.Timer = 0;
                        return;
                    }

                    settler.Target = storage;
                }

                if (settler.Target == links[here])
                {
                    // A negative mode (a byte of 0x80 or more) enters the building north-west of the flag.
                    if (settler.Mode >= 0x80)
                    {
                        Enter(slot, settler);
                    }
                    else if (settler.Mode <= 5)
                    {
                        EnterRoad(slot, settler);
                    }

                    return;
                }

                direction = Route(links[here], settler.Target);
                if (direction < 0 && settler.Mode is >= 0x80 and < 0xff)
                {
                    // 0x43a24e: a settler on its way to a storage that finds no road leads on looks for a flag again.
                    settler.State = 0x19;
                    settler.Mode = 1;
                    settler.Timer = 0;
                    return;
                }

                if (direction < 0)
                {
                    // No road leads on. The worker is not coming any more; it turns to go home (the mode 0xfe walk).
                    var hut = _buildings.FirstOrDefault(b => b.Flag == settler.Target);
                    if (hut is not null)
                    {
                        hut.Status &= 0x7f;
                    }

                    settler.Mode = 0xfe;
                    settler.Target = 0;
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

    // One step on a road; false where the settler waits out the rest of the step's time or the step is blocked.
    private bool Step(int slot, Settler settler, int direction)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, next) = Neighbour(settler, direction);
        int shape;
        var other = Occupant(next);
        if (other == 0)
        {
            shape = 4 + 9 * direction + Height(next) - Height(here);
            settler.Heading = (direction + 3) % 6;
            Move(slot, settler, x, y, next);
        }
        else if (
            direction <= 2
            && _settlers[other] is { State: 2 or 3 } passer
            && passer.Heading == (direction + 3) % 6 - 6
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
            _occupant[here] = other;
            passer.Heading = direction;
            passer.Frame = (byte)back;
            passer.Timer = _tables.StepTime(back);
            shape = 4 + 9 * (direction + 6) + rise;
            settler.Heading = (direction + 3) % 6;
            settler.X = x;
            settler.Y = y;
            _occupant[next] = slot;
        }
        else
        {
            // The settler there waits, or waits for this node. The settler marks the direction it tried in its heading
            // (the direction less 6) and tries it again when its timer of 0x7f has run out (0x43a791).
            settler.Timer = 0x7f;
            settler.Frame = (byte)(0x51 + direction);
            settler.Heading = direction - 6;

            return false;
        }

        settler.Next = 0;
        var old = settler.Timer;
        settler.Timer = (ushort)(old + _tables.StepTime(shape));
        if (settler.Timer < old)
        {
            settler.Frame = (byte)shape;

            return false;
        }

        return true;
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

        building.Status |= 0x40;
        var coming = (building.Status & 0x80) != 0;
        building.Status &= 0x7f;
        if (coming)
        {
            building.Worker = slot;
        }

        settler.State = 6;
        if (Occupant(node) != 0)
        {
            settler.Frame = 0x55;
            settler.Timer = 0;
            return;
        }

        Move(slot, settler, x, y, node);
        var shape = 0x28 + Height(node) - Height(here);
        settler.Frame = (byte)shape;
        var time = _tables.StepTime(shape);
        settler.Timer = time;
        settler.Target = (ushort)(time * _tables.DoorSpeeds[building.TypeIndex]) >> 5;
        settler.Stamp = _clock;
        settler.State = 4;
    }

    // The castle's 20 first settlers (0x442f0c with 0x44333d): all in the castle, in state 1 but the first, which stands by
    // while the castle is built. Their time stamp is the clock of the placement.
    private void CreateCastleSettlers(int x, int y)
    {
        _buildings.Add(new Building(Building.CastleType, x, y, 0, constructing: true) { Status = 0x50, Fill = 0xffff });
        foreach (var job in CastleJobs)
        {
            _settlers.Add(new Settler(job, 0, x, y, _clock, job == BuilderJob ? 10 : 1, 0));
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
        var flag = _flags.FindIndex(f => f.X == flagX && f.Y == flagY);
        var site = Castle is null ? default : BuildSite.Evaluate(_tables, Map, x, y, 0, hasCastle: true);
        // **Inferred:** a small hut needs the site of a small or a large building (3 or 4) and the flag already standing.
        if (flag < 0 || BuildingOfFlag(flag) is not null || site.Node != 5 || site.Site < 3)
        {
            return false;
        }

        var node = Map.Index(x, y);
        var slot = _buildings.FindIndex(building => building.IsFree);
        slot = slot < 0 ? _buildings.Count : slot;
        Map.Bytes(MapLayer.Flags)[node] |= 0x42;
        Map.Bytes(MapLayer.Flags)[Map.Index(flagX, flagY)] |= 0x10;
        Map.Bytes(MapLayer.Object)[node] = (byte)((Map.Layer(MapLayer.Object)[node] & 0x80) | 2);
        Map.Bytes(MapLayer.Resource)[node] = (byte)slot;
        var hut = new Building(Building.WoodcutterType, x, y, flag);
        if (slot == _buildings.Count)
        {
            _buildings.Add(hut);
        }
        else
        {
            _buildings[slot] = hut;
        }

        return true;
    }

    // The building a flag is tied to: the one that stands at it, not one that burns (0x4617fd takes the pointer from the flag's item).
    private Building? BuildingOfFlag(int flag) =>
        _buildings.FirstOrDefault(building => !building.IsFree && !building.IsDemolishing && building.Flag == flag);

    private void MixSettlers(ref ulong hash)
    {
        hash = Mix(Mix(Mix(Mix(hash, _clock), _phase), _scanCursor), _leaving);
        foreach (var building in _buildings)
        {
            foreach (
                var value in new[]
                {
                    building.TypeIndex,
                    building.X,
                    building.Y,
                    building.Flag,
                    building.Status,
                    building.Worker,
                    building.Progress,
                    building.Fill,
                    building.Stamp,
                }
            )
            {
                hash = Mix(hash, value);
            }
        }

        foreach (var settler in _settlers)
        {
            foreach (
                var value in new[]
                {
                    settler.Job,
                    settler.Player,
                    settler.Frame,
                    settler.Timer,
                    settler.X,
                    settler.Y,
                    settler.Stamp,
                    settler.State,
                    settler.Mode,
                    settler.Target,
                    settler.Heading,
                    settler.Next,
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

        foreach (var (index, slot) in _occupant.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)))
        {
            hash = Mix(Mix(hash, index), slot);
        }
    }
}
