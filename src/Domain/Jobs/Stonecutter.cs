namespace OpenSettlers.Domain;

/// <summary>The stonecutter, who cuts stone heaps and carries the stones to his hut's flag (<c>docs/rules/stonecutting.md</c>).</summary>
public sealed record Stonecutter : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = StonecutterCode;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;
}
