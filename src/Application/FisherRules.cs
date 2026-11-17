using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The fisher's work cycle (<c>docs/rules/fishing.md</c>): the worker at his building draws a node of the first 64 around it until one
/// stands free at a shore, walks there, casts for fish up to ten times at the lake detail beside him, walks back to the building's
/// flag, lays a fish on the flag if he caught one, and goes into the building to start again. It is the rules module of the fisher
/// (<see cref="JobRules{TJob}"/>): the door, the step out, the free walk, and the entry into the building are <see cref="HutWork"/>'s,
/// and the object pass (<see cref="HutWork.ObjectPass"/>) moves the lake details he fishes.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class FisherRules(JobParts parts) : JobRules<Fisher>(parts)
{
    // The figure bytes of the two casts (the animation frames 0x83 and 0x84): the cast to the west side and the cast to the east side.
    const int WestCast = 0x83;
    const int EastCast = 0x84;

    // A fisher turns back after ten casts (0x433e12 compares byte 15 with 10).
    const int CastsInAll = 10;

    readonly SettlerMovement _movement = parts.Movement;
    readonly IRandomDraws _draws = parts.Draws;
    readonly HutWork _hut = parts.Hut;

    Map Map => _movement.Map;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <inheritdoc />
    public override Cause Cause => Cause.Fishing;

    /// <summary>Registers the behaviour of the fisher's search and cast.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<Fishes>((slot, settler, _) => Fish(slot, settler));
        registry.Register<LooksForWater>((slot, settler, _) => SearchWater(slot, settler));
    }

    /// <summary>0x43d14d: the fisher leaves the building's node and looks for a node at a shore.</summary>
    public override void Start(int slot, Settler settler, Building building)
    {
        _movement.Release(Map.Index(settler.X, settler.Y));
        _movement.Become(settler, new LooksForWater());
    }

    // State 0x1f, the fisher's search (0x433f71). When its timer has run out it draws a node of the spiral around the building,
    // entries 1 to 64, until a free node at a shore comes up; each node that is not one adds 100 to the timer, and the search ends
    // for this turn when that wraps the timer.
    void SearchWater(int slot, Settler settler)
    {
        var found = SearchTurn(settler);
        _hut.RecordWork(slot, EventType.SearchWater);
        if (found)
        {
            _hut.StepOut(slot, settler);
        }
    }

    // One turn of the search; true where it found a node and took state 7.
    bool SearchTurn(Settler settler)
    {
        if (!_movement.TimerRan(settler))
        {
            return false;
        }

        while (true)
        {
            var entry = ((_draws.Draw() & 0xfc) + 4) >> 2;
            var (dx, dy) = Spiral.Offsets[entry];
            var x = (settler.X + dx) & (Map.Size.Width - 1);
            var y = (settler.Y + dy) & (Map.Size.Height - 1);
            if (!Map.At(x, y).Object.Exists && Map.At(x, y).Roads.IsEmpty && IsShore(x, y) != 0)
            {
                // The vector to the node is taken from the flag the first step reaches: one less each way. The way back is the
                // same vector turned round.
                var way = new Trip
                {
                    Remaining = new Vector(dx - 1, dy - 1),
                    Purpose = new ToWater { Back = new Vector(-(dx - 1), -(dy - 1)) },
                };
                settler.Activity = new StepsOntoFlag { Then = new WalksFree { Trip = way } };

                return true;
            }

            var old = settler.Timer;
            settler.Timer = (ushort)(old + 100);
            if (old > 0xff9b)
            {
                return false;
            }
        }
    }

    // What the node at (x, y) is as a shore: the frame of the cast that fits it, 0 for none. The east cast (0x84) fits a node whose
    // lower triangle is water and whose north-west neighbour's upper triangle is not (0x434022); the west cast (0x83) one whose west
    // neighbour's lower triangle is water and whose north neighbour's upper triangle is not (0x434056). The first that fits wins.
    int IsShore(int x, int y)
    {
        if (Map.At(x, y).Lower.IsWater && !Map.At(x - 1, y - 1).Upper.IsWater)
        {
            return EastCast;
        }

        return Map.At(x - 1, y).Lower.IsWater && !Map.At(x, y - 1).Upper.IsWater ? WestCast : 0;
    }

    /// <summary>
    /// The walk has used up its vector (0x437ddd, the fisher's branch 0x437e53 to 0x438232). At the node he found, he starts casting
    /// where no road crosses it and the node is still a shore; else he turns back at once. At the flag on the way back he lays the
    /// fish he carries onto the flag and goes into the building.
    /// </summary>
    public override void Arrive(int slot, Settler settler)
    {
        var trip = ((WalksFree)settler.Activity).Trip;
        if (trip.Purpose is not ToWater water)
        {
            // The walk home (0x438244, the generic arrival).
            if (trip.Purpose is Home)
            {
                _hut.ArriveHome(slot, settler);

                return;
            }

            if (trip.Purpose is BackToHut { Carrying: true })
            {
                _hut.PutGood(slot, settler, Good.Fish, Cause);
            }

            _hut.ReturnToHut(slot, settler, trip);

            return;
        }

        var cast = Map.At(settler.X, settler.Y).Roads.IsEmpty ? IsShore(settler.X, settler.Y) : 0;
        if (cast != 0)
        {
            settler.Activity = new Fishes
            {
                Back = water.Back,
                Turns = 0,
                Caught = false,
                Casts = 0,
            };
            settler.Frame = (byte)cast;
            settler.Timer = 0x2ff;
            _hut.RecordWork(slot, EventType.Arrive);

            return;
        }

        settler.Activity = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = water.Back,
                Purpose = new BackToHut { Carrying = false },
            },
        };
        settler.Timer = 0;
        _hut.RecordWork(slot, EventType.Arrive);
    }

    void Fish(int slot, Settler settler)
    {
        FishTurn(settler);
        _hut.RecordWork(slot, EventType.Fish);
    }

    // State 0x20, the cast (0x433e05). Each time the timer runs out the fisher takes turns: the fisher who has a catch or has made
    // ten casts turns back with the way back as his vector. An even turn takes two off the frame and adds 0x300 to the timer; an odd
    // turn is a cast: it draws against the lake detail beside him, and where the detail is not 0 and the draw's low six bits are
    // below the detail plus 4 the detail falls by one and the fish is his; it adds 2 to the frame and 0x80 to the timer. A turn that
    // wraps the timer ends the pass.
    void FishTurn(Settler settler)
    {
        if (!_movement.TimerRan(settler))
        {
            return;
        }

        var fishing = (Fishes)settler.Activity;
        while (true)
        {
            if (fishing.Caught || fishing.Casts == CastsInAll)
            {
                settler.Activity = new WalksFree
                {
                    Trip = new Trip
                    {
                        Remaining = fishing.Back,
                        Purpose = new BackToHut { Carrying = fishing.Caught },
                    },
                };
                settler.Timer = 0;

                return;
            }

            fishing = fishing with { Turns = (fishing.Turns + 1) & 0xff };
            settler.Activity = fishing;
            var old = settler.Timer;
            if ((fishing.Turns & 1) == 0)
            {
                settler.Frame -= 2;
                settler.Timer = (ushort)(old + 0x300);
                if (settler.Timer < old)
                {
                    return;
                }

                continue;
            }

            fishing = fishing with { Casts = fishing.Casts + 1, Caught = Cast(settler) };
            settler.Activity = fishing;
            settler.Frame += 2;
            settler.Timer = (ushort)(old + 0x80);
            if (settler.Timer < old)
            {
                return;
            }
        }
    }

    // One cast: the water node beside the fisher is the west neighbour for the west cast and the east neighbour for the east cast, or
    // the node below it where that is no lake node (0x433e47, 0x433e72). A lake detail of 0 catches nothing and draws nothing; with
    // a detail, the draw's low six bits below the detail plus 4 (a byte) catch a fish and lower the detail by one.
    bool Cast(Settler settler)
    {
        var (x, y) = settler.Frame == WestCast ? (settler.X - 1, settler.Y) : (settler.X + 1, settler.Y);
        if (!Map.At(x, y).LakeMark)
        {
            (x, y) = settler.Frame == WestCast ? (x + 1, y + 1) : (x, y + 1);
        }

        ref var water = ref Map.At(x, y);
        var detail = water.LakeDetail;
        if (detail == 0 || (_draws.Draw() & 0x3f) >= (byte)(detail + 4))
        {
            return false;
        }

        water.LakeDetail = detail - 1;

        return true;
    }
}
