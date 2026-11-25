namespace OpenSettlers.Domain;

/// <summary>
/// One good of the <see cref="ContentCatalog"/> (ADR 0020): its stable name, its original code where it has one, and the facts the
/// rules read about it. A content set's source builds the entry; the rules read its facts through <see cref="RuleTables"/>.
/// </summary>
public sealed record GoodEntry
{
    /// <summary>
    /// The stable name, the entry's identity: lowercase letters, digits, and hyphens (<c>log</c>, <c>iron-ore</c>). The catalog orders
    /// its goods by it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The evidence for the name where the name stands for a good of the original (INV-0006); null for an entry of a set's own.</summary>
    public string? NameEvidence { get; init; }

    /// <summary>The number the original gives the good; null for an entry the original lacks. The source of the catalog sets it, the exporters read it, and the readout reads whether the entry has one.</summary>
    public int? OriginalCode { get; init; }

    /// <summary>
    /// The request the good looks for when it has no destination: its class and the request word whose flags ask for it; null for a good
    /// that goes to storage at once.
    /// </summary>
    public GoodClass? Request { get; init; }

    /// <summary>The byte of a building's fill the good counts against on its way, where a request counts it or a loss counts it back.</summary>
    public required FillCounter Counter { get; init; }

    /// <summary>The byte of a building's fill the carrier's handover of the good counts in.</summary>
    public required FillCounter HandoverCounter { get; init; }

    /// <summary>Whether a lost good of this kind counts against the total of resource 1.</summary>
    public required bool CountsAgainstResource1 { get; init; }

    /// <summary>The default transport priority: a rank from 1 up that no other good shares; the carrier lines up the good of the highest rank next.</summary>
    public required int TransportRank { get; init; }
}
