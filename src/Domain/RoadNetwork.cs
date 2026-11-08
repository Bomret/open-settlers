using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The finished roads of the player in the order they were built, and the questions the rules ask of them: the road that
/// leaves a flag in a direction, and the flag and direction at its far end.
/// </summary>
public sealed class RoadNetwork
{
    private readonly List<Road> _roads = [];

    /// <summary>The roads in the order they were built (a road that was split or joined takes the end of the list).</summary>
    public IReadOnlyList<Road> Roads => _roads;

    /// <summary>Adds a road at the end of the list.</summary>
    public void Add(Road road) => _roads.Add(road);

    /// <summary>Removes a road.</summary>
    public void Remove(Road road) => _roads.Remove(road);

    /// <summary>The road that leaves a flag in a direction, or null.</summary>
    public Road? RoadAt(int flag, int direction)
    {
        foreach (var road in _roads)
        {
            if (
                (road.FromSlot == flag && road.FromDirection == direction)
                || (road.ToSlot == flag && road.ToDirection == direction)
            )
            {
                return road;
            }
        }

        return null;
    }

    /// <summary>The flag at the other end of a road that leaves a flag in a direction, and the direction the road leaves it in.</summary>
    public static (int Flag, int Direction) FarEnd(Road road, int flag, int direction) =>
        road.FromSlot == flag && road.FromDirection == direction
            ? (road.ToSlot, road.ToDirection)
            : (road.FromSlot, road.FromDirection);

    /// <summary>The flag at the other end of the road that leaves a flag in a direction, or -1 where none does.</summary>
    public int FlagNeighbour(int flag, int direction) =>
        RoadAt(flag, direction) is { } road ? FarEnd(road, flag, direction).Flag : -1;

    /// <summary>
    /// The route search over the flags (<c>0x42b4bc</c>, the copy of it in the settlers' walk, and <c>0x42ca7a</c>):
    /// the direction a settler at flag <paramref name="from"/> takes first to reach flag <paramref name="to"/>, or -1 where no
    /// road joins them, in a table of <paramref name="flagCount"/> flags. The search runs breadth first from
    /// <paramref name="from"/>, tries the directions of a flag from north (5) to east (0), and hands each flag it reaches
    /// the first direction of the flag it came from; of several routes of the same number of flags it keeps the first one it
    /// reached.
    /// </summary>
    public int Route(int flagCount, int from, int to)
    {
        if (from < 0 || to < 0 || from >= flagCount || to >= flagCount || from == to)
        {
            return -1;
        }

        var reached = new bool[flagCount];
        var first = new int[flagCount];
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
}
