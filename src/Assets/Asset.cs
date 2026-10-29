using System;

namespace OpenSettlers.Assets;

/// <summary>
/// What an asset's preview shows, as plain data. The Godot view of the kind draws it
/// (<c>src/Viewer/PreviewView.cs</c>); a kind without a view shows <see cref="Summary"/>.
/// </summary>
public abstract record Preview
{
    /// <summary>One or a few lines of text that describe the asset.</summary>
    public required string Summary { get; init; }
}

/// <summary>
/// One asset of an install, as an <see cref="AssetKind"/> lists it. The asset check and the asset browser read
/// it through <see cref="Load"/>.
/// </summary>
public sealed record Asset
{
    /// <summary>The name of the asset's <see cref="AssetKind"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>The install file that holds the asset, relative to the install root, with <c>/</c>.</summary>
    public required string File { get; init; }

    /// <summary>The asset's entry in <see cref="File"/>; null when the file is one asset.</summary>
    public string? Entry { get; init; }

    /// <summary>
    /// Whether the asset is an archive slot that the program fills with a copy of another entry. The check counts
    /// aliases apart from entries.
    /// </summary>
    public bool IsAlias { get; init; }

    /// <summary>The asset's name in the browser's list.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Reads the asset and returns its preview. Throws when the asset cannot be read (an
    /// <see cref="System.IO.InvalidDataException"/> for corrupt data); the exception's message is the reason the
    /// check reports and the browser shows. The same method stands for a failure the kind found while listing,
    /// such as a missing file.
    /// </summary>
    public required Func<Preview> Load { get; init; }
}
