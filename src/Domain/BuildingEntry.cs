using System.Collections.Immutable;

namespace OpenSettlers.Domain;

/// <summary>
/// One building type of the <see cref="ContentCatalog"/> (ADR 0020): its stable name, its original code where it has one, and the facts
/// the rules read about it. A content set's source builds the entry; the rules read its facts through <see cref="RuleTables"/>.
/// </summary>
public sealed record BuildingEntry
{
    /// <summary>
    /// The stable name, the entry's identity: lowercase letters, digits, and hyphens (<c>woodcutter-hut</c>). The catalog orders its
    /// building types by it, and a layout names a building by it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The evidence for the name where the name stands for a type of the original (INV-0006); null for an entry of a set's own.</summary>
    public string? NameEvidence { get; init; }

    /// <summary>The number the original gives the type; null for an entry the original lacks. A source of the original's data sets it, the exporters read it, and the readout reads whether the entry has one.</summary>
    public int? OriginalCode { get; init; }

    /// <summary>
    /// The rank that breaks a tie between two building types, the lower first: two commands of one tick that differ only in the type they
    /// place come in its order. No other type of the catalog holds it. A source of the original's data gives a type its original code, the
    /// order of the original; a set of its own states it in its data.
    /// </summary>
    public required int TieRank { get; init; }

    /// <summary>How much room the type takes (the size table at <c>0x42d307</c>).</summary>
    public required BuildingSize Size { get; init; }

    /// <summary>The planks and stones a site of the type needs (<c>0x449f18</c>).</summary>
    public required BuildingNeeds Needs { get; init; }

    /// <summary>The speed of the door of the finished building, in 32nds of a step time (<c>0x43c92b</c>).</summary>
    public required byte StandingDoor { get; init; }

    /// <summary>The speed of the door of the site while the building is built, in 32nds of a step time (<c>0x43c92b</c>).</summary>
    public required byte ConstructingDoor { get; init; }

    /// <summary>What the finished building asks for as its worker (<c>0x42cd1d</c>); null for a type that asks for none.</summary>
    public WorkerRequest? Worker { get; init; }

    /// <summary>The materials the builder takes in turn, true for a stone and false for a plank (<c>0x43f0ba</c>); a turn past the list takes a plank.</summary>
    public ImmutableArray<bool> Stones { get; init; } = [];

    /// <summary>What a blow of the builder adds to the progress word before the word reaches <c>0x8000</c> (<c>0x43f0fa</c>).</summary>
    public required ushort EarlyBlowStep { get; init; }

    /// <summary>What a blow of the builder adds to the progress word from <c>0x8000</c> on (<c>0x43f0fa</c>).</summary>
    public required ushort LateBlowStep { get; init; }

    /// <summary>Whether the site's builder takes a second figure (<c>0x43ceb4</c>).</summary>
    public required bool SecondFigure { get; init; }

    /// <summary>Whether the type is a storage: a finished one counts no good on its way (<c>docs/rules/removal.md</c>).</summary>
    public required bool IsStorage { get; init; }
}
