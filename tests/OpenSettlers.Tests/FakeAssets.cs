using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

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

/// <summary>The real kinds a test hands it, bound to the install the launch arguments name.</summary>
static class RealKinds
{
    /// <summary>The port over <c>kinds</c> and that install; where the launch names no usable install, the port is unusable with the launch's problem.</summary>
    public static IAssetsSource Over(LaunchArgs args, params AssetKind[] kinds) =>
        LaunchInstall.TryOpen(args, out var install, out var problem)
            ? new FakeAssets([.. kinds.Select(k => (IAssetKind)new HistoryEditionAssets.BoundKind(k, install))])
            : new MissingInstall(problem);
}

/// <summary>A preview type of a test's own: one no view draws.</summary>
public sealed record NotePreview : Preview;
