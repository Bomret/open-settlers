using System.Linq;
using OpenSettlers.Import.Testing;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>
/// The real kinds a test hands it, bound to the install the launch arguments name. It stays with the tests because
/// <c>MissingInstall</c>, which it returns for a launch with no usable install, is internal to the launch library.
/// </summary>
static class RealKinds
{
    /// <summary>The port over <c>kinds</c> and that install; where the launch names no usable install, the port is unusable with the launch's problem.</summary>
    public static IAssetsSource Over(LaunchArgs args, params AssetKind[] kinds) =>
        LaunchInstall.TryOpen(args, out var install, out var problem)
            ? new FakeAssets([.. kinds.Select(k => (IAssetKind)new HistoryEditionAssets.BoundKind(k, install))])
            : new MissingInstall(problem);
}
