using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// How the exporters write a building type (ADR 0020, INV-0008, INV-0014): the original's code where the type's entry has one, and for an
/// entry with no code the word of the FNV-1a hash of its stable name that <see cref="GoodWords"/> writes for a good: the hash's low 31 bits
/// with the low byte cleared, and 0x100 where that leaves 0. So it never equals the code of an original type (0 to 0x18), the type of no
/// building (-1), or a word with the constructing bit 0x20, which the exporter adds to the word of a building under construction.
/// </summary>
public static class BuildingWords
{
    /// <summary>The word of the type of no building, as the building table's item holds it.</summary>
    public const int NoBuilding = -1;

    /// <summary>
    /// The number an exporter writes for <paramref name="type"/>: -1 for no building, the original's code of its entry, or the word of the
    /// hash of its stable name for an entry with no code. Throws for a type the catalog lacks, naming it.
    /// </summary>
    public static int Of(ContentCatalog catalog, BuildingType type)
    {
        if (type.IsNone)
        {
            return NoBuilding;
        }

        var entry = catalog.Require(type);

        return entry.OriginalCode ?? GoodWords.WordOf(entry.Name);
    }

    /// <summary>
    /// Whether <paramref name="type"/> has a number of the original, which a readout shows beside its name: a type with no original code
    /// shows its name alone (INV-0006).
    /// </summary>
    public static bool HasCode(ContentCatalog catalog, BuildingType type) =>
        !type.IsNone && catalog.Require(type).OriginalCode is not null;

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> that names both types where two building types of the catalog with no original code have one
    /// word, which the building table's item and the digest could not tell apart.
    /// </summary>
    public static void Check(ContentCatalog catalog) =>
        GoodWords.CheckCodeless(
            "building types",
            catalog.Buildings.Where(entry => entry.OriginalCode is null).Select(entry => entry.Name),
            nameof(catalog)
        );
}
