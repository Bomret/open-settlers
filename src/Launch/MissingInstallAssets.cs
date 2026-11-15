using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.Launch;

/// <summary>
/// The assets port of a launch that names no usable install: listing its kinds fails with the problem of the launch,
/// so the asset check and the asset browser show that text.
/// </summary>
/// <param name="problem">What is wrong with the install the launch names.</param>
sealed class MissingInstallAssets(string problem) : IAssetsSource
{
    /// <inheritdoc />
    public IReadOnlyList<IAssetKind> Kinds() => throw new IOException(problem);
}
