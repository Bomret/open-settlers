using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// What the stonecutter's rules need of the stone heaps: the direction from a node to the node a heap stands on for a stonecutter who stands on it,
/// whether a heap stands on a node, and the check at each turn of his walk out.
/// </summary>
static class StoneHeaps
{
    /// <summary>The direction from a node to the node a heap stands on for a stonecutter who stands on it: north-west (0x435b9a, the state's offset +0x14).</summary>
    public const int Direction = 4;

    /// <summary>Whether a stone heap stands on the node.</summary>
    public static bool At(Map map, int index) => map.At(index).Object.Kind == MapObjectKind.StoneHeap;

    // State 0x16, at each turn of the stonecutter's walk out (0x436a84): where a node north-west of him holds a heap and no
    // settler stands on it, he is there: what is left of the vector joins the way back, the vector is used up, and the walk's
    // memory marks it so. The walk goes on from there as the free walk does. **Not ported:** the same check on the way back
    // after a cut that failed (state 0x16 too, with the vector of the way back).
    public static Trip Eye(Trip trip, bool heapBeside)
    {
        if (!heapBeside || trip.Purpose is not ToHeap heap)
        {
            return trip;
        }

        var back = new Vector(heap.Back.East + trip.Remaining.East, heap.Back.South + trip.Remaining.South);

        return new Trip
        {
            Remaining = new Vector(0, 0),
            Purpose = new ToHeap { Back = back },
            Memory = 8,
        };
    }
}

