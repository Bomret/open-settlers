namespace OpenSettlers.Simulation;

/// <summary>
/// Builds the road the command's directions draw from the flag on its node, where every step is allowed and the last reaches a
/// flag (<see cref="CommandKind.BuildRoad"/>, <see cref="RoadBuilding"/>).
/// </summary>
public sealed class BuildRoadHandler(
    RoadPlanner planner,
    RoadNetwork roads,
    CarrierBehavior carriers,
    GameHistory history,
    Causes causes
) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.BuildRoad;

    /// <inheritdoc/>
    public bool Handle(Command command)
    {
        if (command.Directions is null)
        {
            return false;
        }

        var (status, road) = planner.Plan(command.X, command.Y, command.Directions);
        if (status != RoadStatus.Complete)
        {
            return false;
        }

        roads.Add(road!);
        carriers.SetSides(road!);
        // The road's `placed` cause names the road's start flag (the flag the road was built from).
        var cause = causes.Command(history.FlagId(road!.FromSlot));
        if (cause.IsCommand)
        {
            carriers.CarryCause(road, cause);
        }

        history.RecordFlag(road.FromSlot, EventType.BuildRoad, cause, history.FlagId(road.ToSlot).ToString());
        history.RecordFlag(road.ToSlot, EventType.BuildRoad, cause, history.FlagId(road.FromSlot).ToString());

        return true;
    }
}
