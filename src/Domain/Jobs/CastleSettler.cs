namespace OpenSettlers.Domain;

/// <summary>The castle's own first settler, who stands by while the castle is built and carries the goods the castle hands out onto its flag (<c>docs/rules/castle.md</c>).</summary>
public sealed record CastleSettler : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 4;

    /// <inheritdoc />
    public override int Code => Number;
}
