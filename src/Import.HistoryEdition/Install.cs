using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// An install of Die Siedler on disk: a folder that holds <c>Siedler1.exe</c>. Paths are relative to the root and
/// use <c>/</c>.
/// </summary>
public sealed class Install
{
    /// <summary>The program an install holds.</summary>
    public const string Program = "Siedler1.exe";

    /// <summary>The install folder.</summary>
    public required string Root { get; init; }

    /// <summary>
    /// Opens the install in <paramref name="folder"/>. Returns false with a <c>problem</c> that names what is wrong
    /// when the folder is missing or holds no <see cref="Program"/>. The text names no way to give an install: the
    /// caller that took the folder from an argument adds that.
    /// </summary>
    public static bool TryOpen(
        string folder,
        [NotNullWhen(true)] out Install? install,
        [NotNullWhen(false)] out string? problem
    )
    {
        install = null;
        problem = null;
        if (!Directory.Exists(folder))
        {
            problem = $"install folder not found: {folder}";
        }
        else if (Find(folder, Program) == null)
        {
            problem = $"no {Program} in {folder}, so it is not an install";
        }
        else
        {
            install = new Install { Root = folder };
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
