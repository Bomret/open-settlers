using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// How the exporters write a good (ADR 0020, INV-0008, INV-0014): the original's code where the good's entry has one, and for an entry
/// with no code a word made of the FNV-1a hash of its stable name (<see cref="Fnv1a.Of"/>). The word of a code-less good is the hash's low
/// 31 bits with the low byte cleared, and 0x100 where that leaves 0. So it never equals the code of an original good (1 to 26), no good (0),
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

        return entry.OriginalCode ?? WordOf(entry.Name);
    }

    // The word of a good with no original code, as the class summary states it. The building types use the same word
    // (BuildingWords): its low byte is clear, so it holds neither the constructing bit 0x20 nor an original code.
    internal static int WordOf(string name)
    {
        var word = (int)(Fnv1a.Of(name) & 0x7fffff00);

        return word == 0 ? 0x100 : word;
    }

    /// <summary>
    /// Whether <paramref name="word"/> is the word of a good of the catalog that has no original code. The readout leaves such a word out
    /// and shows the good by name (INV-0006).
    /// </summary>
    public static bool IsCodelessWord(ContentCatalog catalog, int word) =>
        catalog.Goods.Any(entry => entry.OriginalCode is null && WordOf(entry.Name) == word);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> that names both goods where two goods of the catalog with no original code have one word,
    /// which the flag word, the carrier's mode, and the snapshot could not tell apart.
    /// </summary>
    public static void Check(ContentCatalog catalog)
    {
        var byWord = new Dictionary<int, string>();
        foreach (var entry in catalog.Goods.Where(entry => entry.OriginalCode is null))
        {
            var word = WordOf(entry.Name);
            if (!byWord.TryAdd(word, entry.Name))
            {
                throw new ArgumentException(
                    $"The goods {byWord[word]} and {entry.Name} have no original code and the same exporter word, 0x{word:x}.",
                    nameof(catalog)
                );
            }
        }
    }

    /// <summary>
    /// The text an event writes for <paramref name="good"/>: the number <see cref="Of"/> writes for a good with an original code and for no
    /// good (<c>7</c>, <c>0</c>), and the stable name for a good with no code.
    /// </summary>
    public static string Text(ContentCatalog catalog, Good good) =>
        good.IsNone || catalog.Require(good).OriginalCode is not null
            ? Of(catalog, good).ToString(CultureInfo.InvariantCulture)
            : good.Name;
}
