using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The slices of the item tables that one pass of the original's main loop runs for a phase (ADR 0005,
/// <c>docs/rules/timing.md</c>): the flag table's slice (<c>0x46414d</c>), the building table's (<c>0x42cb2d</c>), then the
/// settler table's (<c>0x42f3d9</c>), in that order. Each settler of the slice is handed to the behaviour registered for its
/// activity type.
/// </summary>
/// <param name="settlers">The settler table.</param>
/// <param name="buildings">The building table.</param>
/// <param name="behaviours">The behaviour of each activity type.</param>
public sealed class TablePass(SettlerTable settlers, BuildingTable buildings, ActivityRegistry behaviours)
{
    /// <summary>The number of settler slices: a settler's slot, modulo this, is the phase of its turn.</summary>
    private const int SettlerSlices = 16;

    /// <summary>The buildings that make a slice: a building's slot divided by this is the group that takes its turn in one phase.</summary>
    private const int BuildingSlice = 32;

    /// <summary>
    /// The phases that run the item tables (0 to 31): the groups of buildings come round every <see cref="BuildingSlice"/> groups,
    /// that is every 1024 slots (<c>0x42cb2d</c>, <c>docs/rules/program-index.md</c>).
    /// </summary>
    private const int TablePhases = 32;

    /// <summary>
    /// Runs the slices of <paramref name="phase"/>: <paramref name="flags"/> for the flag table, <paramref name="building"/>
    /// for each building of the phase that holds one (with its slot), and the behaviour of each settler of the phase.
    /// </summary>
    public void Run(int phase, System.Action flags, System.Action<int, Building> building)
    {
        flags();
        for (var slot = 0; slot < buildings.Count; slot++)
        {
            if (slot / BuildingSlice % TablePhases != phase || buildings[slot].IsFree)
            {
                continue;
            }

            building(slot, buildings[slot]);
        }

        for (var slot = phase & (SettlerSlices - 1); slot < settlers.Count; slot += SettlerSlices)
        {
            Visit(slot);
        }
    }

    /// <summary>Gives the settler in the slot a turn: hands it to the behaviour of its activity type.</summary>
    public void Visit(int slot) => behaviours.Handle(slot, settlers[slot]);
}
