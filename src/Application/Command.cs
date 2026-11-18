using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// A player's action that the simulation takes at a given tick. <c>Kind</c> names the action; its codes belong to
/// the slices that add actions. <c>X</c> and <c>Y</c> are the tile the action aims at. Two commands are equal when
/// their fields, their <c>Directions</c> and their <c>Settlers</c>, element by element, and their <c>Stock</c>, good by good, are equal.
/// </summary>
public readonly record struct Command : IEquatable<Command>
{
    /// <summary>The tick whose <see cref="Game.Tick"/> applies the command; counted from 0.</summary>
    public required long Tick { get; init; }

    /// <summary>The player who gives the command.</summary>
    public required int Player { get; init; }

    /// <summary>The action.</summary>
    public required int Kind { get; init; }

    /// <summary>The tile column.</summary>
    public required int X { get; init; }

    /// <summary>The tile row.</summary>
    public required int Y { get; init; }

    /// <summary>The directions of a road, one of 0 to 5 for each step (<see cref="Road.Directions"/>); null for every other action.</summary>
    public IReadOnlyList<int>? Directions { get; init; }

    /// <summary>
    /// The type of the building a <see cref="CommandKind.PlaceBuilding"/> places; null for every other action. A placement with
    /// no type is refused.
    /// </summary>
    public BuildingType? Building { get; init; }

    /// <summary>
    /// The goods the castle a <see cref="CommandKind.PlaceCastle"/> places holds in place of its start stock, each good by stable name
    /// with its count; a good it does not name counts 0. Null for the start stock and for every other action. Only a layout's castle
    /// names it (<c>docs/rules/layouts.md</c>), an engine stand-in.
    /// </summary>
    public IReadOnlyDictionary<Good, int>? Stock { get; init; }

    /// <summary>
    /// The settlers that wait in the castle a <see cref="CommandKind.PlaceCastle"/> places in place of its 20 first settlers, after its
    /// castle settler, in order; null for the 20 first settlers and for every other action. Only a layout's castle names them
    /// (<c>docs/rules/layouts.md</c>), an engine stand-in.
    /// </summary>
    public IReadOnlyList<Job>? Settlers { get; init; }

    /// <inheritdoc/>
    public bool Equals(Command other) =>
        Tick == other.Tick
        && Player == other.Player
        && Kind == other.Kind
        && X == other.X
        && Y == other.Y
        && Building == other.Building
        && (Directions ?? []).SequenceEqual(other.Directions ?? [])
        && (Stock is null) == (other.Stock is null)
        && (Stock ?? Empty).Count == (other.Stock ?? Empty).Count
        && (Stock ?? Empty).All(pair =>
            (other.Stock ?? Empty).TryGetValue(pair.Key, out var count) && count == pair.Value
        )
        && (Settlers is null) == (other.Settlers is null)
        && (Settlers ?? []).SequenceEqual(other.Settlers ?? []);

    static readonly IReadOnlyDictionary<Good, int> Empty = new Dictionary<Good, int>();

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
