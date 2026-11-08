using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The causes the rules give the events they record: the command that made a change, the layout that builds its entries, or
/// the rule area at work (ADR 0011). History only: no rule reads a cause.
/// </summary>
/// <param name="history">The history whose thing IDs the causes name.</param>
/// <param name="buildings">The building table.</param>
/// <param name="walkHome">The walk home: a settler on it carries its cause.</param>
public sealed class Causes(GameHistory history, BuildingTable buildings, WalkHomeBehavior walkHome)
{
    // The command cause a placed building carries until its worker has entered, the worker turns away, or the building is
    // freed: the events of the building and of the settler sent to it name the placing.
    private readonly Dictionary<ThingId, Cause> _placements = [];

    /// <summary>
    /// Set while a layout builds its entries: the commands it applies are the layout's entries, not the player's, and what a
    /// layout sets up is the layout's, whichever rule area runs it.
    /// </summary>
    public bool InLayout { get; set; }

    /// <summary>The cause of a change the settler rules make on their own: the layout inside a layout, else <c>settlers</c>.</summary>
    public Cause Rule => InLayout ? Cause.Layout : Cause.Settlers;

    /// <summary>The cause of a change a player's command makes: the command, or the layout while a layout builds its entries.</summary>
    public Cause Command(ThingId commanded) => InLayout ? Cause.Layout : Cause.Placed(commanded);

    /// <summary>The cause of a change to the building in the slot: the command that placed it while its worker is on the way, else the rule area.</summary>
    public Cause OfBuilding(int slot) =>
        _placements.TryGetValue(history.BuildingId(slot), out var command) ? command : Rule;

    /// <summary>
    /// The cause of a change the settler rules make to the settler in the slot: the walk home while it walks to a storage (a
    /// command cause it carries takes the place of either in <see cref="GameHistory.RecordSettler"/>), else the rule area's.
    /// </summary>
    public Cause OfSettler(int slot) => walkHome.IsWalking(slot) ? Cause.WalkHome : Rule;

    /// <summary>The building in the slot was placed by a command: its worker's events name it.</summary>
    public void Placed(int slot, Cause command) => _placements[history.BuildingId(slot)] = command;

    /// <summary>The command cause the building carries, if it carries one.</summary>
    public bool TryPlacementOf(int slot, out Cause command) =>
        _placements.TryGetValue(history.BuildingId(slot), out command);

    /// <summary>The building no longer carries a command cause.</summary>
    public void ForgetPlacement(ThingId building) => _placements.Remove(building);

    /// <summary>Records an event of the building, with the cause the building has now.</summary>
    public void RecordBuilding(Building building, string type, string? other = null)
    {
        var slot = buildings.SlotOf(building);
        history.RecordBuilding(slot, type, OfBuilding(slot), other);
    }
}
