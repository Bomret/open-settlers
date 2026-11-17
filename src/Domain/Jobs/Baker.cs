using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The baker, who bakes the flour the bakery holds into bread and carries each loaf out to the flag (<c>docs/rules/baking.md</c>).</summary>
public sealed record Baker : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 16;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FoodHutFigureAdds)];
}
