using System.Diagnostics.CodeAnalysis;
using OpenSettlers.Import.HistoryEdition;

namespace OpenSettlers.Launch;

/// <summary>Opens the install the launch arguments name: the composition root's one use of <see cref="Install"/> for a launch.</summary>
public static class LaunchInstall
{
    /// <summary>What every problem text ends with: the two ways to name an install.</summary>
    public const string HowToGive = "pass --game <dir> or set SIEDLER_DIR";

    /// <summary>
    /// Opens the install the launch arguments name. Returns false with a <c>problem</c> that names what is wrong
    /// and both ways to give an install (<c>--game</c> and <c>SIEDLER_DIR</c>) when a launch argument is
    /// a problem, no install is given, the folder is missing, or it holds no <see cref="Install.Program"/>.
    /// </summary>
    public static bool TryOpen(
        LaunchArgs args,
        [NotNullWhen(true)] out Install? install,
        [NotNullWhen(false)] out string? problem
    )
    {
        install = null;
        if (args.Problems.Count > 0)
        {
            problem = $"{string.Join("; ", args.Problems)}: {HowToGive}";

            return false;
        }

        if (string.IsNullOrEmpty(args.Game))
        {
            problem = $"no install given: {HowToGive}";

            return false;
        }

        if (!Install.TryOpen(args.Game, out install, out var own))
        {
            problem = $"{own} ({HowToGive})";

            return false;
        }

        problem = null;

        return true;
    }

    /// <summary>
    /// The rule tables port of the install the launch arguments name. Reading it fails with the problem
    /// <see cref="TryOpen"/> returns where the launch names no usable install.
    /// </summary>
    public static IRuleTablesSource RuleTables(LaunchArgs args) =>
        TryOpen(args, out var install, out var problem)
            ? new HistoryEditionRuleTables(install)
            : new MissingInstallTables(problem);

    /// <summary>
    /// The assets port of the install the launch arguments name. Listing its kinds fails with the problem
    /// <see cref="TryOpen"/> returns where the launch names no usable install.
    /// </summary>
    public static IAssetsSource Assets(LaunchArgs args) =>
        TryOpen(args, out var install, out var problem)
            ? new HistoryEditionAssets(install)
            : new MissingInstallAssets(problem);
}
