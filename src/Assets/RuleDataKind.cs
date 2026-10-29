using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// The rule data of the game: entry 2, which the program reads at start-up (<c>0x41e8e0</c> through
/// <c>0x4073d0</c>) and hands to the simulation; see <c>docs/rules/graphics-archives.md</c>. The kind names the
/// entry and its location. What the data means is pending.
/// </summary>
public sealed class RuleDataKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "rule data";

    /// <summary>The entry that holds the rule data.</summary>
    public const int RuleDataEntry = 2;

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => entry == RuleDataEntry;

    /// <inheritdoc/>
    protected override string Label(string file, ArchiveEntry entry) =>
        $"{base.Label(file, entry)}: offset {entry.Offset}, size {entry.Size}, meaning pending";

    /// <inheritdoc/>
    protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        new LocatedEntryPreview
        {
            Entry = entry,
            Summary =
                $"Rule data of entry {entry.Number}: offset {entry.Offset}, size {entry.Size}. The program reads it at start-up; meaning pending.",
        };
}
