namespace OpenSettlers.Simulation;

/// <summary>
/// Places the building site of the type the command names on the node, tied to the flag south-east of it
/// (<see cref="CommandKind.PlaceBuilding"/>, <see cref="BuildingPlacement.PlaceSite"/>). A command with no type is refused.
/// </summary>
public sealed class PlaceBuildingHandler(BuildingPlacement placement) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.PlaceBuilding;

    /// <inheritdoc/>
    public bool Handle(Command command) =>
        command.Building is { } type && placement.PlaceSite(type, command.X, command.Y);
}
