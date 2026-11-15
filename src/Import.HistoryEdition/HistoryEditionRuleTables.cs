namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The rule tables port of a History Edition install (ADR 0017): the tables <see cref="ProgramTablesImporter"/> reads from the
/// install's <c>Siedler1.exe</c> (INV-0001).
/// </summary>
/// <param name="install">The install whose program holds the tables.</param>
public sealed class HistoryEditionRuleTables(Install install) : IRuleTablesSource
{
    /// <inheritdoc />
    public RuleTables Read() => ProgramTablesImporter.Import(install.ReadAll(Install.Program));
}
