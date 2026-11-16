using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>A kind of good (<c>docs/rules/goods.md</c>), by the number the original gives it: 1 to 26, 7 a log; 0 is no good.</summary>
public readonly record struct Good
{
    /// <summary>The good's number, 0 to 31.</summary>
    public required int Code { get; init; }

    /// <summary>No good.</summary>
    public static Good None => default;

    /// <summary>
    /// The log, good 7: the woodcutter puts it on his flag (<c>0x438013</c>, <c>docs/rules/woodcutting.md</c>), and the castle's word for
    /// it, <c>0x12</c> of the storage item, rises with each delivered log.
    /// </summary>
    public static Good Log => new() { Code = 7 };

    /// <summary>
    /// The plank, good 8: a building site's request for planks (class 1 of the request word at <c>0x42</c>, row 8 of
    /// <c>0x464117</c>) takes the castle's stock word of this good (<c>docs/rules/construction-materials.md</c>).
    /// </summary>
    public static Good Plank => new() { Code = 8 };

    /// <summary>
    /// The stone, good 10: the stonecutter puts it on his flag (<c>0x438182</c>), and a building site's request for stones (class 4 of
    /// the request word at <c>0x44</c>, row 10 of <c>0x464117</c>) takes the castle's stock word of this good.
    /// </summary>
    public static Good Stone => new() { Code = 10 };

    /// <summary>
    /// The fish, good 1: the fisher's walk ends with the put of it on his flag (<c>0x43837f</c>); its class is class 0 of the request word
    /// at <c>0x42</c>, which goods 3 and 6 share (<c>docs/rules/food-buildings.md</c>). **Inferred** name.
    /// </summary>
    public static Good Fish => new() { Code = 1 };

    /// <summary>
    /// The pig, good 2: the job of the type 0xe puts it (<c>0x433577</c>) and the job of the type 0xd takes it in (class 3 of the
    /// request word at <c>0x42</c>, <c>0x43d249</c>); the History Edition draws it as a pig. **Inferred** name.
    /// </summary>
    public static Good Pig => new() { Code = 2 };

    /// <summary>The meat, good 3: the job of the type 0xd puts it (<c>0x4333a5</c>); class 0 of the word at <c>0x42</c>. **Inferred** name.</summary>
    public static Good Meat => new() { Code = 3 };

    /// <summary>
    /// The grain, good 4: the farm's walk ends with the put of it (<c>0x43847d</c>), and the jobs of the types 0xe and 0xf take it in
    /// (class 4 of the request word at <c>0x42</c>, <c>0x43d1e1</c> and <c>0x43d3b0</c>). **Inferred** name.
    /// </summary>
    public static Good Grain => new() { Code = 4 };

    /// <summary>
    /// The flour, good 5: the job of the type 0xf puts it (<c>0x433a1e</c>) and the job of the type 0x10 takes it in (class 5 of the
    /// request word at <c>0x42</c>, <c>0x43d44c</c>). **Inferred** name.
    /// </summary>
    public static Good Flour => new() { Code = 5 };

    /// <summary>The bread, good 6: the job of the type 0x10 puts it (<c>0x43384f</c>); class 0 of the word at <c>0x42</c>. **Inferred** name.</summary>
    public static Good Bread => new() { Code = 6 };

    /// <summary>The six food goods in the order of their numbers, 1 to 6: fish, pig, meat, grain, flour, bread.</summary>
    public static IReadOnlyList<Good> Foods { get; } = [Fish, Pig, Meat, Grain, Flour, Bread];

    /// <summary>Whether this is no good.</summary>
    public bool IsNone => Code == 0;

    /// <summary>Whether a lost good of this kind counts against the total of resource 1: the goods 14 and 15, the number taken to 0 to 31 (0x462c8d).</summary>
    public bool CountsAgainstResource1 => (Code & 0x1f) is 0xe or 0xf;

    /// <summary>
    /// Whether a good that the kind table (<see cref="RuleTables.FillCounterOf"/>) gives no counter counts against the high byte of the
    /// fill it is handed into (the castle's): the goods from 9 on except 13, and the log (7); every other good counts against the low
    /// byte. The carrier's handover reads the kind table first, so the byte a good is counted in on its way, counted back from, and
    /// delivered in is the table's.
    /// </summary>
    public bool FillsHigh => Code >= 9 ? Code != 13 : Code == 7;

    /// <inheritdoc />
    public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
