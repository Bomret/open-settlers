using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The woodcutter's work cycle (docs/rules/woodcutting.md): the worker at his hut picks a node of the first 128 around the hut with a
/// random draw until a tree stands on it, walks there, cuts the tree, walks back to the hut's flag with a log, lays the log on the flag,
/// and goes into the hut to start again. It is the rules module of the woodcutter job (<see cref="JobRules{TJob}"/>): the door, the step
/// out, the free walk, and the entry into the hut are <see cref="HutWork"/>'s. The map's object pass (<see cref="HutWork.ObjectPass"/>)
/// turns a felled tree into nothing in the time the original takes. The trees are the object codes 8 to 0x17 (0x438c11, 0x437f8f;
/// <see cref="MapObjectKind.Tree"/>), and the class table calls them class 1 (docs/rules/build-sites.md). The trees of the codes 8 to 15
/// take the second cutting variant (the animation with the byte 13 set).
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class WoodcutterRules(JobParts parts) : JobRules<Woodcutter>(parts)
{
    private readonly SettlerMovement movement = parts.Movement;
    private readonly IRandomDraws draws = parts.Draws;
    private readonly HutWork hut = parts.Hut;

    /// <summary>The good of a log (the byte 7 the settler writes onto the flag, 0x438011).</summary>
    public const int GoodLog = 7;

    private Map Map => movement.Map;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <inheritdoc />
    public override Cause Cause => Cause.Woodcutting;

    /// <summary>Registers the behaviour of the woodcutter's search and cut.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<Cuts>((slot, settler, _) => Cut(slot, settler));
        registry.Register<LooksForTree>((slot, settler, _) => SearchTree(slot, settler));
    }

    /// <summary>0x43cf15: the woodcutter leaves the hut's node and looks for a tree.</summary>
    public override void Start(int slot, Settler settler, Building building)
    {
        movement.Release(Map.Index(settler.X, settler.Y));
        movement.Become(settler, new LooksForTree());
    }

    // State 0x12, the woodcutter's search (0x438b8c). When its timer has run out it draws a node of the spiral around the
    // hut, entries 1 to 128, until a tree stands on one; each node without a tree adds 400 to the timer, and the search
    // ends for this turn when that wraps the timer.
    private void SearchTree(int slot, Settler settler)
    {
        var found = SearchTurn(settler);
        hut.RecordWork(slot, EventType.SearchTree);
        if (found)
        {
            hut.StepOut(slot, settler);
        }
    }

    // One turn of the search; true where it found a tree and took state 7.
    private bool SearchTurn(Settler settler)
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
            if (Map.At(x, y).Object.Kind == MapObjectKind.Tree)
            {
                // The vector to the tree is taken from the flag the first step reaches: one less each way. The way back is
                // the same vector turned round.
                var way = new Trip(new Vector(dx - 1, dy - 1), new ToTree(new Vector(-(dx - 1), -(dy - 1))));
                settler.Activity = new StepsOntoFlag(new WalksFree(way));

                return true;
            }

            var old = settler.Timer;
            settler.Timer = (ushort)(old + 400);
            if (old > 0xfe6f)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The walk has used up its vector (0x437ddd, the woodcutter's branch at 0x437f53). On the tree's node the settler takes
    /// the way back as its vector and starts cutting where the tree still stands; else it turns back at once. At the
    /// flag on the way back it lays the log it carries onto the flag and goes into the hut.
    /// </summary>
    public override void Arrive(int slot, Settler settler)
    {
        var trip = ((WalksFree)settler.Activity).Trip;
        if (trip.Purpose is not ToTree tree)
        {
            // The walk home (0x4385ab).
            if (trip.Purpose is Home)
            {
                hut.ArriveHome(slot, settler);

                return;
            }

            if (trip.Purpose is BackToHut { Carrying: true })
            {
                hut.PutGood(slot, settler, GoodLog, Cause);
            }

            hut.ReturnToHut(slot, settler, trip);

            return;
        }

        var standing = Map.At(settler.X, settler.Y).Object;
        if (standing.Kind == MapObjectKind.Tree)
        {
            settler.Activity = new Cuts(tree.Back, standing.HasSecondCutVariant, 0, trip.Memory);
            settler.Frame = 0x74;
            settler.Timer = 0x3ff;
            hut.RecordWork(slot, EventType.Arrive);

            return;
        }

        settler.Activity = new WalksFree(new Trip(tree.Back, new BackToHut(false)));
        settler.Timer = 0;
        hut.RecordWork(slot, EventType.Arrive);
    }

    // State 0x11, the cut (0x438a9e): each time the timer runs out the tree takes the next of its falling codes (0x5d to
    // 0x66, the second variant after 0x61 for the codes 8 to 15), the frame is 0x74 plus the count and the timer takes
    // the time that frame's table word gives. After the fifth the settler starts the way back with the log.
    private void Cut(int slot, Settler settler)
    {
        var tree = ThingId.OfObject("tree", settler.X, settler.Y).ToString();
        CutTurn(settler);
        hut.RecordWork(slot, EventType.CutTree, tree);
    }

    private void CutTurn(Settler settler)
    {
        if (!movement.TimerRan(settler))
        {
            return;
        }

        var cut = (Cuts)settler.Activity;
        ref var node = ref Map.At(settler.X, settler.Y);
        while (true)
        {
            var count = (sbyte)(cut.Count + 1);
            cut = cut with { Count = count };
            settler.Activity = cut;
            node.Object = MapObject.Falling(count, cut.SecondVariant);
            if (count == 5)
            {
                break;
            }

            settler.Frame = (byte)(count + 0x74);
            var old = settler.Timer;
            settler.Timer = (ushort)(old + movement.Tables.StepTime(settler.Frame));
            if (settler.Timer < old)
            {
                return;
            }
        }

        settler.Activity = new WalksFree(new Trip(cut.Back, new BackToHut(true)));
        settler.Timer = 0;
    }
}
