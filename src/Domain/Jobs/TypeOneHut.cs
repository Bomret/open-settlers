namespace OpenSettlers.Domain;

/// <summary>The job of the settler of the type 1 hut, whose work looks for a free node by the water (<c>docs/rules/settlers.md</c>).</summary>
public sealed record TypeOneHut : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 11;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;
}
