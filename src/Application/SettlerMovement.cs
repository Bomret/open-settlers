using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The ground the settlers share: which settler stands on which node (a node of settler 0 reads as free, as in the
/// original), the step of a settler onto a node, the timer that counts the clock down, the building a node holds, and the
/// change of a settler from one activity to another. The behaviours of the settlers build on it.
/// </summary>
/// <param name="map">The map the settlers walk on.</param>
/// <param name="buildings">The building table.</param>
/// <param name="tables">The rule tables (the step times).</param>
/// <param name="clock">The game clock in clock units (16 bits).</param>
/// <param name="residueOf">
/// The bytes of the original's item an activity leaves as they are when the settler takes up another activity: the port to the
/// adapter that knows the item.
/// </param>
public sealed class SettlerMovement(
    Map map,
    BuildingTable buildings,
    RuleTables tables,
    Func<ushort> clock,
    Func<Activity, Residue> residueOf
)
{
    private readonly Dictionary<int, int> _occupant = [];

    /// <summary>The map the settlers walk on.</summary>
    public Map Map => map;

    /// <summary>The rule tables.</summary>
    public RuleTables Tables => tables;

    /// <summary>The game clock in clock units.</summary>
    public ushort Clock => clock();

    /// <summary>The nodes that a settler stands on, by node index, with the slot of the settler, in the order of the indices.</summary>
    public IEnumerable<(int Index, int Slot)> Occupied =>
        _occupant.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value));

    /// <summary>The slot of the settler that stands on the node, 0 for none.</summary>
    public int Occupant(int index) => _occupant.TryGetValue(index, out var slot) ? slot : 0;

    /// <summary>The settler in the slot takes the node.</summary>
    public void Occupy(int index, int slot) => _occupant[index] = slot;

    /// <summary>The node is free of settlers.</summary>
    public void Release(int index) => _occupant.Remove(index);

    /// <summary>The height of a node.</summary>
    public int Height(int index) => map.At(index).Height;

    /// <summary>
    /// The building on a node: the node's link names its slot, and the building must stand on the node, since the link
    /// also holds a flag's slot.
    /// </summary>
    public Building? BuildingAt(int index)
    {
        var slot = map.At(index).LinkedSlot;

        return
            slot < buildings.Count
            && !buildings[slot].IsFree
            && map.Index(buildings[slot].X, buildings[slot].Y) == index
            ? buildings[slot]
            : null;
    }

    /// <summary>The column, the row, and the index of the node a step in a direction leads to.</summary>
    public (int X, int Y, int Index) Neighbour(Settler settler, int direction)
    {
        var (dx, dy) = Direction.Steps[direction];
        var x = (settler.X + dx) & (map.Size.Width - 1);
        var y = (settler.Y + dy) & (map.Size.Height - 1);

        return (x, y, map.Index(x, y));
    }

    /// <summary>
    /// On a node of a road, the one direction of the road other than the one the settler came by (its heading); -1 where the road
    /// has none or has more than one.
    /// </summary>
    public static int RoadContinuation(RoadSet roads, int heading)
    {
        var rest = roads.Without(heading);

        return rest.Count == 1 ? rest.First : -1;
    }

    /// <summary>
    /// The step onto a node. It records nothing: the rule that moves the settler records the change after it has applied all of
    /// it (a `moved` event here would show the settler on its new node with the rest of the change not yet made).
    /// </summary>
    public void Move(int slot, Settler settler, int x, int y, int index)
    {
        _occupant.Remove(map.Index(settler.X, settler.Y));
        settler.X = x;
        settler.Y = y;
        _occupant[index] = slot;
    }

    /// <summary>
    /// The timer of the states that wait: the time since the settler's last turn comes off its timer; true where the
    /// timer ran out, that is where the elapsed time was more than the time left (0x43dceb).
    /// </summary>
    public bool TimerRan(Settler settler)
    {
        var delta = (ushort)(Clock - settler.Stamp);
        settler.Stamp = Clock;
        var old = settler.Timer;
        settler.Timer = (ushort)(old - delta);

        return delta > old;
    }

    /// <summary>
    /// The settler takes up another activity. The bytes of the original's item that the new activity does not use stay as they
    /// were, in its residue.
    /// </summary>
    public void Become(Settler settler, Activity next) => settler.Activity = next.Carrying(residueOf(settler.Activity));

    /// <summary>The bytes of the original's item the activity leaves as they are when the settler takes up another one.</summary>
    public Residue ResidueOf(Activity activity) => residueOf(activity);

    /// <summary>
    /// The search for a flag the settler takes up where its walk is turned home with only its state changed (<c>0x461b77</c>,
    /// <c>0x462153</c>): the original reads the byte 11 the walk left (not 0: the far end first), and the other bytes stay.
    /// </summary>
    public SearchesHome HomeSearchFrom(Activity activity)
    {
        var residue = residueOf(activity);

        return new SearchesHome(residue.Mode != 0, residue.Mode) { Residue = residue };
    }

    /// <summary>
    /// The search for a flag home that takes the place of the activity a step's wait leads to (<c>0x462153</c>): as
    /// <see cref="HomeSearchFrom"/>, and the wait's end clears the next byte.
    /// </summary>
    public SearchesHome HomeSearchAfterStep(Activity then)
    {
        var search = HomeSearchFrom(then);

        return search with
        {
            Residue = search.Residue with { Next = 0 },
        };
    }

    /// <summary>The settler takes up another activity with the bytes the new activity does not use set to <paramref name="residue"/>.</summary>
    public void Become(Settler settler, Activity next, Residue residue) => settler.Activity = next.Carrying(residue);
}
