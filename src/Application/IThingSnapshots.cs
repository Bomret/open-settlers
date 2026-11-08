namespace OpenSettlers.Simulation;

/// <summary>
/// The port the <see cref="GameHistory"/> calls for the state of a thing when a rule records an event: the state the engine
/// holds for the thing in the slot, as the original's named raw values (INV-0006). The adapter that writes the values reads the
/// game's state; the history knows no field.
/// </summary>
public interface IThingSnapshots
{
    /// <summary>The state of the building in the slot of the building list.</summary>
    ThingState Building(int slot);

    /// <summary>The state of the flag in the slot of the flag list.</summary>
    ThingState Flag(int slot);

    /// <summary>The state of the settler in the slot of the settler list.</summary>
    ThingState Settler(int slot);
}
