using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>The preview of an entry whose bytes the browser does not show (rule data, an entry no routine reads): where it lies. It has no byte preview.</summary>
public sealed record LocatedEntryPreview : Preview
{
    /// <summary>The entry.</summary>
    public required ArchiveEntry Entry { get; init; }
}
