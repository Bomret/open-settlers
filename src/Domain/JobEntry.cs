namespace OpenSettlers.Domain;

/// <summary>
/// One job of the <see cref="ContentCatalog"/> (ADR 0020): its stable name and its original code where it has one. The job's rules and
/// looks hang on its type (ADR 0016), so an entry holds no fact of the rules. A content set's source builds the entry.
/// </summary>
public sealed record JobEntry
{
    /// <summary>
    /// The stable name, the entry's identity: lowercase letters, digits, and hyphens (<c>woodcutter</c>). It is the name <see cref="Job.Name"/>
    /// gives the job, and a readout and a play script find the job by it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The evidence for the name where the name stands for a job of the original (INV-0006); null for an entry of a set's own.</summary>
    public string? NameEvidence { get; init; }

    /// <summary>The number the original gives the job; null for an entry the original lacks. The History Edition importer and the exporters read it.</summary>
    public int? OriginalCode { get; init; }
}
