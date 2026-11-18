using System.Collections.Generic;
using OpenSettlers.Import.HistoryEdition;
using static OpenSettlers.Testing.GraphicEntries;

namespace OpenSettlers.Tests;

/// <summary>
/// The entries of the object graphics archive the tests build in code. It stays with the tests because it names the
/// internal entry numbers of the History Edition source (<c>ObjectSprites</c>).
/// </summary>
static class ObjectPayloads
{
    /// <summary>The palette, two object sprites, and the castle's sprite; with <paramref name="flag"/>, the flag's too.</summary>
    public static Dictionary<int, byte[]> Payloads(bool flag = false)
    {
        var payloads = new Dictionary<int, byte[]>
        {
            [3] = Palette(),
            [1250] = Sprite(),
            [1251] = Square(5, 6),
            [1369] = Sprite(),
            [ObjectSprites.CastleEntry] = Square(7, 8),
        };
        if (flag)
        {
            payloads[ObjectSprites.FlagEntry] = Square(9, 4);
        }

        return payloads;
    }
}
