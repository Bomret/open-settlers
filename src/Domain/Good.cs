using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// A kind of good (<c>docs/rules/goods.md</c>): the entry of the <see cref="ContentCatalog"/> that its <see cref="Name"/> names (ADR 0020).
/// The stable name is the good's whole identity: equality, hashing, and text go by it. The facts of a good are the entry's, read through
/// <see cref="RuleTables"/>; the original's number of a good is an attribute of the entry that a source of the original's data sets and the
/// exporters read, and whose presence the readout reads. No good is <see cref="None"/>, which has no name and is never an entry.
/// </summary>
public readonly record struct Good
{
    readonly string? _name;

    /// <summary>The good's stable name (<c>log</c>); empty for no good.</summary>
    public string Name
    {
        get => _name ?? "";
        init => _name = value;
    }

    /// <summary>Whether <paramref name="other"/> is the good with this stable name.</summary>
    public bool Equals(Good other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <summary>A hash of the stable name that is the same on every run.</summary>
    public override int GetHashCode() => (int)Fnv1a.Of(Name);

    /// <summary>No good.</summary>
    public static Good None => default;

    /// <summary>
    /// The log: the woodcutter puts it on his flag (<c>0x438013</c>, <c>docs/rules/woodcutting.md</c>), and the castle's word for
    /// it, <c>0x12</c> of the storage item, rises with each delivered log.
    /// </summary>
    public static Good Log => new() { Name = "log" };

    /// <summary>
    /// The plank: a building site's request for planks (class 1 of the request word at <c>0x42</c>, row 8 of
    /// <c>0x464117</c>) takes the castle's stock word of this good (<c>docs/rules/construction-materials.md</c>).
    /// </summary>
    public static Good Plank => new() { Name = "plank" };

    /// <summary>
    /// The stone: the stonecutter puts it on his flag (<c>0x438182</c>), and a building site's request for stones (class 4 of
    /// the request word at <c>0x44</c>, row 10 of <c>0x464117</c>) takes the castle's stock word of this good.
    /// </summary>
    public static Good Stone => new() { Name = "stone" };

    /// <summary>
    /// The fish: the fisher's walk ends with the put of it on his flag (<c>0x43837f</c>); its class is class 0 of the request word
    /// at <c>0x42</c>, which the meat and the bread share (<c>docs/rules/food-buildings.md</c>). **Inferred** name.
    /// </summary>
    public static Good Fish => new() { Name = "fish" };

    /// <summary>
    /// The pig: the job of the type 0xe puts it (<c>0x433577</c>) and the job of the type 0xd takes it in (class 3 of the
    /// request word at <c>0x42</c>, <c>0x43d249</c>). **Inferred** name.
    /// </summary>
    public static Good Pig => new() { Name = "pig" };

    /// <summary>The meat: the job of the type 0xd puts it (<c>0x4333a5</c>); class 0 of the word at <c>0x42</c>. **Inferred** name.</summary>
    public static Good Meat => new() { Name = "meat" };

    /// <summary>
    /// The grain: the farm's walk ends with the put of it (<c>0x43847d</c>), and the jobs of the types 0xe and 0xf take it in
    /// (class 4 of the request word at <c>0x42</c>, <c>0x43d1e1</c> and <c>0x43d3b0</c>). **Inferred** name.
    /// </summary>
    public static Good Grain => new() { Name = "grain" };

    /// <summary>
    /// The flour: the job of the type 0xf puts it (<c>0x433a1e</c>) and the job of the type 0x10 takes it in (class 5 of the
    /// request word at <c>0x42</c>, <c>0x43d44c</c>). **Inferred** name.
    /// </summary>
    public static Good Flour => new() { Name = "flour" };

    /// <summary>The bread: the job of the type 0x10 puts it (<c>0x43384f</c>); class 0 of the word at <c>0x42</c>. **Inferred** name.</summary>
    public static Good Bread => new() { Name = "bread" };

    /// <summary>
    /// The sword: a new knight takes one from the castle's stock (<c>0x428a75</c>, <c>docs/rules/new-settlers.md</c>). **Inferred**
    /// name; which of the two weapons the original keeps is the sword and which the shield is open (<c>docs/rules/food-buildings.md</c>).
    /// </summary>
    public static Good Sword => new() { Name = "sword" };

    /// <summary>
    /// The shield: a new knight takes one from the castle's stock (<c>0x428a75</c>). **Inferred** name; which of the two weapons the
    /// original keeps is the sword and which the shield is open.
    /// </summary>
    public static Good Shield => new() { Name = "shield" };

    /// <summary>The six food goods in the order the food readout shows them: fish, pig, meat, grain, flour, bread.</summary>
    public static IReadOnlyList<Good> Foods { get; } = [Fish, Pig, Meat, Grain, Flour, Bread];

    /// <summary>Whether this is no good.</summary>
    public bool IsNone => Name.Length == 0;

    /// <summary>The good's stable name.</summary>
    public override string ToString() => Name;
}
