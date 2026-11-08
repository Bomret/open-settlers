namespace OpenSettlers.Simulation;

/// <summary>Takes down the building on the node (<see cref="CommandKind.RemoveBuilding"/>).</summary>
public sealed class RemoveBuildingHandler(Removal removal) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.RemoveBuilding;

    /// <inheritdoc/>
    public bool Handle(Command command) => removal.RemoveBuildingAt(command.X, command.Y);
}
