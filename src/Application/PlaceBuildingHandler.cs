namespace OpenSettlers.Simulation;

/// <summary>
/// Puts a woodcutter's hut on the node, tied to the flag south-east of it (<see cref="CommandKind.PlaceBuilding"/>). The hut
/// stands finished at once: a **stand-in** until the construction slice, not a rule of the original. The map takes the marks
/// the castle's placement leaves, with the object code 2 (**inferred** for a small building; no run places one).
/// </summary>
public sealed class PlaceBuildingHandler(
    RuleTables tables,
    Map map,
    CastleStock castle,
    FlagTable flags,
    BuildingTable buildings,
    GameHistory history,
    Causes causes,
    Removal removal
) : ICommandHandler
{
    /// <inheritdoc/>
    public int Kind => CommandKind.PlaceBuilding;

    /// <inheritdoc/>
    public bool Handle(Command command)
    {
        var (x, y) = (command.X, command.Y);
        var width = map.Size.Width;
        var height = map.Size.Height;
        var flagX = (x + 1) & (width - 1);
        var flagY = (y + 1) & (height - 1);
        var flag = flags.SlotAt(flagX, flagY);
        var site = castle.Castle is null ? default : BuildSite.Evaluate(tables, map, x, y, 0, hasCastle: true);
        // **Inferred:** a small hut needs the site of a small or a large building (3 or 4), and either the flag standing
        // south-east of it (node state 5), a road's bits on that node (6), or nothing on or around it (7); the flag button
        // takes the same states for its flag (4, 6, 7 of the flag's own node).
        var withFlag = site.Node == 5 && flag >= 0 && buildings.OfFlag(flag) is null;
        if (site.Site < 3 || !(withFlag || site.Node is 6 or 7 && !flags.IsFull))
        {
            return false;
        }

        // The building's placement (0x449a63) puts its flag down on the south-east node where none stands there, as the
        // flag placement does (0x4422c5): the flag object and bit, the deposit moved, the link to the new slot.
        if (!withFlag)
        {
            FlagPlacement.Apply(map, flagX, flagY, flags.NextSlot);
            flag = FlagSlots.Add(flags, history, flagX, flagY);
            history.RecordFlag(flag, EventType.PlaceFlag, causes.Command(history.FlagId(flag)));

            // A flag on a road's node splits the road (0x4662e2), before the hut's mark at the flag is set below: the mark
            // (bit 4 of the flag's direction byte) is no road bit.
            if (site.Node == 6)
            {
                removal.SplitRoad(map.Index(flagX, flagY), flag, causes.Command(history.FlagId(flag)));
            }
        }

        var node = map.Index(x, y);
        var slot = buildings.NextSlot;
        ref var hutNode = ref map.At(node);
        hutNode.Roads = hutNode.Roads.With(1);
        hutNode.LakeMark = true;
        hutNode.Object = MapObject.Hut;
        hutNode.LinkTo(slot);
        ref var flagNode = ref map.At(flagX, flagY);
        flagNode.Roads = flagNode.Roads.With(4);
        buildings.Add(new Building(BuildingType.Woodcutter, x, y, flag));

        history.FillBuilding(slot);
        var cause = causes.Command(history.BuildingId(slot));
        if (cause.IsCommand)
        {
            causes.Placed(slot, cause);
        }

        history.RecordBuilding(slot, EventType.PlaceBuilding, cause, history.FlagId(flag).ToString());

        return true;
    }
}
