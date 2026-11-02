using System.Collections.Generic;

namespace OpenSettlers.Launch;

/// <summary>What the engine does after it starts.</summary>
public enum LaunchMode
{
    /// <summary>The asset browser, the default.</summary>
    Browser,

    /// <summary>The asset check, a headless run that sets the process exit code.</summary>
    Check,

    /// <summary>The map view: a generated map drawn from the install's terrain graphics.</summary>
    Map,

    /// <summary>The game view: a game on a generated map that ticks at the original's normal speed.</summary>
    Play,
}

/// <summary>
/// The user arguments after <c>--</c>, read once. <c>--check</c> selects <see cref="LaunchMode.Check"/>, <c>--map</c>
/// selects <see cref="LaunchMode.Map"/>, <c>--play</c> selects <see cref="LaunchMode.Play"/>; two different mode flags are a problem and select no mode (the browser). No mode
/// flag selects <see cref="LaunchMode.Browser"/>. <c>--game &lt;dir&gt;</c> names the install and wins over the
/// <c>SIEDLER_DIR</c> value. A flag that needs a value and has none, at the end or before another flag, is a
/// problem and its value is never read as a path.
/// </summary>
public sealed class LaunchArgs
{
    /// <summary>The flag that names the install.</summary>
    public const string GameFlag = "--game";

    /// <summary>The flag that selects the asset check.</summary>
    public const string CheckFlag = "--check";

    /// <summary>The flag that selects the map view.</summary>
    public const string MapFlag = "--map";

    /// <summary>The flag that selects the game view.</summary>
    public const string PlayFlag = "--play";

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
        string? game = null;
        var gameGiven = false;
        LaunchMode? mode = null;
        string? modeFlag = null;
        string? conflictFlag = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == GameFlag && !gameGiven)
            {
                gameGiven = true;
                // The value is the next argument unless it is missing, empty, or another flag; it is not consumed
                // then, so a mode flag after the game flag still selects its mode.
                if (i + 1 < args.Length && args[i + 1].Length > 0 && !args[i + 1].StartsWith("--"))
                {
                    game = args[++i];
                }
                else
                {
                    problems.Add($"{GameFlag} needs a value");
                }
            }
            else if (args[i] == CheckFlag || args[i] == MapFlag || args[i] == PlayFlag)
            {
                if (modeFlag is not null && modeFlag != args[i])
                {
                    conflictFlag ??= args[i];
                }

                modeFlag ??= args[i];
                mode ??= args[i] switch
                {
                    CheckFlag => LaunchMode.Check,
                    MapFlag => LaunchMode.Map,
                    _ => LaunchMode.Play,
                };
            }
        }

        if (conflictFlag is not null)
        {
            problems.Add($"{modeFlag} and {conflictFlag} are different modes, give only one");
        }

        return new LaunchArgs
        {
            Mode = mode ?? LaunchMode.Browser,
            Game =
                gameGiven ? game
                : string.IsNullOrEmpty(siedlerDir) ? null
                : siedlerDir,
            Problems = problems,
        };
    }
}
