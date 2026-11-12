namespace OpenSettlers.Domain;

/// <summary>The sawmiller, who saws the logs the sawmill holds into planks and carries each plank out to the flag (<c>docs/rules/sawmilling.md</c>).</summary>
public sealed record Sawmiller : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = SawmillerCode;

    /// <inheritdoc />
    public override int Code => Number;
}
