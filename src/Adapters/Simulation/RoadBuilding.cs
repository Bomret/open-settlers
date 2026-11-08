using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>How far a drawn road has come (<see cref="RoadBuilding.Walk"/>, <see cref="Game.CheckRoad"/>).</summary>
public enum RoadStatus
{
    /// <summary>A step, or the start, is refused: the original would sound the error and leave the road as it was.</summary>
    Refused,

    /// <summary>Every step is allowed and the road has not reached a flag: it is still being drawn.</summary>
    Open,

    /// <summary>The last step reached a flag and the road was accepted.</summary>
    Complete,
}

/// <summary>
/// Road drawing as the original does it (<c>docs/rules/roads.md</c>): the allowed directions at the road's end
/// (<c>0x44b6ce</c>), the click on the next node (<c>0x443f62</c> to <c>0x44477f</c> inside <c>0x440c24</c>), and the
/// commit at a flag (<c>0x444e68</c>). The original draws into the map as the player clicks and takes the bits back
/// when the road is cancelled; <see cref="Walk"/> does the same inside one call and returns every bit it set unless the
/// road is accepted and kept.
/// </summary>
public static class RoadBuilding
{
    /// <summary>
    /// The column and row each of the six directions moves by: 0 east, 1 south-east, 2 south, 3 west, 4 north-west,
    /// 5 north. Direction <c>d</c> and <c>(d + 3) % 6</c> are opposite.
    /// </summary>
    public static IReadOnlyList<(int Dx, int Dy)> Steps { get; } = [(1, 0), (1, 1), (0, 1), (-1, 0), (-1, -1), (0, -1)];

    // The class the program's table gives a flag.
    private const int FlagClass = 3;

    /// <summary>
    /// Draws the road that starts at the flag on the node and takes the given directions, one step each, the way the
    /// original's click on the next node does. A refused start or step gives <see cref="RoadStatus.Refused"/> and
    /// leaves the map as it was. A road that ends short of a flag gives <see cref="RoadStatus.Open"/> and leaves the
    /// map as it was. A road whose last step reaches a flag and passes the commit gives
    /// <see cref="RoadStatus.Complete"/>; with <paramref name="keep"/> the road's bits stay in the map, otherwise they
    /// are taken back too. A direction after the road reached a flag is refused.
    /// </summary>
    internal static (RoadStatus Status, Road? Road) Walk(
        ProgramTables tables,
        Map map,
        int startX,
        int startY,
        IReadOnlyList<int> directions,
        bool keep
    )
    {
        var start = map.Index(startX, startY);
        // The road button needs a flag of the player's under the cursor (the evaluator's answers 1 and 2, 0x44b6e6).
        if (map.At(start).Owner != Owner.Player(0) || BuildSite.ObjectClass(tables, map.At(start).Object) != FlagClass)
        {
            return (RoadStatus.Refused, null);
        }

        // The bits the walk has set, as node and bit pairs in the order set, so that a step back and a refusal take
        // them back.
        var set = new List<(int Node, int Bit)>();
        var nodes = new List<int> { start };
        var steps = new List<int>();
        var cursor = (X: startX, Y: startY);
        Road? road = null;
        var status = RoadStatus.Open;

        void Set(int node, int bit)
        {
            map.At(node).Roads = map.At(node).Roads.With(bit);
            set.Add((node, bit));
        }

        void Revert()
        {
            for (var i = set.Count - 1; i >= 0; i--)
            {
                map.At(set[i].Node).Roads = map.At(set[i].Node).Roads.Without(set[i].Bit);
            }

            set.Clear();
        }

        foreach (var direction in directions)
        {
            if (status != RoadStatus.Open || direction is < 0 or > 5)
            {
                status = RoadStatus.Refused;
                break;
            }

            var from = map.Index(cursor.X, cursor.Y);
            var length = steps.Count;
            if (!Allowed(tables, map, cursor.X, cursor.Y, length, direction))
            {
                status = RoadStatus.Refused;
                break;
            }

            var (dx, dy) = Steps[direction];
            var next = (X: (cursor.X + dx) & (map.Size.Width - 1), Y: (cursor.Y + dy) & (map.Size.Height - 1));
            var to = map.Index(next.X, next.Y);
            var back = (direction + 3) % 6;

            if (map.At(from).Roads.Has(direction))
            {
                // The bit of this direction is set at the end: the click takes the last step back.
                if (length == 0)
                {
                    status = RoadStatus.Refused;
                    break;
                }

                map.At(from).Roads = map.At(from).Roads.Without(direction);
                map.At(to).Roads = map.At(to).Roads.Without(back);
                set.RemoveAll(entry => entry.Node == from && entry.Bit == direction);
                set.RemoveAll(entry => entry.Node == to && entry.Bit == back);
                steps.RemoveAt(length - 1);
                nodes.RemoveAt(nodes.Count - 1);
                cursor = next;
                continue;
            }

            var reachesFlag = map.At(to).Object.Kind == MapObjectKind.Flag;
            if (!map.At(to).Roads.IsEmpty && !reachesFlag)
            {
                // A node that holds a road already, and no flag: refused. The Road button never sets view flag bit 3 (docs/rules/roads.md).
                status = RoadStatus.Refused;
                break;
            }

            steps.Add(direction);
            nodes.Add(to);
            if (!reachesFlag)
            {
                Set(from, direction);
                Set(to, back);
                cursor = next;
                continue;
            }

            road = Commit(map, nodes, steps);
            if (road is null)
            {
                status = RoadStatus.Refused;
                break;
            }

            Set(from, direction);
            Set(to, back);
            status = RoadStatus.Complete;
        }

        if (status == RoadStatus.Complete && keep)
        {
            return (status, road);
        }

        Revert();

        return (status, status == RoadStatus.Complete ? road : null);
    }

