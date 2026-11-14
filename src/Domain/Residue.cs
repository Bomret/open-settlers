namespace OpenSettlers.Domain;

/// <summary>
/// The bytes of a settler's item that its activity does not use and that a change of activity leaves as they were: the
/// original shares four fields of the item (bytes 11 to 15) between its states, so a state's values stay in the item
/// after the settler has left it. They have no meaning in the activity that holds them. No rule reads or writes them: a change of
/// activity carries them over, and the exporters write them again so that the original's item is produced exactly
/// (<c>docs/rules/settlers.md</c>).
/// </summary>
public readonly record struct Residue
{
    /// <summary>Byte 11.</summary>
    public int Mode { get; init; }

    /// <summary>The word at 12.</summary>
    public int Target { get; init; }

    /// <summary>Byte 14.</summary>
    public int Heading { get; init; }

    /// <summary>Byte 15.</summary>
    public int Next { get; init; }
}
