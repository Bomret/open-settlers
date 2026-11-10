using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// A player's action that the simulation takes at a given tick. <c>Kind</c> names the action; its codes belong to
/// the slices that add actions. <c>X</c> and <c>Y</c> are the tile the action aims at. Two commands are equal when
/// their fields and their <c>Directions</c>, element by element, are equal.
/// </summary>
/// <param name="Tick">The tick whose <see cref="Game.Tick"/> applies the command; counted from 0.</param>
/// <param name="Player">The player who gives the command.</param>
/// <param name="Kind">The action.</param>
/// <param name="X">The tile column.</param>
/// <param name="Y">The tile row.</param>
/// <param name="Directions">
/// The directions of a road, one of 0 to 5 for each step (<see cref="Road.Directions"/>); null for every other action.
/// </param>
/// <param name="Building">
/// The type of the building a <see cref="CommandKind.PlaceBuilding"/> places; null for every other action. A placement with
/// no type is refused.
/// </param>
public readonly record struct Command(
    long Tick,
    int Player,
    int Kind,
    int X,
    int Y,
    IReadOnlyList<int>? Directions = null,
    BuildingType? Building = null
) : IEquatable<Command>
{
    /// <inheritdoc/>
    public bool Equals(Command other) =>
        Tick == other.Tick
        && Player == other.Player
        && Kind == other.Kind
        && X == other.X
        && Y == other.Y
        && Building == other.Building
        && (Directions ?? []).SequenceEqual(other.Directions ?? []);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(Tick, Player, Kind, X, Y, Building);
        foreach (var direction in Directions ?? [])
        {
            hash = HashCode.Combine(hash, direction);
        }

        return hash;
    }
}
