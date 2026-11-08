namespace OpenSettlers.Simulation;

/// <summary>Takes down the road that passes the node (<see cref="CommandKind.RemoveRoad"/>).</summary>
public sealed class RemoveRoadHandler(Removal removal) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.RemoveRoad;

    /// <inheritdoc/>
    public bool Handle(Command command) => removal.RemoveRoadAt(command.X, command.Y);
}
