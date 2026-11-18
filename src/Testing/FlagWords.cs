using System;
using System.Globalization;

namespace OpenSettlers.Testing;

/// <summary>The flag words of the original's items in the engine's numbering.</summary>
public static class FlagWords
{
    /// <summary>
    /// The verb goods numbers the flags one higher than the engine (the dummy of the original's game start, docs/rules/goods.md).
    /// The word of the bytes at the hex offset holds a flag in the settler's states that walk to a flag (2, 5, 6, 0xf)
    /// and in the hut's item; there it is the engine's slot plus one, and 0 where there is none. A word beyond the
    /// flags (the engine has flagCount) is no flag, and neither is the word of a settler in another state.
    /// </summary>
    public static string InEngineNumbers(string bytes, int offset, int flagCount)
    {
        var isFlagWord = offset == 12 || Convert.ToInt32(bytes[20..22], 16) is 2 or 5 or 6 or 0xf;
        var word = Convert.ToInt32(string.Concat(bytes.AsSpan(offset + 2, 2), bytes.AsSpan(offset, 2)), 16);

        return isFlagWord && word >= 1 && word <= flagCount
            ? string.Concat(
                bytes.AsSpan(0, offset),
                (word - 1).ToString("X2", CultureInfo.InvariantCulture),
                bytes.AsSpan(offset + 2)
            )
            : bytes;
    }
}
