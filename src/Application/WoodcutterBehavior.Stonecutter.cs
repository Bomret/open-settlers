using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

// The stonecutter's work cycle (docs/rules/stonecutting.md), the woodcutter's with the heap's own search, walk, and cut: the
// worker at his hut draws nodes of the first 128 around the hut until a stone heap stands beside one, walks there, steps onto
// the heap's node, cuts, steps back with a stone, lays it on the flag as good 10, and goes into the hut. The step out, the free
// walk, the arrival at the flag, and the entry into the hut are the woodcutter's.
public sealed partial class WoodcutterBehavior
{
    /// <summary>The good of a stone (the byte 10 the settler writes onto the flag, 0x438180).</summary>
    public const int GoodStone = 10;

    // The time a node without a heap adds to the search's timer (0x435c50), where the woodcutter's adds 400.
    private const int HeapMiss = 100;

    // The direction from a node to the node a heap stands on for a stonecutter who stands on it: north-west (0x435b9a, the
    // state's offset +0x14).
    private const int HeapDirection = 4;

    // The direction from the heap's node back to the node the stonecutter came by: south-east.
    private const int WayOut = 1;

    // The strokes: the stonecutter waits 0x600 units at the heap before the stone comes off (0x435cd3), and the step back
    // takes three quarters of the step's time (0x435e0d).
    private const int StrokeTime = 0x600;

    /// <summary>
    /// The start routine of job 7 (<c>0x43cfdd</c>), run where the stonecutter has stepped through the door: he leaves the hut's node
    /// and looks for a heap.
    /// </summary>
    public void StartStonecutter(int slot, Settler settler, Building hut)
    {
        movement.Release(Map.Index(settler.X, settler.Y));
        movement.Become(settler, new LooksForStone());
    }

    // The node a heap stands on for a stonecutter at the node (x, y).
    private int HeapOf(int x, int y)
    {
        var (dx, dy) = Direction.Steps[HeapDirection];

        return Map.Index((x + dx) & (Map.Size.Width - 1), (y + dy) & (Map.Size.Height - 1));
    }

    private bool HeapAt(int index) => Map.At(index).Object.Kind == MapObjectKind.StoneHeap;

    // State 0x15, the stonecutter's search (0x435b17). When its timer has run out he draws a node of the spiral around the
    // hut, entries 1 to 128, until a node not marked solid has a heap north-west of it; each node without adds 100 to the
    // timer, and the search ends for this turn when that wraps the timer.
    private void SearchStone(int slot, Settler settler)
    {
        var found = SearchStoneTurn(settler);
        RecordWork(slot, EventType.SearchStone);
        if (found)
        {
            StepOut(slot, settler);
        }
    }