/// <summary>
/// The stonecutter's work cycle (docs/rules/stonecutting.md), the woodcutter's with the heap's own search, walk, and cut: the
/// worker at his hut draws nodes of the first 128 around the hut until a stone heap stands beside one, walks there, steps onto
/// the heap's node, cuts, steps back with a stone, lays it on the flag as good 10, and goes into the hut. It is the rules module of the
/// stonecutter job (<see cref="JobRules{TJob}"/>): the step out, the free walk, and the entry into the hut are <see cref="HutWork"/>'s.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class StonecutterRules(JobParts parts) : JobRules<Stonecutter>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly IRandomDraws _draws = parts.Draws;
    readonly HutWork _hut = parts.Hut;

    /// <summary>The good of a stone (the byte 10 the settler writes onto the flag, 0x438180).</summary>
    public const int GoodStone = 10;

    // The time a node without a heap adds to the search's timer (0x435c50), where the woodcutter's adds 400.
    const int HeapMiss = 100;

    // The direction from the heap's node back to the node the stonecutter came by: south-east.
    const int WayOut = 1;

    // The strokes: the stonecutter waits 0x600 units at the heap before the stone comes off (0x435cd3), and the step back
    // takes three quarters of the step's time (0x435e0d).
    const int StrokeTime = 0x600;

    Map Map => _movement.Map;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <inheritdoc />
    public override Cause Cause => Cause.Stonecutting;

    /// <summary>Registers the behaviour of the stonecutter's search and cut.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<LooksForStone>((slot, settler, _) => SearchStone(slot, settler));
        registry.Register<CutsStone>((slot, settler, _) => CutStone(slot, settler));
    }

    /// <summary>
    /// The start routine of job 7 (<c>0x43cfdd</c>), run where the stonecutter has stepped through the door: he leaves the hut's node
    /// and looks for a heap.
    /// </summary>
    public override void Start(int slot, Settler settler, Building building)
    {
        _movement.Release(Map.Index(settler.X, settler.Y));
        _movement.Become(settler, new LooksForStone());
    }

    /// <summary>On his walk out (state 0x16) he eyes the node north-west of him for a heap at each turn (<see cref="StoneHeaps.Eye"/>).</summary>
    public override void WalkTurn(Settler settler)
    {
        var walk = (WalksFree)settler.Activity;
        if (walk.EyesHeaps)
        {
            var heap = _movement.Neighbour(settler, StoneHeaps.Direction).Index;
            settler.Activity = walk with
            {
                Trip = StoneHeaps.Eye(walk.Trip, _movement.Occupant(heap) == 0 && StoneHeaps.At(Map, heap)),
            };
        }
    }

    // The node a heap stands on for a stonecutter at the node (x, y).
    int HeapOf(int x, int y)
    {
        var (dx, dy) = Direction.Steps[StoneHeaps.Direction];

        return Map.Index((x + dx) & (Map.Size.Width - 1), (y + dy) & (Map.Size.Height - 1));
    }

    // State 0x15, the stonecutter's search (0x435b17). When its timer has run out he draws a node of the spiral around the
    // hut, entries 1 to 128, until a node not marked solid has a heap north-west of it; each node without adds 100 to the
    // timer, and the search ends for this turn when that wraps the timer.
    void SearchStone(int slot, Settler settler)
    {
        var found = SearchStoneTurn(settler);
        _hut.RecordWork(slot, EventType.SearchStone);
        if (found)
        {
            _hut.StepOut(slot, settler);
        }
    }

    // One turn of the search; true where he found a heap and took state 7.
    bool SearchStoneTurn(Settler settler)
    {
        if (!_movement.TimerRan(settler))
        {
            return false;
        }

        while (true)
        {
            var entry = ((_draws.Draw() & 0x1fc) + 4) >> 2;
            var (dx, dy) = Spiral.Offsets[entry];
            var x = (settler.X + dx) & (Map.Size.Width - 1);
            var y = (settler.Y + dy) & (Map.Size.Height - 1);
            if (!Map.At(x, y).LakeMark && StoneHeaps.At(Map, HeapOf(x, y)))
            {
                // The vector to the node beside the heap is taken from the flag the first step reaches: one less each way.
                var way = new Trip
                {
                    Remaining = new Vector(dx - 1, dy - 1),
                    Purpose = new ToHeap { Back = new Vector(-(dx - 1), -(dy - 1)) },
                };
                settler.Activity = new StepsOntoFlag
                {
                    Then = new WalksFree { Trip = way, EyesHeaps = true },
                };

                return true;
            }

            var old = settler.Timer;
            settler.Timer = (ushort)(old + HeapMiss);
            if (old > 0xffff - HeapMiss)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The walk's arrival of job 7 (0x438025). On the node beside the heap, with the way back as the vector, he steps onto
    /// the heap's node where it still holds a heap and no settler, and takes state 0x17; else he turns back at once, with
    /// no stone. At the flag on the way back he lays the stone he carries onto the flag and goes into the hut.
    /// </summary>
    public override void Arrive(int slot, Settler settler)
    {
        var walk = (WalksFree)settler.Activity;
        var trip = walk.Trip;
        if (trip.Purpose is Home)
        {
            // The walk home (0x4385ab).
            _hut.ArriveHome(slot, settler);

            return;
        }

        if (trip.Purpose is not ToHeap heap)
        {
            if (trip.Purpose is BackToHut { Carrying: true })
            {
                _hut.PutGood(slot, settler, GoodStone, Cause);
            }

            _hut.ReturnToHut(slot, settler, trip);

            return;
        }

        var here = Map.Index(settler.X, settler.Y);
        var (x, y, node) = _movement.Neighbour(settler, StoneHeaps.Direction);
        if (_movement.Occupant(node) != 0 || !StoneHeaps.At(Map, node))
        {
            settler.Activity = new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = heap.Back,
                    Purpose = new BackToHut { Carrying = false },
                },
                EyesHeaps = walk.EyesHeaps,
            };
            settler.Timer = 0;
            _hut.RecordWork(slot, EventType.Arrive);

            return;
        }

        var shape = 0x28 + _movement.Height(node) - _movement.Height(here);
        _movement.Move(slot, settler, x, y, node);
        settler.Frame = (byte)shape;
        var time = _movement.Tables.StepTime(shape);
        settler.Timer = time;
        settler.Activity = new CutsStone
        {
            Back = heap.Back,
            Phase = 0,
            Quarter = time >> 2,
        };
        _hut.RecordWork(slot, EventType.Arrive);
    }

    // State 0x17, the cut (0x435c5c), in three parts. The step onto the heap's node (part 0) is waited out until a quarter of
    // its time is left; then the stonecutter strikes for 0x600 units (part 1, frame 0x7b); then the heap loses a stone and
    // he steps back onto the node he came by, taking three quarters of the step's time (part 2), and starts the way back with
    // the stone. Each part runs at once into the next where its time has already run out. A heap's last stone removes the heap
    // and its mark. He waits to step back while another settler stands on that node.
    void CutStone(int slot, Settler settler)
    {
        // The heap's node: his own until the step back, and the neighbour he stepped back from during part 2.
        var (heapX, heapY) = (settler.X, settler.Y);
        if (((CutsStone)settler.Activity).Phase == 2)
        {
            (heapX, heapY, _) = _movement.Neighbour(settler, StoneHeaps.Direction);
        }

        var heap = ThingId.OfObject("stone", heapX, heapY).ToString();
        CutStoneTurn(slot, settler);
        _hut.RecordWork(slot, EventType.CutStone, heap);
    }

    void CutStoneTurn(int slot, Settler settler)
    {
        var cut = (CutsStone)settler.Activity;
        var delta = (ushort)(_movement.Clock - settler.Stamp);
        settler.Stamp = _movement.Clock;
        if (cut.Phase == 0)
        {
            settler.Timer = (ushort)(settler.Timer - delta);
            if ((short)(ushort)(cut.Quarter - settler.Timer) < 0)
            {
                return;
            }

            settler.Timer = (ushort)(settler.Timer - (cut.Quarter + 1));
            cut = cut with { Phase = 1 };
            settler.Activity = cut;
            settler.Frame = 0x7b;
            if (Wraps(settler, StrokeTime))
            {
                return;
            }
        }
        else
        {
            var old = settler.Timer;
            settler.Timer = (ushort)(old - delta);
            if (delta <= old)
            {
                return;
            }
        }

        if (cut.Phase == 1)
        {
            var (x, y, back) = _movement.Neighbour(settler, WayOut);
            if (_movement.Occupant(back) != 0)
            {
                settler.Timer = 0;

                return;
            }

            var here = Map.Index(settler.X, settler.Y);
            ref var heap = ref Map.At(here);
            if (heap.Object.Code == MapObject.LastHeapCode)
            {
                heap.Object = MapObject.None;
                heap.LakeMark = false;
            }
            else
            {
                heap.Object = new MapObject { Code = (byte)(heap.Object.Code + 1) };
            }

            var shape = 0xd + _movement.Height(back) - _movement.Height(here);
            _movement.Move(slot, settler, x, y, back);
            settler.Frame = (byte)shape;
            var quarter = _movement.Tables.StepTime(shape) >> 2;
            settler.Stamp = _movement.Clock;
            cut = cut with { Phase = 2 };
            settler.Activity = cut;
            if (Wraps(settler, 3 * quarter))
            {
                return;
            }
        }

        settler.Activity = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = cut.Back,
                Purpose = new BackToHut { Carrying = true },
            },
        };
        settler.Timer = 0;
    }

    // Adds time to the settler's timer; true where the sum wraps it (the original's carry), so that the part waits it out.
    static bool Wraps(Settler settler, int time)
    {
        var old = settler.Timer;
        settler.Timer = (ushort)(old + time);

        return settler.Timer < old;
    }
}
