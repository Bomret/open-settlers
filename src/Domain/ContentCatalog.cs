using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>
/// The engine's list of the goods a game knows, one <see cref="GoodEntry"/> each (ADR 0020). A content set's source builds it, and
/// <see cref="RuleTables"/> holds it. It orders its goods by stable name, ordinal, whatever order the source gives them in, so a game plays
/// the same on every run (INV-0002). "No good" is never an entry.
/// </summary>
public sealed class ContentCatalog
{
    /// <summary>The number of request classes a request word holds: a word is one byte of classes.</summary>
    const int ClassesPerWord = 8;

    readonly Dictionary<string, GoodEntry> _byName;

    /// <summary>
    /// Creates the catalog from its goods. Throws an <see cref="ArgumentException"/> that names the entry and builds nothing for a duplicate
    /// stable name, a duplicate original code, a stable name outside lowercase letters, digits, and hyphens, a transport rank below 1 or
    /// one that another good holds, and a request class that does not fit its request word (0 to 7).
    /// </summary>
    /// <param name="goods">The goods, in any order.</param>
    public ContentCatalog(IEnumerable<GoodEntry> goods)
    {
        var ordered = goods.OrderBy(good => good.Name, StringComparer.Ordinal).ToList();
        _byName = [];
        Dictionary<int, GoodEntry> byCode = [];
        var byRank = new Dictionary<int, string>();
        foreach (var good in ordered)
        {
            if (good.Name.Length == 0 || !good.Name.All(IsNameCharacter))
            {
                throw Refuse(good, "its stable name holds anything other than lowercase letters, digits, and hyphens");
            }

            if (!_byName.TryAdd(good.Name, good))
            {
                throw Refuse(good, "another good has this stable name");
            }

            if (good.OriginalCode is { } code && !byCode.TryAdd(code, good))
            {
                throw Refuse(good, $"{byCode[code].Name} has this original code, {code}");
            }

            if (good.TransportRank < 1)
            {
                throw Refuse(good, $"its transport rank {good.TransportRank} is below 1");
            }

            if (!byRank.TryAdd(good.TransportRank, good.Name))
            {
                throw Refuse(good, $"{byRank[good.TransportRank]} has this transport rank, {good.TransportRank}");
            }

            if (good.Request is { } request && request.Class is < 0 or >= ClassesPerWord)
            {
                throw Refuse(
                    good,
                    $"its request class {request.Class} does not fit a request word, which holds the classes 0 to {ClassesPerWord - 1}"
                );
            }
        }

        Goods = [.. ordered];
    }

    /// <summary>A catalog with no good.</summary>
    public static ContentCatalog Empty { get; } = new([]);

    /// <summary>The goods in ordinal order of their stable names.</summary>
    public ImmutableArray<GoodEntry> Goods { get; }

    /// <summary>
    /// The entry of <paramref name="good"/>, the one with its stable name; null for no good and for a good the catalog lacks.
    /// </summary>
    public GoodEntry? EntryOf(Good good) => good.IsNone ? null : _byName.GetValueOrDefault(good.Name);

    /// <summary>
    /// The entry of <paramref name="good"/>. Throws an <see cref="InvalidOperationException"/> that names the good where the catalog
    /// lacks it: a rule that meets a good its game's catalog lacks stops there. No good is no entry and throws too.
    /// </summary>
    public GoodEntry Require(Good good) =>
        EntryOf(good)
        ?? throw new InvalidOperationException(
            $"The good '{good.Name}' is no entry of the content catalog, which holds: {string.Join(", ", Goods.Select(entry => entry.Name))}."
        );

    static bool IsNameCharacter(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';

    static ArgumentException Refuse(GoodEntry good, string reason) =>
        new($"The good '{good.Name}' is refused: {reason}.");
}
