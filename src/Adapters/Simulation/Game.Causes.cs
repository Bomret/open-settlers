namespace OpenSettlers.Simulation;

// The cause of the changes a command makes. The events themselves are recorded in the game's History.
public sealed partial class Game
{
    // The cause of a change a player's command makes: the command, or the layout while a layout builds its entries.
    private Cause CommandCause(ThingId commanded) => InLayout ? Cause.Layout : Cause.Placed(commanded);

    // Set while Layout.Apply builds its entries: the commands it applies are the layout's entries, not the player's.
    internal bool InLayout { get; set; }
}
