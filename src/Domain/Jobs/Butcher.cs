using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The butcher, who turns each pig the butcher's shop holds into meat and carries it out to the flag (<c>docs/rules/butchering.md</c>).</summary>
public sealed record Butcher : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 13;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FoodHutFigureAdds)];
}
