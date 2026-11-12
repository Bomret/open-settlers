namespace OpenSettlers.Domain;

/// <summary>The builder, who walks out to a building site and builds it from the planks and stones on site (<c>docs/rules/construction.md</c>).</summary>
public sealed record Builder : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = BuilderCode;

    /// <inheritdoc />
    public override int Code => Number;
}
