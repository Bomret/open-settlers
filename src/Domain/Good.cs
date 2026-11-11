namespace OpenSettlers.Domain;

/// <summary>A kind of good (<c>docs/rules/goods.md</c>), by the number the original gives it: 1 to 26, 7 a log; 0 is no good.</summary>
/// <param name="Code">The good's number, 0 to 31.</param>
public readonly record struct Good(int Code)
{
    /// <summary>No good.</summary>
    public static Good None => default;

    /// <summary>
    /// The log, good 7: the woodcutter puts it on his flag (<c>0x438013</c>, <c>docs/rules/woodcutting.md</c>), and the castle's word for
    /// it, <c>0x12</c> of the storage item, rises with each delivered log.
    /// </summary>
    public static Good Log => new(7);

    /// <summary>
    /// The plank, good 8: a building site's request for planks (class 1 of the request word at <c>0x42</c>, row 8 of
    /// <c>0x464117</c>) takes the castle's stock word of this good (<c>docs/rules/construction-materials.md</c>).
    /// </summary>
    public static Good Plank => new(8);

    /// <summary>
    /// The stone, good 10: the stonecutter puts it on his flag (<c>0x438182</c>), and a building site's request for stones (class 4 of
    /// the request word at <c>0x44</c>, row 10 of <c>0x464117</c>) takes the castle's stock word of this good.
    /// </summary>
    public static Good Stone => new(10);

    /// <summary>Whether this is no good.</summary>
    public bool IsNone => Code == 0;

    /// <summary>Whether a lost good of this kind counts against the total of resource 1: the goods 14 and 15, the number taken to 0 to 31 (0x462c8d).</summary>
    public bool CountsAgainstResource1 => (Code & 0x1f) is 0xe or 0xf;

    /// <summary>
    /// Whether a good of this kind handed into a building counts against the high byte of its fill: the log (7) and the goods
    /// from 9 on except 13; every other good counts against the low byte.
    /// </summary>
    public bool FillsHigh => Code >= 9 ? Code != 13 : Code == 7;

    /// <inheritdoc />
    public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
