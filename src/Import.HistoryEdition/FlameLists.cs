using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The flames the original draws over a burning building, read from <c>Gfx_SpriteLists</c> (<c>0x44e114</c>) of the
/// program (<c>docs/rules/removal.md</c>, "The burning draw"). The data starts with 25 word offsets from its own start, one
/// for each list; a list holds 3 bytes for each flame, a size byte (0 small, 8 large) and a signed x and y offset, and a
/// byte of 0x80 or more ends it. A building takes the list of its type's number, and a site below 16000 progress list 0.
/// Only the History Edition source reads it.
/// </summary>
sealed class FlameLists
{
    /// <summary>The address of the lists' header in the program.</summary>
    public const int Address = 0x44e114;

    /// <summary>The number of lists, one for each building type 0 to 24.</summary>
    public const int ListCount = 25;

    /// <summary>The sprite entry of a small flame at phase 0, the first of the 16 flame entries (456 to 471).</summary>
    public const int FirstEntry = 136 + 320;

    // The size byte of a large flame; a small flame's is 0.
    const byte LargeSize = 8;

    // A byte of this value or more in a list's size byte ends the list.
    const byte EndOfList = 0x80;

    const int FlameBytes = 3;

    readonly IReadOnlyList<Flame>[] _lists;

    FlameLists(IReadOnlyList<Flame>[] lists) => _lists = lists;

    /// <summary>
    /// Reads the lists from the program's bytes. Throws <see cref="System.IO.InvalidDataException"/> where the header or a
    /// list lies outside the program's image: a header offset past it, or a list that runs past it unterminated, and where a
    /// flame's size byte is neither 0 nor 8.
    /// </summary>
    public static FlameLists Read(byte[] exe)
    {
        var header = ProgramTablesImporter.Slice(exe, Address, 2 * ListCount);
        var lists = new IReadOnlyList<Flame>[ListCount];
        for (var list = 0; list < ListCount; list++)
        {
            var at = Address + BitConverter.ToInt16(header, 2 * list);
            var flames = new List<Flame>();
            while (ProgramTablesImporter.Slice(exe, at, 1)[0] < EndOfList)
            {
                var flame = ProgramTablesImporter.Slice(exe, at, FlameBytes);
                if (flame[0] is not (0 or LargeSize))
                {
                    throw new InvalidDataException(
                        $"flame list {list} holds the size byte {flame[0]} at 0x{at:x}, neither 0 nor {LargeSize}"
                    );
                }

                flames.Add(
                    new Flame
                    {
                        Size = flame[0] == LargeSize ? FlameSize.Large : FlameSize.Small,
                        X = (sbyte)flame[1],
                        Y = (sbyte)flame[2],
                    }
                );
                at += FlameBytes;
            }

            lists[list] = flames;
        }

        return new FlameLists(lists);
    }

    /// <summary>
    /// The flames of a burning building of <paramref name="type"/>: list 0 for an early site, else the list of the type's
    /// original code (<see cref="BuildingTables.CodeOfName"/>); none for a type with no code or a code outside 0 to 24.
    /// </summary>
    public IReadOnlyList<Flame> Of(BuildingType type, bool earlySite)
    {
        if (earlySite)
        {
            return _lists[0];
        }

        return BuildingTables.CodeOfName.TryGetValue(type.Name, out var code) && code < ListCount ? _lists[code] : [];
    }

    /// <summary>The sprite entry of a flame (<c>0x44e329</c>): 136, plus its size byte, plus its phase, plus the 320 that <c>Gfx_DrawSpriteBase320</c> adds.</summary>
    public static int Entry(FlameSize size, int phase) =>
        FirstEntry + (size == FlameSize.Large ? LargeSize : 0) + (phase & 7);
}
