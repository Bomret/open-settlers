using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// Where a road would stand if the player drew it from the flag on a node along directions, as the original's road drawing
/// judges it (<see cref="RoadBuilding"/>). It changes nothing, so an interface can call it after each step it draws.
/// </summary>
public sealed class RoadPlanner(RuleTables tables, Map map, CastleStock castle)
{
    /// <summary>The road is refused, still open, or reaching a flag and accepted; refused before the castle stands.</summary>
    public RoadStatus Check(int x, int y, IReadOnlyList<int> directions) =>
        castle.Castle is null
            ? RoadStatus.Refused
            : RoadBuilding.Walk(tables, map, x, y, directions, keep: false).Status;

    /// <summary>The road the directions draw from the flag on the node, with the status of the drawing; no road before the castle stands.</summary>
    public (RoadStatus Status, Road? Road) Plan(int x, int y, IReadOnlyList<int> directions) =>
        castle.Castle is null
            ? (RoadStatus.Refused, null)
            : RoadBuilding.Walk(tables, map, x, y, directions, keep: true);
}
