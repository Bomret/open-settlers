namespace OpenSettlers.Domain;

/// <summary>
/// The bytes of a settler's item that its activity does not use and that a change of activity leaves as they were: the
/// original shares four fields of the item (bytes 11 to 15) between its states, so a state's values stay in the item
/// after the settler has left it. They have no meaning in the activity that holds them. No rule reads or writes them: a change of
/// activity carries them over, and the exporters write them again so that the original's item is produced exactly
/// (<c>docs/rules/settlers.md</c>).
/// </summary>
/// <param name="Mode">Byte 11.</param>
/// <param name="Target">The word at 12.</param>
/// <param name="Heading">Byte 14.</param>
/// <param name="Next">Byte 15.</param>
public readonly record struct Residue(int Mode = 0, int Target = 0, int Heading = 0, int Next = 0);
