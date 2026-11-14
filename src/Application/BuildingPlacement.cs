using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Puts a building on a node with its flag south-east of it, as <c>Building_Place</c> (<c>0x449a63</c>) does
/// (<c>docs/rules/build-sites.md</c>, "Placing a building"): the player's command places a building site
/// (<see cref="PlaceSite"/>), and a layout's finished-building entry places a finished building (<see cref="PlaceFinished"/>),
/// as the original's mission start does (<c>0x41f2d2</c>). No command gives a finished building.
/// </summary>
public sealed class BuildingPlacement(
    RuleTables tables,
    Map map,
    CastleStock castle,
    FlagTable flags,
    BuildingTable buildings,
    GameHistory history,
    Causes causes,
    Removal removal
)
{
    /// <summary>The building types a placement builds: the types of the construction milestone.</summary>
    public static IReadOnlyList<BuildingType> Types { get; } =
    [BuildingType.Woodcutter, BuildingType.Stonecutter, BuildingType.Forester, BuildingType.Sawmill];

    /// <summary>
    /// Places a building site of <paramref name="type"/> on the node, with the needs and the height the original writes. Refused,
    /// with no change and no event, for a type no placement builds, before the castle stands, and where the node holds no site
    /// for the type's size or no room for its flag.
    /// </summary>
    public bool PlaceSite(BuildingType type, int x, int y) => Place(type, x, y, finished: false);

    /// <summary>
    /// Places a finished building of <paramref name="type"/> on the node, as the mission start's placement leaves it: the same
    /// placement and checks as <see cref="PlaceSite"/>, then the building stands. A **stand-in** for what the original's
    /// finished building holds beyond that (its status bit 4 and progress word), so the digests of the M3 layouts stay.
    /// </summary>
    public bool PlaceFinished(BuildingType type, int x, int y) => Place(type, x, y, finished: true);

    bool Place(BuildingType type, int x, int y, bool finished)
    {
        if (!Types.Contains(type))
        {
            return false;
        }

        var size = tables.SizeOf(type);
        var width = map.Size.Width;
        var height = map.Size.Height;
        var flagX = (x + 1) & (width - 1);
        var flagY = (y + 1) & (height - 1);
        var flag = flags.SlotAt(flagX, flagY);
        var site = castle.Castle is null
            ? default
            : BuildSite.Evaluate(tables, map, x, y, 0, hasCastle: true, buildings);
        // A small type needs the site of a small or a large building (3 or 4), a large type the site of a large one (4) (the
        // size table 0x42d307, as 0x42cf9d reads it). **Inferred:** the building takes either the flag standing south-east of
        // it (node state 5), a road's bits on that node (6), or nothing on or around it (7); the flag button takes the same
        // states for its flag (4, 6, 7 of the flag's own node).
        var fits = size == BuildingSize.Large ? site.Site == 4 : size == BuildingSize.Small && site.Site >= 3;
        var withFlag = site.Node == 5 && flag >= 0 && buildings.OfFlag(flag) is null;
        if (!fits || !(withFlag || site.Node is 6 or 7 && !flags.IsFull))
        {
            return false;
        }

        // The placement (0x449a63) first moves the deposit of the building's node to the nodes around it, as it does for a flag's
        // node (0x449f58), before it writes the building's object and link, then puts the building's flag down on the south-east
        // node where none stands there, as the flag placement does (0x4422c5): the flag object and bit, the deposit moved, the
        // link to the new slot. A flag node of a road whose bits hold no two road directions fails before anything changes.
        if (!withFlag && site.Node == 6)
        {
            removal.CheckSplit(map.Index(flagX, flagY));
        }

        FlagPlacement.MoveDeposit(map, x, y);
        if (!withFlag)
        {
            FlagPlacement.Apply(map, flagX, flagY, flags.NextSlot);
            flag = FlagSlots.Add(flags, history, flagX, flagY);
            history.RecordFlag(flag, EventType.PlaceFlag, causes.Command(history.FlagId(flag)));

            // A flag on a road's node splits the road (0x4662e2), before the building's mark at the flag is set below: the
            // mark (bit 4 of the flag's direction byte) is no road bit.
            if (site.Node == 6)
            {
                removal.SplitRoad(map.Index(flagX, flagY), flag, causes.Command(history.FlagId(flag)));
            }
        }

        var node = map.Index(x, y);
        var slot = buildings.NextSlot;
        ref var buildingNode = ref map.At(node);
        buildingNode.Roads = buildingNode.Roads.With(1);
        buildingNode.LakeMark = true;
        buildingNode.Object = size == BuildingSize.Large ? MapObject.LargeHut : MapObject.Hut;
        buildingNode.LinkTo(slot);
        ref var flagNode = ref map.At(flagX, flagY);
        flagNode.Roads = flagNode.Roads.With(4);

        var needs = tables.NeedsOf(type);
        if (!finished)
        {
            // The placement (0x449a63) sets the flag's request words: the plank's class and the stone's class, each in the request
            // word the good class table gives it, at rank 0 until the building pass gives the site's requests their rank.
            foreach (var good in new[] { Good.Plank, Good.Stone })
            {
                if (tables.ClassOfGood(good) is { } asked)
                {
                    flags[flag].SetRequest(asked.Word, new Request { Asks = asked, Rank = 0 });
                }
            }
        }

        buildings.Add(
            new Building(type, x, y, flag, constructing: !finished)
            {
                // The progress word starts at 1 for a small building and 0 for a large one, which waits to be levelled.
                Progress = finished ? (ushort)0 : (ushort)(size == BuildingSize.Small ? 1 : 0),
                Site = finished
                    ? null
                    : new BuildingSite
                    {
                        Planks = needs.Planks,
                        Stones = needs.Stones,
                        TargetHeight = site.Height,
                    },
            }
        );

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
