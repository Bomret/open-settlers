using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>
/// The kinds of the History Edition's assets port that the install the launch arguments name gives, narrowed to the kinds
/// of the given names, in the order given, so a test sees the report of those kinds alone. The port is the launch library's public one.
/// </summary>
static class NamedKinds
{
    /// <summary>The port over the kinds of the names, bound to the install the arguments name.</summary>
    public static IAssetsSource Over(LaunchArgs args, params string[] names) =>
        new Narrowed(LaunchInstall.Assets(args), names);

    sealed class Narrowed(IAssetsSource inner, string[] names) : IAssetsSource
    {
        public IReadOnlyList<IAssetKind> Kinds()
        {
            var all = inner.Kinds();

            return [.. names.Select(name => all.Single(kind => kind.Name == name))];
        }
    }
}
