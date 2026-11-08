namespace OpenSettlers.Simulation;

// Taking down a flag, a road, or a building, and the split of a road by a flag, are the removal's (Removal,
// docs/rules/removal.md); the game forwards the click and the commands to it.
public sealed partial class Game
{
    /// <summary>The removal: the click class of a node, the take-downs, and the split of a road.</summary>
    internal Removal Removal => _removal;

    internal int ClickClass(int x, int y) => _removal.ClickClass(x, y);

    /// <summary>
    /// The command a click of the remove tool on the node asks for: <see cref="CommandKind.RemoveFlag"/> for a flag the original
    /// takes down, <see cref="CommandKind.RemoveBuilding"/> for a building that stands, <see cref="CommandKind.RemoveRoad"/>
    /// for a node of a road, and null where the original's check refuses the click.
    /// </summary>
    public int? RemovalAt(int x, int y) =>
        _removal.ClickClass(x, y) switch
        {
            2 => CommandKind.RemoveFlag,
            3 => CommandKind.RemoveBuilding,
            4 => CommandKind.RemoveRoad,
            _ => null,
        };

    // Internal for the test of the join of two water roads, whose flag the original's click refuses (ClickClass).
    internal void TakeFlagDown(int index) => _removal.TakeFlagDown(index);
}
