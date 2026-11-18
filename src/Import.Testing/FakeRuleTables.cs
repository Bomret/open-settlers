using System;
using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;

namespace OpenSettlers.Import.Testing;

/// <summary>A rule tables port that answers from a function, so a view or a command runs with no install on disk.</summary>
public sealed class FakeRuleTables(Func<RuleTables> read) : IRuleTablesSource
{
    /// <summary>The port of the made-up tables of <see cref="MadeProgram.Made()"/>.</summary>
    public static FakeRuleTables Made { get; } = new(MadeProgram.Made);

    /// <inheritdoc />
    public RuleTables Read() => read();
}
