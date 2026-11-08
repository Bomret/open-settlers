namespace OpenSettlers.Domain;

/// <summary>A kind of good (<c>docs/rules/goods.md</c>), by the number the original gives it: 1 to 26, 7 a log; 0 is no good.</summary>
/// <param name="Code">The good's number, 0 to 31.</param>
public readonly record struct Good(int Code)
{
    /// <summary>No good.</summary>
    public static Good None => default;

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
