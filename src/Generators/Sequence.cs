using System.Linq;

namespace OpenSettlers.Generators;

/// <summary>A list that compares by its items, so that a model holding it compares equal across compilations.</summary>
sealed class Sequence<T>(T[] items) : System.IEquatable<Sequence<T>>
{
    public T[] Items { get; } = items;

    public bool Equals(Sequence<T>? other) => other is not null && Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => Equals(obj as Sequence<T>);

    public override int GetHashCode() => Items.Aggregate(17, (hash, item) => hash * 31 + (item?.GetHashCode() ?? 0));
}
