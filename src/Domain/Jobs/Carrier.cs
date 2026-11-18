namespace OpenSettlers.Domain;

/// <summary>The carrier, who takes a road and waits on it (<c>docs/rules/carriers.md</c>).</summary>
[JobName("carrier", "Carrier; docs/rules/carriers.md")]
public sealed record Carrier : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 0;

    /// <inheritdoc />
    public override int? OriginalCode => Number;
}
