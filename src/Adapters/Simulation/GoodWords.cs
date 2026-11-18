using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// How the exporters write a good (ADR 0020, INV-0008, INV-0014): the original's code where the good's entry has one, and for an entry
/// with no code the exporter word of its stable name (<see cref="ExporterWords"/>): the hash's low 31 bits with the low byte cleared, and
/// 0x100 where that leaves 0. So it never equals the code of an original good (1 to 26), no good (0),
/// or the carrier's storage and building modes (0xfe and 0xff), and a packed byte that holds a good in its low bits and a direction above
/// them keeps its direction bits when the exporter adds them to the word.
/// </summary>
public static class GoodWords
{
    /// <summary>
    /// The number an exporter writes for <paramref name="good"/>: 0 for no good, the original's code of its entry, or the word of the hash of
    /// its stable name for an entry with no code. Throws for a good the catalog lacks, naming it.
    /// </summary>
    public static int Of(ContentCatalog catalog, Good good)
    {
        if (good.IsNone)
        {
            return 0;
        }

        var entry = catalog.Require(good);

        return entry.OriginalCode ?? ExporterWords.Of(entry.Name);
    }

    /// <summary>
    /// Whether <paramref name="word"/> is the word of a good of the catalog that has no original code. The readout leaves such a word out
    /// and shows the good by name (INV-0006).
    /// </summary>
    public static bool IsCodelessWord(ContentCatalog catalog, int word) =>
        catalog.Goods.Any(entry => entry.OriginalCode is null && ExporterWords.Of(entry.Name) == word);

    /// <summary>
    /// The text an event writes for <paramref name="good"/>: the number <see cref="Of"/> writes for a good with an original code and for no
    /// good (<c>7</c>, <c>0</c>), and the stable name for a good with no code.
    /// </summary>
    public static string Text(ContentCatalog catalog, Good good) =>
        good.IsNone || catalog.Require(good).OriginalCode is not null
            ? Of(catalog, good).ToString(CultureInfo.InvariantCulture)
            : good.Name;
}
