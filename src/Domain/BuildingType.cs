namespace OpenSettlers.Domain;

/// <summary>A building's type, by the number the original gives it (bits 2 to 6 of byte 4 of the building's item).</summary>
/// <param name="Code">The type's number; -1 is the type of a slot that holds no building.</param>
public readonly record struct BuildingType(int Code)
{
    /// <summary>
    /// The castle. Its type byte holds 0xe0 while the castle is built, the type 0x38 of the table of door speeds, and 0x60
    /// when it stands.
    /// </summary>
    public static BuildingType Castle => new(0x18);

    /// <summary>
    /// The woodcutter's hut: the handler of the type 2 (<c>0x42d945</c>) asks for the job of the woodcutter, whose work is to cut
    /// trees (<c>docs/rules/woodcutting.md</c>), and its sprite is the object 0xa8 (<c>0x44e63b</c>). The type 1 asks for a
    /// job whose work looks for a free node by the water.
    /// </summary>
    public static BuildingType Woodcutter => new(2);

    /// <summary>The storehouse: a storage like the castle (<c>docs/rules/removal.md</c>).</summary>
    public static BuildingType Storehouse => new(0xa);

    /// <summary>The type of a slot no building holds.</summary>
    public static BuildingType None => new(-1);

    /// <inheritdoc />
    public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
