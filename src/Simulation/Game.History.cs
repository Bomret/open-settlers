using System.Collections.Generic;

namespace OpenSettlers.Simulation;

// The thing IDs and the event store of the game (ADR 0011, INV-0007): the counters and the store sit beside the game state, take
// no part in Digest, and no rule reads them to decide.
public sealed partial class Game
{
    // How often each slot of the settler, building, and flag lists was filled. A list the free slots at its end trimmed keeps
    // its counts, and nothing lowers one.
    private readonly List<int> _settlerFills = [];
    private readonly List<int> _buildingFills = [];
    private readonly List<int> _flagFills = [];

    /// <summary>The ID of the settler in the slot of <see cref="Settlers"/>; the fill count 0 for a slot never filled.</summary>
    public ThingId SettlerId(int slot) => new(ThingId.SettlerKind, slot, FillOf(_settlerFills, slot));

    /// <summary>The ID of the building in the slot of <see cref="Buildings"/>; the fill count 0 for a slot never filled.</summary>
    public ThingId BuildingId(int slot) => new(ThingId.BuildingKind, slot, FillOf(_buildingFills, slot));

    /// <summary>The ID of the flag in the slot of <see cref="Flags"/>; the fill count 0 for a slot never filled.</summary>
    public ThingId FlagId(int slot) => new(ThingId.FlagKind, slot, FillOf(_flagFills, slot));

    private static int FillOf(List<int> fills, int slot) => slot >= 0 && slot < fills.Count ? fills[slot] : 0;

    // Counts a fill of the slot: the slot's next thing takes the next count.
    private static void Fill(List<int> fills, int slot)
    {
        while (fills.Count <= slot)
        {
            fills.Add(0);
        }

        fills[slot]++;
    }
}
