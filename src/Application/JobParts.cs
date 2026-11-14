using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// The shared parts a job rules module is built from (ADR 0016): the same instances go to every module, so the modules act on one game.
/// </summary>
public sealed record JobParts
{
    /// <summary>The settlers' ground: the clock, the timer, the nodes, the change of activity, and the tables.</summary>
    public required SettlerMovement Movement { get; init; }

    /// <summary>The building table.</summary>
    public required BuildingTable Buildings { get; init; }

    /// <summary>The flag table.</summary>
    public required FlagTable Flags { get; init; }

    /// <summary>The history a module records its changes in (no rule reads it).</summary>
    public required GameHistory History { get; init; }

    /// <summary>The random generator.</summary>
    public required IRandomDraws Draws { get; init; }

    /// <summary>Lays a good on a place of a flag: the flag slot, the place, and the good.</summary>
    public required Action<int, int, int> LayGood { get; init; }

    /// <summary>
    /// The states the workers who go out from a building share: the step from a flag into the building (the turn of state 6), the step
    /// from a building onto its flag (the turn of state 7), and the generic arrival at the end of a free walk home (<c>0x4385ab</c>). The
    /// modules record their changes and lay their goods through it.
    /// </summary>
    public required HutWork Hut { get; init; }
}