    // One turn of the search; true where he found a heap and took state 7.
    private bool SearchStoneTurn(Settler settler)
    {
        if (!movement.TimerRan(settler))
        {
            return false;
        }

        while (true)
        {
            var entry = ((draws.Draw() & 0x1fc) + 4) >> 2;
            var (dx, dy) = Spiral.Offsets[entry];
            var x = (settler.X + dx) & (Map.Size.Width - 1);
            var y = (settler.Y + dy) & (Map.Size.Height - 1);
            if (!Map.At(x, y).LakeMark && HeapAt(HeapOf(x, y)))
            {
                // The vector to the node beside the heap is taken from the flag the first step reaches: one less each way.
                var way = new Trip(new Vector(dx - 1, dy - 1), new ToHeap(new Vector(-(dx - 1), -(dy - 1))));
                settler.Activity = new StepsOntoFlag(new WalksFree(way, EyesHeaps: true));

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

    // State 0x16, at each turn of the stonecutter's walk out (0x436a84): where a node north-west of him holds a heap and no
    // settler stands on it, he is there: what is left of the vector joins the way back, the vector is used up, and the walk's
    // memory marks it so. The walk goes on from there as the free walk does. **Not ported:** the same check on the way back
    // after a cut that failed (state 0x16 too, with the vector of the way back).
    private static Trip EyeHeaps(Trip trip, bool heapBeside)
    {
        if (!heapBeside || trip.Purpose is not ToHeap heap)
        {
            return trip;
        }

        var back = new Vector(heap.Back.East + trip.Remaining.East, heap.Back.South + trip.Remaining.South);

        return new Trip(new Vector(0, 0), new ToHeap(back), 8);
    }

    // The walk's arrival of job 7 (0x438025). On the node beside the heap, with the way back as the vector, he steps onto
    // the heap's node where it still holds a heap and no settler, and takes state 0x17; else he turns back at once, with
    // no stone. At the flag on the way back he lays the stone he carries onto the flag and goes into the hut.
    private void ArriveStonecutter(int slot, Settler settler)
    {
        var walk = (WalksFree)settler.Activity;
        var trip = walk.Trip;
        if (trip.Purpose is Home)
        {
            // The walk home (0x4385ab).
            walkHome.ArriveHome(slot, settler);
            RecordWork(slot, EventType.Arrive);

            return;
        }

        if (trip.Purpose is not ToHeap heap)
        {
            if (trip.Purpose is BackToHut { Carrying: true })
            {
                PutGood(slot, settler, GoodStone, Cause.Stonecutting);
            }

            // The vector's east steps are the byte the entry into the hut sets to 0.
            settler.Activity = new WalksFree(trip with { Remaining = new Vector(0, trip.Remaining.South) });
            EnterHut(slot, settler);

            return;
        }

        var here = Map.Index(settler.X, settler.Y);
        var (x, y, node) = movement.Neighbour(settler, HeapDirection);
        if (movement.Occupant(node) != 0 || !HeapAt(node))
        {
            settler.Activity = new WalksFree(new Trip(heap.Back, new BackToHut(false)), walk.EyesHeaps);
            settler.Timer = 0;
            RecordWork(slot, EventType.Arrive);

            return;
        }

        var shape = 0x28 + movement.Height(node) - movement.Height(here);
        movement.Move(slot, settler, x, y, node);
        settler.Frame = (byte)shape;
        var time = movement.Tables.StepTime(shape);
        settler.Timer = time;
        settler.Activity = new CutsStone(heap.Back, 0, time >> 2);
        RecordWork(slot, EventType.Arrive);
    }

    // State 0x17, the cut (0x435c5c), in three parts. The step onto the heap's node (part 0) is waited out until a quarter of
    // its time is left; then the stonecutter strikes for 0x600 units (part 1, frame 0x7b); then the heap loses a stone and
    // he steps back onto the node he came by, taking three quarters of the step's time (part 2), and starts the way back with
    // the stone. Each part runs at once into the next where its time has already run out. A heap's last stone removes the heap
    // and its mark. He waits to step back while another settler stands on that node.
    private void CutStone(int slot, Settler settler)
    {
        var heap = ThingId.OfObject("stone", settler.X, settler.Y).ToString();
        CutStoneTurn(slot, settler);
        RecordWork(slot, EventType.CutStone, heap);
    }

    private void CutStoneTurn(int slot, Settler settler)
    {
        var cut = (CutsStone)settler.Activity;
        var delta = (ushort)(movement.Clock - settler.Stamp);
        settler.Stamp = movement.Clock;
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
            var (x, y, back) = movement.Neighbour(settler, WayOut);
            if (movement.Occupant(back) != 0)
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
                heap.Object = new MapObject((byte)(heap.Object.Code + 1));
            }

            var shape = 0xd + movement.Height(back) - movement.Height(here);
            movement.Move(slot, settler, x, y, back);
            settler.Frame = (byte)shape;
            var quarter = movement.Tables.StepTime(shape) >> 2;
            settler.Stamp = movement.Clock;
            cut = cut with { Phase = 2 };
            settler.Activity = cut;
            if (Wraps(settler, (ushort)(quarter + 2 * quarter)))
            {
                return;
            }
        }

        settler.Activity = new WalksFree(new Trip(cut.Back, new BackToHut(true)));
        settler.Timer = 0;
    }

    // Adds time to the settler's timer; true where the sum wraps it (the original's carry), so that the part waits it out.
    private static bool Wraps(Settler settler, int time)
    {
        var old = settler.Timer;
        settler.Timer = (ushort)(old + time);

        return settler.Timer < old;
    }
}
