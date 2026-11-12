using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// The shared parts a job rules module is built from (ADR 0016): the same instances go to every module, so the modules act on one game.
/// </summary>
/// <param name="Movement">The settlers' ground: the clock, the timer, the nodes, the change of activity, and the tables.</param>
/// <param name="Buildings">The building table.</param>
/// <param name="Flags">The flag table.</param>
/// <param name="History">The history a module records its changes in (no rule reads it).</param>
/// <param name="Draws">The random generator.</param>
/// <param name="LayGood">Lays a good on a place of a flag: the flag slot, the place, and the good.</param>
/// <param name="EnterHut">The step from a flag into the building north-west of it (the turn of state 6).</param>
/// <param name="StepOut">The step from a building onto its flag (the turn of state 7).</param>
/// <param name="ArriveHome">The generic arrival at the end of a free walk home (<c>0x4385ab</c>), which records its change.</param>
public sealed record JobParts(
    SettlerMovement Movement,
    BuildingTable Buildings,
    FlagTable Flags,
    GameHistory History,
    IRandomDraws Draws,
    Action<int, int, int> LayGood,
    Action<int, Settler> EnterHut,
    Action<int, Settler> StepOut,
    Action<int, Settler> ArriveHome
);