    // The directions the original's cursor update (0x44b6ce) leaves allowed at the road's end: the node in that
    // direction belongs to the player, and it is the last step's node (the road may be taken back), or it holds no
    // object of class 2 or 4 (stones and rocks, the small building; a tree, class 1, does not block) and then it is a
    // flag, or it holds no road, or a flag could be put on it.
    private static bool Allowed(ProgramTables tables, Map map, int x, int y, int length, int direction)
    {
        var (dx, dy) = Steps[direction];
        var next = map.Index(x + dx, y + dy);
        if (map.At(next).Owner != Owner.Player(0))
        {
            return false;
        }

        // Only a road already begun marks the way back: at the start flag the roads that leave it are no step back.
        if (length != 0 && map.At(x, y).Roads.Has(direction))
        {
            return true;
        }

        var objectClass = BuildSite.ObjectClass(tables, map.At(next).Object);
        if (objectClass is 2 or 4)
        {
            return false;
        }

        if (objectClass == FlagClass || map.At(next).Roads.IsEmpty)
        {
            return true;
        }

        // simplicity: the original goes on to walk the road when a start-up option (bit 6 of +0x37e) is set; that
        // option is not recovered, and the emulator values hold it clear (docs/rules/roads.md).
        return BuildSite
                .Evaluate(
                    tables,
                    map,
                    (x + dx) & (map.Size.Width - 1),
                    (y + dy) & (map.Size.Height - 1),
                    0,
                    hasCastle: true
                )
                .Site != 0;
    }

    // The commit at a flag (0x444e68): walks the drawn road from the flag it ends at back to the one it started at.
    // Every node must be the player's, and the beside-triangles of the steps must be all land or all water.
    private static Road? Commit(Map map, List<int> nodes, List<int> steps)
    {
        foreach (var node in nodes)
        {
            if (map.At(node).Owner != Owner.Player(0))
            {
                return null;
            }
        }

        var kinds = 0;
        for (var k = nodes.Count - 1; k >= 1; k--)
        {
            // The step from nodes[k] back to nodes[k - 1].
            var back = (steps[k - 1] + 3) % 6;
            kinds |= Water(map, nodes[k], back) ? 2 : 1;
        }

        if (kinds is not (1 or 2))
        {
            return null;
        }

        return new Road(
            map.At(nodes[0]).Link ?? 0,
            steps[0],
            map.At(nodes[^1]).Link ?? 0,
            (steps[^1] + 3) % 6,
            [.. steps],
            kinds == 2
        );
    }

    // Whether the two triangles beside the step from the node in the direction are both water (0x444e68: the
    // terrain codes of 0 to 3 are water). The triangles are read from the node the step starts at, which differs by
    // direction.
    private static bool Water(Map map, int node, int direction)
    {
        var width = map.Size.Width;
        var x = node % width;
        var y = node / width;
        Node At(int dx, int dy) => map.At(x + dx, y + dy);

        return direction switch
        {
            0 => At(0, 0).Lower.IsWater && At(0, -1).Upper.IsWater,
            1 => At(0, 0).Upper.IsWater && At(0, 0).Lower.IsWater,
            2 => At(0, 0).Upper.IsWater && At(-1, 0).Lower.IsWater,
            3 => At(-1, 0).Lower.IsWater && At(-1, -1).Upper.IsWater,
            4 => At(-1, -1).Upper.IsWater && At(-1, -1).Lower.IsWater,
            _ => At(-1, -1).Lower.IsWater && At(0, -1).Upper.IsWater,
        };
    }
}
