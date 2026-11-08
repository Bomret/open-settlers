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

    /// <summary>Adds roads at the end of the list, in order.</summary>
    public void AddRange(IEnumerable<Road> roads) => _roads.AddRange(roads);

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
}
