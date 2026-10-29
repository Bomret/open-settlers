using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
        else if (Find(args.Game, Program) == null)
        {
            problem = $"no {Program} in {args.Game}, so it is not an install ({HowToGive})";
        }
        else
        {
            install = new Install { Root = args.Game };
        }

        return install != null;
    }

    /// <summary>Whether the install holds a file at <c>path</c>, in any letter case.</summary>
    public bool Exists(string path) => Find(Root, path) != null;

    /// <summary>The bytes of the file at <c>path</c>, in any letter case. Throws when it cannot be read.</summary>
    public byte[] ReadAll(string path) => File.ReadAllBytes(Find(Root, path) ?? Path.Combine(Root, path));

    /// <summary>
    /// The file at the relative <c>path</c> under <c>root</c>, each segment matched without regard to letter
    /// case: a folder segment only against folders, the last only against files. Where names differ only in case,
    /// the exact spelling wins, otherwise the first in ordinal order. Null when no spelling exists or a folder on
    /// the way cannot be listed.
    /// </summary>
    static string? Find(string root, string path)
    {
        var segments = path.Split('/');
        var current = root;
        for (var i = 0; i < segments.Length; i++)
        {
            var last = i == segments.Length - 1;
            string[] names;
            try
            {
                names = last ? Directory.GetFiles(current) : Directory.GetDirectories(current);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            var segment = segments[i];
            var matches = names
                .Where(candidate =>
                    string.Equals(Path.GetFileName(candidate), segment, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
            var found =
                matches.FirstOrDefault(candidate => Path.GetFileName(candidate) == segment)
                ?? matches.OrderBy(Path.GetFileName, StringComparer.Ordinal).FirstOrDefault();
            if (found == null)
            {
                return null;
            }

            current = found;
        }

        return current;
    }
}
