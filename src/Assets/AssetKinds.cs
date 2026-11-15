using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Assets;

/// <summary>What the asset check and the asset browser ask of the kinds an assets port lists.</summary>
public static class AssetKinds
{
    /// <summary>
    /// The problem with <c>kinds</c> that stops the check and the browser: two kinds of one name. Null when every
    /// name is unique.
    /// </summary>
    public static string? NameClash(IEnumerable<IAssetKind> kinds) =>
        kinds.GroupBy(k => k.Name).FirstOrDefault(g => g.Count() > 1) is { } twice
            ? $"two asset kinds are named \"{twice.Key}\"; a kind's name must be unique"
            : null;
}
