using System;
using System.Collections.Generic;

namespace OpenSettlers.Launch;

/// <summary>What the engine does after it starts.</summary>
public enum LaunchMode
{
    /// <summary>The asset browser, the default.</summary>
    Browser,

    /// <summary>The asset check, a headless run that sets the process exit code.</summary>
    Check,
}

/// <summary>
/// The user arguments after <c>--</c>, read once. <c>--check</c> selects <see cref="LaunchMode.Check"/>; no mode
/// flag selects <see cref="LaunchMode.Browser"/>. <c>--game &lt;dir&gt;</c> names the install and wins over the
/// <c>SIEDLER_DIR</c> value. A flag that needs a value and has none, at the end or before another flag, is a
/// problem and its value is never read as a path.
/// </summary>
public sealed class LaunchArgs
{
    /// <summary>The flag that names the install.</summary>
    public const string GameFlag = "--game";

    /// <summary>The environment variable that stands in for <see cref="GameFlag"/>.</summary>
    public const string GameVariable = "SIEDLER_DIR";

    /// <summary>The run's mode.</summary>
    public required LaunchMode Mode { get; init; }

    /// <summary>The install folder as given; null when none was given or the game flag lacked its value.</summary>
    public string? Game { get; init; }

    /// <summary>One line per flag that lacks its value.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>
    /// Reads <c>args</c>, the arguments after <c>--</c>. <c>siedlerDir</c> is the value of the
    /// <c>SIEDLER_DIR</c> environment variable, null or empty when unset; it is used only when the game flag is
    /// absent. Never throws.
    /// </summary>
    public static LaunchArgs Parse(string[] args, string? siedlerDir)
    {
        var problems = new List<string>();
        var index = Array.IndexOf(args, GameFlag);
        string? game = string.IsNullOrEmpty(siedlerDir) ? null : siedlerDir;
        if (index >= 0)
        {
            var hasValue = index + 1 < args.Length && args[index + 1].Length > 0 && !args[index + 1].StartsWith("--");
            game = hasValue ? args[index + 1] : null;
            if (!hasValue)
            {
                problems.Add($"{GameFlag} needs a value");
            }
        }

        return new LaunchArgs
        {
            Mode = Array.IndexOf(args, "--check") >= 0 ? LaunchMode.Check : LaunchMode.Browser,
            Game = game,
            Problems = problems,
        };
    }
}
