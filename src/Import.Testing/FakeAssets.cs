using System;
using System.Collections.Generic;

namespace OpenSettlers.Import.Testing;

/// <summary>An assets port over kinds a test hands it: no install, no file.</summary>
public sealed class FakeAssets(params IAssetKind[] kinds) : IAssetsSource
{
    public IReadOnlyList<IAssetKind> Kinds() => kinds;
}

/// <summary>A kind that lists the assets the test hands it, or throws what it is told to when it lists.</summary>
public sealed class ListedKind(string name, params Asset[] assets) : IAssetKind
{
    public Exception? ListingFails { get; init; }

    public string Name => name;

    public IEnumerable<Asset> Enumerate() => ListingFails is { } e ? throw e : assets;
}

/// <summary>A preview type of a test's own: one no view draws.</summary>
public sealed record NotePreview : Preview;
