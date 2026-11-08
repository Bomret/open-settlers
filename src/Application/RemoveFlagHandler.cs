namespace OpenSettlers.Simulation;

/// <summary>Takes down the flag on the node where the original's remove click allows it (<see cref="CommandKind.RemoveFlag"/>).</summary>
public sealed class RemoveFlagHandler(Removal removal) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.RemoveFlag;

    /// <inheritdoc/>
    public bool Handle(Command command) => removal.RemoveFlagAt(command.X, command.Y);
}
