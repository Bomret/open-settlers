using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// What a settler is employed as (<c>docs/rules/settlers.md</c>). A settler keeps its identity when its job changes: a settler without a
/// job becomes a carrier when it takes a road. A job is a type (ADR 0016): each ported job is a sealed record of its own that declares its
/// stable name with its evidence (<see cref="JobNameAttribute"/>) and the number the original gives it (<see cref="OriginalCode"/>), a
/// <see cref="Knight"/> holds one name for each level, and an <see cref="UnportedJob"/> holds any other number as a raw field.
/// The generator writes <see cref="Name"/>, <see cref="NameEvidence"/>, <see cref="Named"/>, <see cref="Ported"/>, and <see cref="Of"/> from the
/// job types, and refuses two jobs with one name. A caller names a job by its type (<c>new Woodcutter()</c>, <c>is Woodcutter</c>) or by its
/// stable name through <see cref="Named"/>. No rule reads <see cref="OriginalCode"/>: the importer and the exporters do.
/// </summary>
public abstract partial record Job
{
    private protected Job() { }

    /// <summary>The number the original gives the job (byte 0 of the settler's item, bits 2 to 6); null for a job the original lacks.</summary>
    public abstract int? OriginalCode { get; }

    /// <summary>
    /// Whether the settler arrives at the end of a free walk home as the generic arrival of <c>0x4385ab</c> has it whatever
    /// the vector (the table at <c>0x437dfb</c>): all but the woodcutter, the fisher (<c>docs/rules/fishing.md</c>), the stonecutter, the farmer, and
    /// the knights, and the jobs the engine does not port that the table excepts. Those jobs' own branches hand over to it where the walk is a walk home. A job type that has a branch of its own overrides it.
    /// </summary>
    public virtual bool ArrivesHomeGenerically => true;

    /// <summary>
    /// The types of the data records the job's rules and looks read from the rule tables (<see cref="RuleTables.Get{T}"/>), declared in
    /// the job's own file. The importer refuses a program whose tables module for the job leaves one unread, so a job's data is
    /// never a silent default. Default: none.
    /// </summary>
    public virtual IReadOnlyList<System.Type> Data => [];

    /// <summary>The job's stable name; the raw number of an <see cref="UnportedJob"/>, which has no name.</summary>
    public sealed override string ToString() =>
        Name.Length > 0 ? Name : OriginalCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
}
