using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The assets port of the History Edition install (ADR 0017): one kind per file family of the install, each bound to
/// the install it lists.
/// </summary>
public sealed class HistoryEditionAssets(Install install) : IAssetsSource
{
    /// <inheritdoc/>
    public IReadOnlyList<IAssetKind> Kinds() =>
        AssetKind.Discover().Select(k => (IAssetKind)new BoundKind(k, install)).ToList();

    internal sealed class BoundKind(AssetKind kind, Install install) : IAssetKind
    {
        public string Name => kind.Name;

        public IEnumerable<Asset> Enumerate() => kind.Enumerate(install);
    }
}
