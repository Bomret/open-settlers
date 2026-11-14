namespace OpenSettlers.Simulation;

// The one way a flag takes a slot of the flag table.
static class FlagSlots
{
    // Puts a flag in the next slot; the item of the slot starts as zeros (the allocator clears it).
    internal static int Add(FlagTable flags, GameHistory history, int x, int y)
    {
        var slot = flags.Add(x, y);
        history.FillFlag(slot);

        return slot;
    }
}
