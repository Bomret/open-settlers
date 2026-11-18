using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The farmer's work cycle (docs/rules/farming.md): the worker at his farm draws nodes of the entries 7 to 38 of the spiral around the
/// farm until one holds a field he may harvest or is a free meadow node he may sow, walks there, waits, sows or harvests, and walks
/// back to the farm's flag, where he lays the grain he took on the flag and goes into the farm. It is the rules module of the farmer
/// job (<see cref="JobRules{TJob}"/>): the step out, the free walk, and the entry into the farm are <see cref="HutWork"/>'s, and so
/// is the fields' growth (the object pass).
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class FarmerRules(JobParts parts) : JobRules<Farmer>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly IRandomDraws _draws = parts.Draws;
    readonly HutWork _hut = parts.Hut;

    // The time a node he may neither sow nor harvest adds to the search's timer (0x433df5).
    const int NodeMiss = 0x1f4;

    // The time he waits on the node before he sows (0x438411) and before he harvests (0x438426).
    const int SowWait = 0x5bf;
    const int HarvestWait = 0x7bf;

    Map Map => _movement.Map;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <inheritdoc />
    public override Cause Cause => Cause.Farming;

    /// <summary>The farmer's search: spiral entries 7 to 38, a field he may harvest, or a node with no object he may sow.</summary>
    public override JobSearch Search { get; } =
        new()
        {
            First = 7,
            Count = 32,
            Accepts = (map, x, y) =>
                map.At(x, y).Object is { Exists: true } standing ? standing.IsHarvestable : CanSow(map, x, y),
        };

    /// <summary>Registers the behaviour of the farmer's search and his work on a node.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<LooksForField>((slot, settler, _) => SearchNode(slot, settler));
        registry.Register<TendsField>((slot, settler, _) => Tend(slot, settler));
    }

    /// <summary>
    /// The start routine of job 14 (<c>0x43d311</c>), run where the farmer has stepped through the door: he leaves the farm's node and
    /// looks for a node to sow or a field to harvest.
    /// </summary>
    public override void Start(int slot, Settler settler, Building building)
    {
        _movement.Release(Map.Index(settler.X, settler.Y));
        _movement.Become(settler, new LooksForField());
    }

    // The object code of the node at the offset from (x, y), where the original reads "large building" for the codes 3 and 4.
    static bool BuildingAt(Map map, int x, int y, int dx, int dy) => map.At(x + dx, y + dy).Object.Code is 3 or 4;

    // A node he sows (0x433bb4 to 0x433d6b): no object, no road, lowland on both triangles of its tile, no large building on the six
    // nodes around it, and meadow on the triangles around it: the lower triangle of the tile west of it, both of the tile north-west
    // of it, and the upper triangle of the tile north of it.
    static bool CanSow(Map map, int x, int y)
    {
        ref var node = ref map.At(x, y);
        if (node.Object.Exists || !node.Roads.IsEmpty || node.Upper != Terrain.Lowland || node.Lower != Terrain.Lowland)
        {
            return false;
        }

        foreach (var (dx, dy) in Direction.Steps)
        {
            if (BuildingAt(map, x, y, dx, dy))
            {
                return false;
            }
        }

        return map.At(x - 1, y).Lower == Terrain.Lowland
            && map.At(x - 1, y - 1).Upper == Terrain.Lowland
            && map.At(x - 1, y - 1).Lower == Terrain.Lowland
            && map.At(x, y - 1).Upper == Terrain.Lowland;
    }

    // State 0x21, the farmer's search (0x433b35). When its timer has run out he draws a node of the spiral around the farm, entries 7 to
    // 38, until a node he may harvest or sow comes up; each node he may not adds 0x1f4 to the timer, and the search ends for this turn
    // when that wraps the timer.
    void SearchNode(int slot, Settler settler)
    {
        var found = SearchNodeTurn(settler);
        _hut.RecordWork(slot, EventType.SearchField);
        if (found)
        {
            _hut.StepOut(slot, settler);
        }
    }

    // One turn of the search; true where he found a node and took state 7.
    bool SearchNodeTurn(Settler settler)
    {
        if (!_movement.TimerRan(settler))
        {
            return false;
        }

        while (true)
        {
            var entry = Search.Entry(_draws.Draw());
            var (dx, dy) = Spiral.Offsets[entry];
            var x = (settler.X + dx) & (Map.Size.Width - 1);
            var y = (settler.Y + dy) & (Map.Size.Height - 1);
            if (Search.Accepts(Map, x, y))
            {
                // The vector to the node is taken from the flag the first step reaches: one less each way.
                var way = new Trip
                {
                    Remaining = new Vector(dx - 1, dy - 1),
                    Purpose = new ToField { Back = new Vector(-(dx - 1), -(dy - 1)) },
                };
                settler.Activity = new StepsOntoFlag { Then = new WalksFree { Trip = way } };

                return true;
            }

            var old = settler.Timer;
            settler.Timer = (ushort)(old + NodeMiss);
            if (old > 0xffff - NodeMiss)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The walk's arrival of job 14 (0x438391). On the node he found he takes the way back as his vector and, where a field he may harvest
    /// stands, waits to harvest (state 0x22, frame 0x88); where the node holds no object and no road, waits to sow (frame 0x87); else he
    /// turns back at once. At the flag on the way back he lays the grain he carries on it and goes into the farm.
    /// </summary>
    public override void Arrive(int slot, Settler settler)
    {
        var trip = ((WalksFree)settler.Activity).Trip;
        if (trip.Purpose is not ToField field)
        {
            if (trip.Purpose is Home)
            {
                // The walk home (0x4385ab).
                _hut.ArriveHome(slot, settler);

                return;
            }

            if (trip.Purpose is BackToHut { Carrying: true })
            {
                _hut.PutGood(slot, settler, Good.Grain.Code, Cause);
            }

            _hut.ReturnToHut(slot, settler, trip);

            return;
        }

        var standing = Map.At(settler.X, settler.Y);
        if (standing.Object.IsHarvestable || (!standing.Object.Exists && standing.Roads.IsEmpty))
        {
            var harvests = standing.Object.IsHarvestable;
            settler.Activity = new TendsField
            {
                Back = field.Back,
                Harvests = harvests,
                Memory = trip.Memory,
            };
            settler.Frame = harvests ? (byte)0x88 : (byte)0x87;
            settler.Timer = harvests ? (ushort)HarvestWait : (ushort)SowWait;
            _hut.RecordWork(slot, EventType.Arrive);

            return;
        }

        settler.Activity = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = field.Back,
                Purpose = new BackToHut { Carrying = false },
            },
        };
        settler.Timer = 0;
        _hut.RecordWork(slot, EventType.Arrive);
    }

    // State 0x22 (0x433a3a): when the timer has run out he harvests, which takes the field one stage on and leaves the grain in his hands,
    // or sows, which puts the first growing field on a node that still holds no object and no road; then he walks back.
    void Tend(int slot, Settler settler)
    {
        var field = ThingId.OfObject("field", settler.X, settler.Y).ToString();
        TendTurn(settler);
        _hut.RecordWork(slot, EventType.TendField, field);
    }

    void TendTurn(Settler settler)
    {
        if (!_movement.TimerRan(settler))
        {
            return;
        }

        var tends = (TendsField)settler.Activity;
        ref var node = ref Map.At(settler.X, settler.Y);
        if (tends.Harvests)
        {
            node.Object = node.Object.Harvested();
        }
        else if (!node.Object.Exists && node.Roads.IsEmpty)
        {
            node.Object = new MapObject { Code = MapObject.SownFieldCode };
        }

        settler.Activity = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = tends.Back,
                Purpose = new BackToHut { Carrying = tends.Harvests },
            },
        };
        settler.Timer = 0;
    }
}
