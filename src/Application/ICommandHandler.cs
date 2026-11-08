namespace OpenSettlers.Simulation;

/// <summary>
/// The rule of one kind of <see cref="Command"/>. The game finds the handler of a command by its
/// <see cref="Command.Kind"/>, after it has checked that the command is the player's and aims at a node of the map.
/// </summary>
public interface ICommandHandler
{
    /// <summary>The <see cref="CommandKind"/> the handler applies.</summary>
    int Kind { get; }

    /// <summary>
    /// Applies the command where its rule allows it and tells whether it did. A command the rule refuses changes
    /// nothing in the game.
    /// </summary>
    bool Handle(Command command);
}
