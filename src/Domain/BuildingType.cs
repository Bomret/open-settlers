namespace OpenSettlers.Domain;

/// <summary>A building's type, by the number the original gives it (bits 2 to 6 of byte 4 of the building's item).</summary>
public readonly record struct BuildingType
{
    /// <summary>The type's number; -1 is the type of a slot that holds no building.</summary>
    public required int Code { get; init; }

    /// <summary>
    /// The castle. Its type byte holds 0xe0 while the castle is built, the type 0x38 of the table of door speeds, and 0x60
    /// when it stands.
    /// </summary>
    public static BuildingType Castle => new() { Code = 0x18 };

    /// <summary>
    /// The woodcutter's hut: the handler of the type 2 (<c>0x42d945</c>) asks for the job of the woodcutter, whose work is to cut
    /// trees (<c>docs/rules/woodcutting.md</c>), and its sprite is the object 0xa8 (<c>0x44e63b</c>). The type 1 asks for a
    /// job whose work looks for a free node by the water.
    /// </summary>
    public static BuildingType Woodcutter => new() { Code = 2 };

    /// <summary>
    /// The stonecutter's hut, a small building (<c>docs/rules/build-sites.md</c>, "The four types"). **Inferred** name: the
    /// handler of the type 4 (<c>0x42d987</c>) asks for the job 7 with the tool of the stock word 0x2e (good 23); a later slice confirms the name
    /// from the goods the job puts down.
    /// </summary>
    public static BuildingType Stonecutter => new() { Code = 4 };

    /// <summary>
    /// The forester's hut, a small building (<c>docs/rules/build-sites.md</c>, "The four types"). **Inferred** name: the
    /// handler of the type 9 (<c>0x42d9c9</c>) asks for the job 8 with no tool; a later slice confirms the name from the
    /// activity the job runs.
    /// </summary>
    public static BuildingType Forester => new() { Code = 9 };

    /// <summary>
    /// The sawmill, a large building (<c>docs/rules/build-sites.md</c>, "The four types"). **Inferred** name: the handler of
    /// the type 0x11 (<c>0x42da0b</c>) asks for the job 6 with the tool of the stock word 0x2c (good 22); a later slice confirms the name from
    /// the goods the job puts down.
    /// </summary>
    public static BuildingType Sawmill => new() { Code = 0x11 };

    /// <summary>
    /// The fisher's hut, a small building (<c>docs/rules/food-buildings.md</c>). Its handler (<c>0x42e0b8</c>) asks for job 11 with the
    /// tool of the stock word 0x24 (good 18), and job 11's walk ends by the water with the put of good 1 (fish, <c>0x43837f</c>).
    /// </summary>
    public static BuildingType Fisher => new() { Code = 1 };

    /// <summary>
    /// The farm, a large building: the handler of the type 0xc (<c>0x42e25e</c>) asks for job 14 with the tool of the stock word 0x28
    /// (good 20), and job 14's walk ends with the put of good 4 (grain, <c>0x43847d</c>).
    /// </summary>
    public static BuildingType Farm => new() { Code = 0xc };

    /// <summary>
    /// The baker, a large building: the handler of the type 0xd (<c>0x42e1b3</c>) asks for job 13 with the tool of the stock word 0x26
    /// (good 19); the job takes in good 2 (flour) and puts good 3 (bread, <c>0x4333a5</c>).
    /// </summary>
    public static BuildingType Baker => new() { Code = 0xd };

    /// <summary>
    /// The mill, a large building: the handler of the type 0xe (<c>0x42e0fa</c>) asks for job 12 with no tool; the job takes in good 4
    /// (grain) and puts good 2 (flour, <c>0x433577</c>).
    /// </summary>
    public static BuildingType Mill => new() { Code = 0xe };

    /// <summary>
    /// The pig farm, a small building: the handler of the type 0xf (<c>0x42e2a0</c>) asks for job 15 with no tool; the job takes in
    /// good 4 (grain) and puts good 5 (pig, <c>0x433a1e</c>).
    /// </summary>
    public static BuildingType PigFarm => new() { Code = 0xf };

    /// <summary>
    /// The butcher, a large building: the handler of the type 0x10 (<c>0x42e37e</c>) asks for job 16 with no tool; the job takes in
    /// good 5 (pig) and puts good 6 (meat, <c>0x43384f</c>).
    /// </summary>
    public static BuildingType Butcher => new() { Code = 0x10 };

    /// <summary>The storehouse: a storage like the castle (<c>docs/rules/removal.md</c>).</summary>
    public static BuildingType Storehouse => new() { Code = 0xa };

    /// <summary>The type of a slot no building holds.</summary>
    public static BuildingType None => new() { Code = -1 };

    /// <inheritdoc />
    public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
