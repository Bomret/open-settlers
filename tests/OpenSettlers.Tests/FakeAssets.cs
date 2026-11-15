using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>An assets port over kinds a test hands it: no install, no file.</summary>
public sealed class FakeAssets(params IAssetKind[] kinds) : IAssetsSource
{
    public IReadOnlyList<IAssetKind> Kinds() => kinds;
}

/// <summary>An assets port with no usable data: its kinds throw the reason, as a launch with no install does.</summary>
public sealed class UnusableAssets(string reason) : IAssetsSource
{
    public IReadOnlyList<IAssetKind> Kinds() => throw new IOException(reason);
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
            ? new FakeAssets([.. kinds.Select(k => (IAssetKind)new Bound(k, install))])
            : new UnusableAssets(problem);

    sealed class Bound(AssetKind kind, Install install) : IAssetKind
    {
        public string Name => kind.Name;

        public IEnumerable<Asset> Enumerate() => kind.Enumerate(install);
    }
}

/// <summary>A preview type of a test's own: one no view draws.</summary>
public sealed record NotePreview : Preview;
