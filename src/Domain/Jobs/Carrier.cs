namespace OpenSettlers.Domain;

/// <summary>The carrier, who takes a road and waits on it (<c>docs/rules/carriers.md</c>).</summary>
public sealed record Carrier : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = CarrierCode;

    /// <inheritdoc />
    public override int Code => Number;
}
