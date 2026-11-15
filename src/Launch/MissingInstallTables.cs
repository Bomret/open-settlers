using System.IO;

namespace OpenSettlers.Launch;

/// <summary>
/// The rule tables port of a launch that names no usable install: reading it fails with the problem of the launch, so the
/// view or command that asks shows that text.
/// </summary>
/// <param name="problem">What is wrong with the install the launch names.</param>
sealed class MissingInstallTables(string problem) : IRuleTablesSource
{
    /// <inheritdoc />
    public RuleTables Read() => throw new IOException(problem);
}
