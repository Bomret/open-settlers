using System;
using System.IO;

namespace OpenSettlers.Import;

/// <summary>
/// The rule tables port (ADR 0017): the domain's <see cref="RuleTables"/> as a source of game data holds them. A view or a
/// command asks for the tables through it and knows no program or install.
/// </summary>
public interface IRuleTablesSource
{
    /// <summary>
    /// Reads the rule tables. Throws an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> where the data
    /// cannot be read, and an <see cref="InvalidDataException"/> where it does not hold the tables as expected.
    /// </summary>
    RuleTables Read();
}
