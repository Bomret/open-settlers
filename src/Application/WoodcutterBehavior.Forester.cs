using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

// The forester's work cycle (docs/rules/forestry.md), the woodcutter's with the node's own search and an arrival that plants: the
// worker at his hut draws nodes of the first 128 around the hut until one is free meadow, walks there, waits, puts a sapling
// down, and walks back to the hut's flag and into the hut. The step out, the free walk, the arrival at the flag, and the entry into
// the hut are the woodcutter's. The sapling's growth is the object pass's.
public sealed partial class WoodcutterBehavior
{
    // The time a node he may not plant on adds to the search's timer (0x438de7), where the woodcutter's adds 400.
    private const int SpotMiss = 0x2bc;

    // The terrain byte of a tile whose two triangles are both lowland (0x438d4a): the node and its north-west neighbour need it.
    private static readonly Terrain Meadow = Terrain.Lowland;

    // The time he waits on the node before the sapling goes down (0x4381de), and after it (0x438a6a).
    private const int ArrivalWait = 0xbf;
    private const int PlantWait = 0x80;

    /// <summary>
    /// The start routine of job 8 (<c>0x43d009</c>), run where the forester has stepped through the door: he leaves the hut's node and
    /// looks for a node to plant on.
    /// </summary>
    public void StartForester(int slot, Settler settler, Building hut)
    {
        movement.Release(Map.Index(settler.X, settler.Y));
        movement.Become(settler, new LooksForSpot());
    }

    // A node he plants on: no road, no object, no mark of a lake or a large object, and the lowland terrain on both triangles of the
    // node's tile and of the tile north-west of it (0x438ce2 to 0x438d75).
    private bool CanPlant(int x, int y)
    {
        ref var node = ref Map.At(x, y);
        if (!node.Roads.IsEmpty || node.LakeMark || node.Object.Exists || !IsMeadow(ref node))
        {
            return false;
        }

        var (dx, dy) = Direction.Steps[HeapDirection];

        return IsMeadow(ref Map.At(x + dx, y + dy));
    }

    private static bool IsMeadow(ref Node node) => node.Upper == Meadow && node.Lower == Meadow;

    // State 0x13, the forester's search (0x438c9f). When its timer has run out he draws a node of the spiral around the hut, entries 1
    // to 128, until a node he may plant on comes up; each node he may not adds 0x2bc to the timer, and the search ends for this turn
    // when that wraps the timer.
    private void SearchSpot(int slot, Settler settler)
    {
        var found = SearchSpotTurn(settler);
        RecordWork(slot, EventType.SearchSpot);
        if (found)
        {
            StepOut(slot, settler);
        }
    }

    // One turn of the search; true where he found a node and took state 7.
    private bool SearchSpotTurn(Settler settler)
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
            if (CanPlant(x, y))
            {
                // The vector to the node is taken from the flag the first step reaches: one less each way.
                var way = new Trip(new Vector(dx - 1, dy - 1), new ToSpot(new Vector(-(dx - 1), -(dy - 1))));
                settler.Activity = new StepsOntoFlag(new WalksFree(way));

                return true;
            }

            var old = settler.Timer;
            settler.Timer = (ushort)(old + SpotMiss);
            if (old > 0xffff - SpotMiss)
            {
                return false;
            }
        }
    }

    // The walk's arrival of job 8 (0x438194). On the node he found he takes the way back as his vector and, where the node still
    // holds no object, waits to plant (state 0x14, frame 0x79); else he turns back at once. At the flag on the way back he goes into
    // the hut.
    private void ArriveForester(int slot, Settler settler)
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

        if (trip.Purpose is not ToSpot spot)
        {
            // The vector's east steps are the byte the entry into the hut sets to 0.
            settler.Activity = new WalksFree(trip with { Remaining = new Vector(0, trip.Remaining.South) });
            EnterHut(slot, settler);

            return;
        }

        if (Map.At(settler.X, settler.Y).Object.Exists)
        {
            settler.Activity = new WalksFree(new Trip(spot.Back, new BackToHut(false)));
            settler.Timer = 0;
            RecordWork(slot, EventType.Arrive);

            return;
        }

        settler.Activity = new Plants(spot.Back, false, trip.Memory);
        settler.Frame = 0x79;
        settler.Timer = ArrivalWait;
        RecordWork(slot, EventType.Arrive);
    }

    // State 0x14 (0x4389bf). When the timer has run out the first turn puts the sapling down: the code 0x67, or 0x68 by a draw's low
    // bit, on the node where no object and no road is, with the frame 0x7a, and waits 0x80 more units; the turn after that, or at once
    // where the wait does not wrap the timer, sends him back to the flag without a load.
    private void Plant(int slot, Settler settler)
    {
        PlantTurn(settler);
        RecordWork(slot, EventType.PlantSapling, ThingId.OfObject("sapling", settler.X, settler.Y).ToString());
    }

    private void PlantTurn(Settler settler)
    {
        if (!movement.TimerRan(settler))
        {
            return;
        }

        var plants = (Plants)settler.Activity;
        if (!plants.Planted)
        {
            settler.Frame = 0x7a;
            var code = MapObject.SaplingCode + (draws.Draw() & 1);
            ref var node = ref Map.At(settler.X, settler.Y);
            if (!node.Object.Exists && node.Roads.IsEmpty)
            {
                node.Object = new MapObject((byte)code);
            }

            plants = plants with { Planted = true };
            settler.Activity = plants;
            var old = settler.Timer;
            settler.Timer = (ushort)(old + PlantWait);
            if (settler.Timer < old)
            {
                return;
            }
        }

        settler.Activity = new WalksFree(new Trip(plants.Back, new BackToHut(false)));
        settler.Timer = 0;
    }

    // The object pass's handler of the saplings (0x4290d5, 0x42910f): one draw; where its bits 8 and 9 are clear the sapling is
    // a tree of its own range, picked by the draw's low three bits, and keeps the node's water mark.
    private void GrowSapling(ref Node node)
    {
        var draw = draws.Draw();
        if ((draw & 0x300) == 0)
        {
            node.Object = MapObject.Grown(node.Object, draw);
        }
    }
}
