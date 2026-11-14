using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The state of a settler, building, or flag at one moment: the raw values the engine holds for it, each as a field name and
/// its number, in a fixed order. The names of a settler and a building are the ones the <c>list</c> step shows.
/// </summary>
public sealed record ThingState
{
    /// <summary>The fields in order.</summary>
    public required IReadOnlyList<(string Field, int Value)> Fields { get; init; }

    /// <summary>The value of the field, or null for a field the state has not.</summary>
    public int? Get(string field) => Fields.Where(f => f.Field == field).Select(f => (int?)f.Value).FirstOrDefault();

    /// <summary>The fields as <c>name=value</c> text in order.</summary>
    public override string ToString() => string.Join(" ", Fields.Select(f => $"{f.Field}={f.Value}"));

    /// <summary>States are equal where their fields are.</summary>
    public bool Equals(ThingState? other) => other is not null && Fields.SequenceEqual(other.Fields);

    /// <summary>The hash of the fields.</summary>
    public override int GetHashCode() => Fields.Aggregate(0, (hash, f) => HashCode.Combine(hash, f.Field, f.Value));
}
