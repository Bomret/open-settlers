namespace OpenSettlers.Domain;

/// <summary>
/// What a settler is employed as, by the number the original gives the job (<c>docs/rules/settlers.md</c>). A settler
/// keeps its identity when its job changes: a settler without a job becomes a carrier when it takes a road. A job is a type
/// (ADR 0016): each ported job is a sealed record of its own that declares its number, a <see cref="Knight"/> holds the
/// numbers 22 to 26, and an <see cref="UnportedJob"/> holds any other number. <see cref="Of"/>, generated from the job types,
/// finds the job of a number. The type names no job: a caller names a job by its type (<c>new Woodcutter()</c>,
/// <c>is Woodcutter</c>), and each job type's own file holds its number and what the original's tables say of it.
/// </summary>
public abstract partial record Job
{
    private protected Job() { }

    /// <summary>The job's number: byte 0 of the settler's item, bits 2 to 6.</summary>
    public abstract int Code { get; }

    /// <summary>The first job of the knights; the knights are the jobs 22 to 26.</summary>
    internal const int FirstKnight = 22;

    internal const int LastKnight = 26;

    /// <summary>Whether this is one of the knights' jobs.</summary>
    public bool IsKnight => Code is >= FirstKnight and <= LastKnight;

    /// <summary>Whether this is a job of a knight or of one of the jobs above (the castle's own handlers are not ported).</summary>
    public bool IsKnightOrAbove => Code >= FirstKnight;

    /// <summary>
    /// Whether the settler arrives at the end of a free walk home as the generic arrival of <c>0x4385ab</c> has it whatever
    /// the vector (the table at <c>0x437dfb</c>): all but the woodcutter, the jobs 7, 8, 11 (the settler of the type 1 hut, <c>docs/rules/settlers.md</c>), 14, and
    /// 20, and the knights. Those jobs' own branches hand over to it where the walk is a walk home. A job type that has a branch of its own overrides it.
    /// </summary>
    public virtual bool ArrivesHomeGenerically => true;

    /// <summary>The level of a knight's job: 0 for the first.</summary>
    public int KnightLevel => Code - FirstKnight;

    /// <summary>The job a knight takes when it is made a knight of the next level.</summary>
    public Job Promoted => Of(Code + 1);

    /// <summary>The job's number alone.</summary>
    public sealed override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
