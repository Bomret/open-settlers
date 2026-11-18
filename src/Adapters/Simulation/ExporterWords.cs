using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The word an exporter writes for a catalog entry with no original code, whatever its kind (<see cref="GoodWords"/>,
/// <see cref="BuildingWords"/>, <see cref="JobWords"/>; ADR 0020): the FNV-1a hash of its stable name (<see cref="Fnv1a.Of"/>), its low 31
/// bits with the low byte cleared, and 0x100 where that leaves 0. Its low byte is clear, so it holds neither an original code nor the
/// constructing bit 0x20 of a building type, and a packed byte that holds an entry in its low bits keeps the bits above them.
/// </summary>
static class ExporterWords
{
    /// <summary>The word of the entry with the stable name <paramref name="name"/>.</summary>
    internal static int Of(string name)
    {
        var word = (int)(Fnv1a.Of(name) & 0x7fffff00);

        return word == 0 ? 0x100 : word;
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> that names both entries (<paramref name="kind"/> says what they are) where two of the
    /// stable names <paramref name="names"/> of entries with no original code have one exporter word, which the words an exporter writes
    /// could not tell apart. The exception names <paramref name="parameter"/> as the argument at fault: the caller's parameter that holds
    /// the catalog.
    /// </summary>
    internal static void CheckCodeless(string kind, IEnumerable<string> names, string parameter)
    {
        var byWord = new Dictionary<int, string>();
        foreach (var name in names)
        {
            var word = Of(name);
            if (!byWord.TryAdd(word, name))
            {
                throw new ArgumentException(
                    $"The {kind} {byWord[word]} and {name} have no original code and the same exporter word, 0x{word:x}.",
                    parameter
                );
            }
        }
    }
}
