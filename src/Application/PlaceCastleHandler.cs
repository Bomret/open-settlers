namespace OpenSettlers.Simulation;

/// <summary>
/// Puts the player's first castle on the node where the original allows it, and once (<see cref="CommandKind.PlaceCastle"/>):
/// the castle with its stock, its flag, and its 20 first settlers.
/// </summary>
/// <param name="tables">The rule tables.</param>
/// <param name="map">The map the castle is written into: the castle, its flag, the heights, and the territory.</param>
/// <param name="castle">The castle and its stock.</param>
/// <param name="startStock">The stock the castle holds when it is placed.</param>
/// <param name="flags">The flag table.</param>
/// <param name="settlers">The settler table.</param>
/// <param name="roster">The castle's settlers.</param>
/// <param name="history">The history.</param>
/// <param name="causes">The causes.</param>
public sealed class PlaceCastleHandler(
    RuleTables tables,
    Map map,
    CastleStock castle,
    System.Collections.Generic.IReadOnlyList<int> startStock,
    FlagTable flags,
    SettlerTable settlers,
    CastleRoster roster,
    GameHistory history,
    Causes causes
) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.PlaceCastle;

    /// <inheritdoc/>
    public bool Handle(Command command)
    {
        var (x, y) = (command.X, command.Y);
        if (castle.Castle is not null)
        {
            return false;
        }

        var site = BuildSite.Evaluate(tables, map, x, y, 0, hasCastle: false);
        if (!site.IsCastleSite)
        {
            return false;
        }

        castle.Place(
            new Castle
            {
                X = x,
                Y = y,
                Height = site.Height,
            },
            startStock
        );
        CastlePlacement.Apply(map, x, y, site.Height, 0);
        // The castle's flag stands south-east of it and takes the first slot of the flag table.
        FlagSlots.Add(flags, history, (x + 1) & (map.Size.Width - 1), (y + 1) & (map.Size.Height - 1));
        roster.CreateCastleSettlers(x, y);
        var cause = causes.Command(history.BuildingId(0));
        history.RecordBuilding(0, EventType.PlaceCastle, cause, history.FlagId(0).ToString());
        history.RecordFlag(0, EventType.PlaceCastle, cause);
        for (var slot = 0; slot < settlers.Count; slot++)
        {
            history.RecordSettler(slot, EventType.PlaceCastle, cause);
        }

        return true;
    }
}
