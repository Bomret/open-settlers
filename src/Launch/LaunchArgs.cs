using System.Collections.Generic;
using System.Globalization;
using OpenSettlers.Simulation;

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

    /// <summary>Seed info: the headless run that prints the digests and counts of one seed and size.</summary>
    SeedInfo,
}

/// <summary>
/// The user arguments after <c>--</c>, read once. <c>--check</c> selects <see cref="LaunchMode.Check"/>, <c>--map</c>
/// selects <see cref="LaunchMode.Map"/>, <c>--seed-info</c> selects <see cref="LaunchMode.SeedInfo"/>. No mode flag
/// selects <see cref="LaunchMode.Browser"/>. <c>--game &lt;dir&gt;</c> names the install and wins over the
/// <c>SIEDLER_DIR</c> value. <c>--seed &lt;seed&gt;</c> (every form <see cref="Seed.TryParse"/> takes, quoted when
/// it holds spaces) and <c>--size &lt;1|2|3&gt;</c> belong to <c>--map</c> and <c>--seed-info</c> only.
/// <para>
/// Every bad argument is a line in <see cref="Problems"/> and the launch exits with <see cref="BadArgumentExitCode"/>
/// before it opens the install or a window, so that code wins over the exit code 2 of a missing install. The bad
/// arguments: a flag that needs a value and has none, at the end or before another flag (its value is never read
/// as a path); an unknown flag; a bad seed (an unquoted four-group seed is one); a bad size (anything but an
/// integer the original offers); two different mode flags, which select no mode (the browser); <c>--seed</c> or
/// <c>--size</c> with <c>--check</c> or without a mode flag.
/// </para>
/// </summary>
public sealed class LaunchArgs
{
    /// <summary>The process exit code for a bad argument. Exit code 2 stays "no usable install".</summary>
    public const int BadArgumentExitCode = 4;

    /// <summary>The flag that names the install.</summary>
    public const string GameFlag = "--game";

    /// <summary>The flag that selects the asset check.</summary>
    public const string CheckFlag = "--check";

    /// <summary>The flag that selects the map view.</summary>
    public const string MapFlag = "--map";

    /// <summary>The flag that selects seed info.</summary>
    public const string SeedInfoFlag = "--seed-info";

    /// <summary>The flag that gives the seed of the map.</summary>
    public const string SeedFlag = "--seed";

    /// <summary>The flag that gives the size value of the map.</summary>
    public const string SizeFlag = "--size";

    /// <summary>The environment variable that stands in for <see cref="GameFlag"/>.</summary>
    public const string GameVariable = "SIEDLER_DIR";

    /// <summary>The run's mode; <see cref="LaunchMode.Browser"/> when two different mode flags clash.</summary>
    public required LaunchMode Mode { get; init; }

    /// <summary>The install folder as given; null when none was given or the game flag lacked its value.</summary>
    public string? Game { get; init; }

    /// <summary>The seed as given, for display; null when no valid seed flag was given.</summary>
    public string? SeedText { get; init; }

    /// <summary>The parsed seed; null when no valid seed flag was given.</summary>
    public Seed? Seed { get; init; }

    /// <summary>The size the size flag gave; null when no valid size flag was given.</summary>
    public MapSize? Size { get; init; }

    /// <summary>One line per bad argument; empty when the arguments are good.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>
    /// Reads <c>args</c>, the arguments after <c>--</c>. <c>siedlerDir</c> is the value of the
    /// <c>SIEDLER_DIR</c> environment variable, null or empty when unset; it is used only when the game flag is
    /// absent. Never throws. A stray argument that is no flag and no flag's value is ignored.
    /// </summary>
    public static LaunchArgs Parse(string[] args, string? siedlerDir)
    {
        var problems = new List<string>();
        string? game = null;
        var gameGiven = false;
        string? modeFlag = null;
        string? conflictFlag = null;
        string? seedText = null;
        Seed? seed = null;
        MapSize? size = null;
        var seedGiven = false;
        var sizeGiven = false;
        for (var i = 0; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is GameFlag or SeedFlag or SizeFlag)
            {
                // The value is the next argument unless it is missing or another flag; it is not consumed
                // then, so a mode flag after the flag still selects its mode.
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                {
                    problems.Add($"{flag} needs a value");
                    gameGiven |= flag == GameFlag;

                    continue;
                }

                var value = args[++i];
                if (flag == GameFlag)
                {
                    if (!gameGiven)
                    {
                        game = value.Length > 0 ? value : null;
                        if (game is null)
                        {
                            problems.Add($"{GameFlag} needs a value");
                        }
                    }

                    gameGiven = true;
                }
                else if (flag == SeedFlag && !seedGiven)
                {
                    seedGiven = true;
                    if (Simulation.Seed.TryParse(value, out var parsed, out var reason))
                    {
                        seed = parsed;
                        seedText = value;
                    }
                    else
                    {
                        problems.Add($"bad {SeedFlag} '{value}': {reason}");
                    }
                }
                else if (flag == SizeFlag && !sizeGiven)
                {
                    sizeGiven = true;
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                    {
                        problems.Add($"bad {SizeFlag} '{value}': not a size number, use 1, 2 or 3");
                    }
                    else if (!MapSize.TryFromValue(number, out size, out var reason))
                    {
                        problems.Add($"bad {SizeFlag}: {reason}");
                    }
                }
            }
            else if (flag is CheckFlag or MapFlag or SeedInfoFlag)
            {
                if (modeFlag is not null && modeFlag != flag)
                {
                    conflictFlag ??= flag;
                }

                modeFlag ??= flag;
            }
            else if (flag.StartsWith("--"))
            {
                problems.Add($"unknown flag {flag}");
            }
        }

        if (conflictFlag is not null)
        {
            problems.Add($"{modeFlag} and {conflictFlag} are different modes, give only one");
        }

        if (modeFlag == SeedInfoFlag && conflictFlag is null && !seedGiven)
        {
            problems.Add($"{SeedInfoFlag} needs {SeedFlag}");
        }

        if (modeFlag is null or CheckFlag)
        {
            var where = modeFlag is null ? "the asset browser" : CheckFlag;
            if (seedGiven)
            {
                problems.Add($"{SeedFlag} does not apply to {where}, use {MapFlag} or {SeedInfoFlag}");
            }

            if (sizeGiven)
            {
                problems.Add($"{SizeFlag} does not apply to {where}, use {MapFlag} or {SeedInfoFlag}");
            }
        }

        return new LaunchArgs
        {
            Mode = conflictFlag is not null
                ? LaunchMode.Browser
                : modeFlag switch
                {
                    CheckFlag => LaunchMode.Check,
                    MapFlag => LaunchMode.Map,
                    SeedInfoFlag => LaunchMode.SeedInfo,
                    _ => LaunchMode.Browser,
                },
            Game =
                gameGiven ? game
                : string.IsNullOrEmpty(siedlerDir) ? null
                : siedlerDir,
            SeedText = seedText,
            Seed = seed,
            Size = size,
            Problems = problems,
        };
    }
}
