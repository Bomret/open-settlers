using System;

namespace OpenSettlers.Domain;

/// <summary>
/// A kind of building (<c>docs/rules/build-sites.md</c>): the entry of the <see cref="ContentCatalog"/> that its <see cref="Name"/> names
/// (ADR 0020). The stable name is the type's whole identity: equality, hashing, and text go by it. The facts of a type are the entry's,
/// read through <see cref="RuleTables"/>; the original's number of a type is an attribute of the entry that only the History Edition
/// importer and the exporters read. The type of a slot that holds no building is <see cref="None"/>, which has no name and is never an entry.
/// </summary>
public readonly record struct BuildingType
{
    readonly string? _name;

    /// <summary>The type's stable name (<c>woodcutter-hut</c>); empty for the type of a slot that holds no building.</summary>
    public string Name
    {
        get => _name ?? "";
        init => _name = value;
    }

    /// <summary>Whether <paramref name="other"/> is the type with this stable name.</summary>
    public bool Equals(BuildingType other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <summary>A hash of the stable name that is the same on every run.</summary>
    public override int GetHashCode() => (int)Fnv1a.Of(Name);

    /// <summary>Whether this is the type of a slot that holds no building.</summary>
    public bool IsNone => Name.Length == 0;

    /// <summary>The castle, a storage with a door of its own, built from no needs.</summary>
    public static BuildingType Castle => new() { Name = "castle" };

    /// <summary>
    /// The woodcutter's hut: its handler asks for the job of the woodcutter, whose work is to cut trees
    /// (<c>docs/rules/woodcutting.md</c>).
    /// </summary>
    public static BuildingType Woodcutter => new() { Name = "woodcutter-hut" };

    /// <summary>The stonecutter's hut, a small building (<c>docs/rules/build-sites.md</c>, "The four types"). **Inferred** name.</summary>
    public static BuildingType Stonecutter => new() { Name = "stonecutter-hut" };

    /// <summary>The forester's hut, a small building (<c>docs/rules/build-sites.md</c>, "The four types"). **Inferred** name.</summary>
    public static BuildingType Forester => new() { Name = "forester-hut" };

    /// <summary>The sawmill, a large building (<c>docs/rules/build-sites.md</c>, "The four types"). **Inferred** name.</summary>
    public static BuildingType Sawmill => new() { Name = "sawmill" };

    /// <summary>
    /// The fisher's hut, a small building (<c>docs/rules/food-buildings.md</c>): its worker's walk ends by the water with the put of
    /// fish.
    /// </summary>
    public static BuildingType Fisher => new() { Name = "fisher-hut" };

    /// <summary>The farm, a large building: its worker's walk ends with the put of grain (<c>docs/rules/food-buildings.md</c>).</summary>
    public static BuildingType Farm => new() { Name = "farm" };

    /// <summary>The butcher, a large building: its job takes in pigs and puts meat (<c>docs/rules/food-buildings.md</c>).</summary>
    public static BuildingType Butcher => new() { Name = "butcher" };

    /// <summary>
    /// The pig farm, a large building and the farm's twin in size, materials, and scaffold: its job takes in grain and puts pigs
    /// (<c>docs/rules/food-buildings.md</c>).
    /// </summary>
    public static BuildingType PigFarm => new() { Name = "pig-farm" };

    /// <summary>
    /// The mill, a small building that the History Edition draws as a windmill: its job takes in grain and puts flour
    /// (<c>docs/rules/food-buildings.md</c>).
    /// </summary>
    public static BuildingType Mill => new() { Name = "mill" };

    /// <summary>The baker, a large building: its job takes in flour and puts bread (<c>docs/rules/food-buildings.md</c>).</summary>
    public static BuildingType Baker => new() { Name = "baker" };

    /// <summary>The storehouse: a storage like the castle (<c>docs/rules/removal.md</c>).</summary>
    public static BuildingType Storehouse => new() { Name = "storehouse" };

    /// <summary>The type of a slot no building holds.</summary>
    public static BuildingType None => default;

    /// <inheritdoc />
    public override string ToString() => Name;
}
