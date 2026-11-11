namespace OpenSettlers.Simulation;

/// <summary>
/// Puts a flag on the node where the flag button of the original allows it (<see cref="CommandKind.PlaceFlag"/>); a flag on a
/// node that holds a road splits the road (docs/rules/roads.md#a-flag-on-a-road).
/// </summary>
public sealed class PlaceFlagHandler(
    RuleTables tables,
    Map map,
    CastleStock castle,
    FlagTable flags,
    GameHistory history,
    Causes causes,
    Removal removal,
    BuildingTable? buildings = null
) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.PlaceFlag;

    /// <inheritdoc/>
    public bool Handle(Command command)
    {
        var (x, y) = (command.X, command.Y);
        if (castle.Castle is null || flags.IsFull)
        {
            return false;
        }

        var site = BuildSite.Evaluate(tables, map, x, y, 0, hasCastle: true, buildings);
        if (!site.AllowsFlag)
        {
            return false;
        }

        FlagPlacement.Apply(map, x, y, flags.NextSlot);
        var slot = FlagSlots.Add(flags, history, x, y);
        var cause = causes.Command(history.FlagId(slot));
        history.RecordFlag(slot, EventType.PlaceFlag, cause);

        // A flag on a node that holds a road splits the road (node state 4, docs/rules/roads.md#a-flag-on-a-road).
        if (site.Node == 4)
        {
            removal.SplitRoad(map.Index(x, y), slot, cause);
        }

        return true;
    }
}
