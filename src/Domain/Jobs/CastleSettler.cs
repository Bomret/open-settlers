namespace OpenSettlers.Domain;

/// <summary>The castle's own first settler, who stands by while the castle is built and carries the goods the castle hands out onto its flag (<c>docs/rules/castle.md</c>).</summary>
[JobName(
    "castle-settler",
    "CastleSettler; the castle's first settler stands by while the castle is built (state 10, 0x43f17a) and no building asks for job 4; Game.Removal reads job 4 as a settler that goes home as a carrier; docs/rules/settlers.md"
)]
public sealed record CastleSettler : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 4;

    /// <inheritdoc />
    public override int? OriginalCode => Number;
}
