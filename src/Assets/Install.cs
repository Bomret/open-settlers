using System.Diagnostics.CodeAnalysis;
using System.IO;
using OpenSettlers.Launch;

namespace OpenSettlers.Assets;

/// <summary>
/// An install of Die Siedler on disk: a folder that holds <c>Siedler1.exe</c>. Paths are relative to the root and
/// use <c>/</c>.
/// </summary>
public sealed class Install
{
    /// <summary>The program an install holds.</summary>
    public const string Program = "Siedler1.exe";

    /// <summary>What every problem text ends with: the two ways to name an install.</summary>
    public const string HowToGive = "pass --game <dir> or set SIEDLER_DIR";

    /// <summary>The install folder.</summary>
    public required string Root { get; init; }

    /// <summary>
    /// Opens the install the launch arguments name. Returns false with a <c>problem</c> that names what is wrong
    /// and both ways to give an install (<c>--game</c> and <c>SIEDLER_DIR</c>) when a launch argument is
    /// a problem, no install is given, the folder is missing, or it holds no <see cref="Program"/>.
    /// </summary>
    public static bool TryOpen(
        LaunchArgs args,
        [NotNullWhen(true)] out Install? install,
        [NotNullWhen(false)] out string? problem
    )
    {
        install = null;
        problem = null;
        if (args.Problems.Count > 0)
        {
            problem = $"{string.Join("; ", args.Problems)}: {HowToGive}";
        }
        else if (string.IsNullOrEmpty(args.Game))
        {
            problem = $"no install given: {HowToGive}";
        }
        else if (!Directory.Exists(args.Game))
        {
            problem = $"install folder not found: {args.Game} ({HowToGive})";
        }
        else if (!File.Exists(Path.Combine(args.Game, Program)))
        {
            problem = $"no {Program} in {args.Game}, so it is not an install ({HowToGive})";
        }
        else
        {
            install = new Install { Root = args.Game };
        }

        return install != null;
    }

    /// <summary>Whether the install holds a file at <c>path</c>.</summary>
    public bool Exists(string path) => File.Exists(Path.Combine(Root, path));

    /// <summary>The bytes of the file at <c>path</c>. Throws when it cannot be read.</summary>
    public byte[] ReadAll(string path) => File.ReadAllBytes(Path.Combine(Root, path));
}
