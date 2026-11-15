using System;

namespace OpenSettlers.Tests;

/// <summary>A rule tables port that answers from a function, so a view or a command runs with no install on disk.</summary>
public sealed class FakeRuleTables(Func<RuleTables> read) : IRuleTablesSource
{
    /// <summary>The port of the made-up tables of <see cref="TestProgram.Made()"/>.</summary>
    public static FakeRuleTables Made { get; } = new(TestProgram.Made);

    /// <inheritdoc />
    public RuleTables Read() => read();
}
