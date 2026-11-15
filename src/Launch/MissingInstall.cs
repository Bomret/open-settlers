using System.Collections.Generic;
using System.IO;
using OpenSettlers.Domain;

namespace OpenSettlers.Launch;

/// <summary>
/// The rule tables port and the assets port of a launch that names no usable install: reading the tables or listing the kinds
/// fails with the problem of the launch, so the view or command that asks shows that text.
/// </summary>
/// <param name="problem">What is wrong with the install the launch names.</param>
sealed class MissingInstall(string problem) : IRuleTablesSource, IAssetsSource
{
    /// <inheritdoc />
    public RuleTables Read() => throw new IOException(problem);

    /// <inheritdoc />
    public IReadOnlyList<IAssetKind> Kinds() => throw new IOException(problem);
}
