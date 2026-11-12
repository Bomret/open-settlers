namespace OpenSettlers.Domain;

/// <summary>The forester, who plants saplings on free meadow nodes around his hut (<c>docs/rules/forestry.md</c>).</summary>
public sealed record Forester : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = ForesterCode;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;
}
