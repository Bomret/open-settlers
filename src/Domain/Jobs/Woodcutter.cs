namespace OpenSettlers.Domain;

/// <summary>The woodcutter, who cuts trees and carries the logs to his hut's flag (<c>docs/rules/woodcutting.md</c>).</summary>
public sealed record Woodcutter : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 5;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;
}
