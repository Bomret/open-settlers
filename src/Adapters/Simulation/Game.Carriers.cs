namespace OpenSettlers.Simulation;

// The carriers are the carrier behaviour's (CarrierBehavior, docs/rules/carriers.md); the game keeps the flag table's slots
// and the lookups of the roads between the flags.
public sealed partial class Game
{
    /// <summary>The carriers: the flag table's pass, the carrier requests, and the walk along the roads.</summary>
    internal CarrierBehavior Carriers => _carriers;

    // Puts a flag in the next slot; the item of the slot starts as zeros (the allocator clears it).
    private int AddFlag(int x, int y)
    {
        var slot = _flags.Add(x, y);
        History.FillFlag(slot);

        return slot;
    }

    private int FlagNeighbour(int flag, int direction) => _roads.FlagNeighbour(flag, direction);
}
