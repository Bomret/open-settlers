using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The forester's work cycle (docs/rules/forestry.md), the woodcutter's with the node's own search and an arrival that plants: the
/// worker at his hut draws nodes of the first 128 around the hut until one is free meadow, walks there, waits, puts a sapling
/// down, and walks back to the hut's flag and into the hut. It is the rules module of the forester job (<see cref="JobRules{TJob}"/>):
/// the step out, the free walk, and the entry into the hut are <see cref="HutWork"/>'s, and so is the sapling's growth (the object pass).
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class ForesterRules(JobParts parts) : JobRules<Forester>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly IRandomDraws _draws = parts.Draws;
    readonly HutWork _hut = parts.Hut;

    // The time a node he may not plant on adds to the search's timer (0x438de7), where the woodcutter's adds 400.
    const int SpotMiss = 0x2bc;

    // The direction of the tile north-west of a node (the offset at +0x14 of the state block, 0x438d4a to 0x438d75).
    const int NorthWest = 4;

    // The time he waits on the node before the sapling goes down (0x4381de), and after it (0x438a6a).
    const int ArrivalWait = 0xbf;
    const int PlantWait = 0x80;

    Map Map => _movement.Map;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <inheritdoc />
    public override Cause Cause => Cause.Forestry;

    /// <summary>Registers the behaviour of the forester's search and planting.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<LooksForSpot>((slot, settler, _) => SearchSpot(slot, settler));
        registry.Register<Plants>((slot, settler, _) => Plant(slot, settler));
    }

    /// <summary>
    /// The start routine of job 8 (<c>0x43d009</c>), run where the forester has stepped through the door: he leaves the hut's node and
    /// looks for a node to plant on.
    /// </summary>
    public override void Start(int slot, Settler settler, Building building)
    {
        _movement.Release(Map.Index(settler.X, settler.Y));
        _movement.Become(settler, new LooksForSpot());
    }

    // A node he plants on: no road, no object, no mark of a lake or a large object, and the lowland terrain on both triangles of the
    // node's tile and of the tile north-west of it (0x438ce2 to 0x438d75).
    bool CanPlant(int x, int y)
    {
        ref var node = ref Map.At(x, y);
        if (!node.Roads.IsEmpty || node.LakeMark || node.Object.Exists || !IsMeadow(ref node))
        {
            return false;
        }

        var (dx, dy) = Direction.Steps[NorthWest];

        return IsMeadow(ref Map.At(x + dx, y + dy));
    }

    // A tile whose two triangles are both lowland (the terrain byte 0x55): the node and its north-west neighbour need it.
    static bool IsMeadow(ref Node node) => node.Upper == Terrain.Lowland && node.Lower == Terrain.Lowland;

    // State 0x13, the forester's search (0x438c9f). When its timer has run out he draws a node of the spiral around the hut, entries 1
    // to 128, until a node he may plant on comes up; each node he may not adds 0x2bc to the timer, and the search ends for this turn
    // when that wraps the timer.
    void SearchSpot(int slot, Settler settler)
    {
        var found = SearchSpotTurn(settler);
        _hut.RecordWork(slot, EventType.SearchSpot);
        if (found)
        {
            _hut.StepOut(slot, settler);
        }
    }

    // One turn of the search; true where he found a node and took state 7.
    bool SearchSpotTurn(Settler settler)
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
            if (CanPlant(x, y))
            {
                // The vector to the node is taken from the flag the first step reaches: one less each way.
                var way = new Trip
                {
                    Remaining = new Vector(dx - 1, dy - 1),
                    Purpose = new ToSpot { Back = new Vector(-(dx - 1), -(dy - 1)) },
                };
                settler.Activity = new StepsOntoFlag { Then = new WalksFree { Trip = way } };

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

    /// <summary>
    /// The walk's arrival of job 8 (0x438194). On the node he found he takes the way back as his vector and, where the node still
    /// holds no object, waits to plant (state 0x14, frame 0x79); else he turns back at once. At the flag on the way back he goes into
    /// the hut.
    /// </summary>
    public override void Arrive(int slot, Settler settler)
    {
        var trip = ((WalksFree)settler.Activity).Trip;
        if (trip.Purpose is Home)
        {
            // The walk home (0x4385ab).
            _hut.ArriveHome(slot, settler);

            return;
        }

        if (trip.Purpose is not ToSpot spot)
        {
            _hut.ReturnToHut(slot, settler, trip);

            return;
        }

        if (Map.At(settler.X, settler.Y).Object.Exists)
        {
            settler.Activity = new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = spot.Back,
                    Purpose = new BackToHut { Carrying = false },
                },
            };
            settler.Timer = 0;
            _hut.RecordWork(slot, EventType.Arrive);

            return;
        }

        settler.Activity = new Plants
        {
            Back = spot.Back,
            Planted = false,
            Memory = trip.Memory,
        };
        settler.Frame = 0x79;
        settler.Timer = ArrivalWait;
        _hut.RecordWork(slot, EventType.Arrive);
    }

    // State 0x14 (0x4389bf). When the timer has run out the first turn puts the sapling down: the code 0x67, or 0x68 by a draw's low
    // bit, on the node where no object and no road is, with the frame 0x7a, and waits 0x80 more units; the turn after that, or at once
    // where the wait does not wrap the timer, sends him back to the flag without a load.
    void Plant(int slot, Settler settler)
    {
        PlantTurn(settler);
        _hut.RecordWork(slot, EventType.PlantSapling, ThingId.OfObject("sapling", settler.X, settler.Y).ToString());
    }

    void PlantTurn(Settler settler)
    {
        if (!_movement.TimerRan(settler))
        {
            return;
        }

        var plants = (Plants)settler.Activity;
        if (!plants.Planted)
        {
            settler.Frame = 0x7a;
            var code = MapObject.SaplingCode + (_draws.Draw() & 1);
            ref var node = ref Map.At(settler.X, settler.Y);
            if (!node.Object.Exists && node.Roads.IsEmpty)
            {
                node.Object = new MapObject { Code = (byte)code };
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

        settler.Activity = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = plants.Back,
                Purpose = new BackToHut { Carrying = false },
            },
        };
        settler.Timer = 0;
    }
}
