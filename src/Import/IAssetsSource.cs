using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.Import;

/// <summary>
/// The assets port (ADR 0017): the kinds of asset an install holds, as a source of game data lists them. The asset
/// check and the asset browser read assets through it and know no file or format.
/// </summary>
public interface IAssetsSource
{
    /// <summary>
    /// The kinds of asset, in the order the asset check visits them. Throws an <see cref="IOException"/> where the
    /// source has no usable data; the message is the reason the check and the browser report.
    /// </summary>
    IReadOnlyList<IAssetKind> Kinds();
}

/// <summary>One kind of asset of a source: a category name and the assets of that kind.</summary>
public interface IAssetKind
{
    /// <summary>The kind's category name in the browser and in the check's report; unique among kinds.</summary>
    string Name { get; }

    /// <summary>
    /// Lists the assets of this kind in the order the browser shows them. A listed file that is missing, or a kind
    /// that finds none of its files, is listed as an asset whose <see cref="Asset.Load"/> throws with the reason.
    /// <c>Enumerate</c> itself throws only for a failure that belongs to no file, an <see cref="IOException"/>,
    /// <see cref="InvalidDataException"/>, or <see cref="UnauthorizedAccessException"/>. Does not read the assets' contents.
    /// </summary>
    IEnumerable<Asset> Enumerate();
}
