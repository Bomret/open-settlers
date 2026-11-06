using System.Diagnostics.CodeAnalysis;
using OpenSettlers.Simulation;

namespace OpenSettlers.Launch;

/// <summary>The launch-time load of the layout file <c>--layout</c> names (<see cref="LaunchArgs.Layout"/>).</summary>
public static class LayoutLoad
{
    /// <summary>
    /// Reads the layout file at <paramref name="path"/>. Returns 0 and the layout when it loads. Returns
    /// <see cref="LaunchArgs.BadArgumentExitCode"/> and a message of the form <c>ERROR file: entry: reason</c> (one
    /// problem after another, joined by <c>; </c>) when the file is missing, cannot be read, or fails to load.
    /// </summary>
    public static int Run(string path, [NotNullWhen(true)] out Layout? layout, [NotNullWhen(false)] out string? message)
    {
        var read = Layout.ReadFile(path);
        layout = read.Layout;
        if (layout is not null)
        {
            message = null;

            return 0;
        }

        message = $"ERROR {string.Join("; ", read.Problems)}";

        return LaunchArgs.BadArgumentExitCode;
    }

    /// <summary>
    /// Starts <paramref name="game"/> from <paramref name="layout"/>: builds its entries (<see cref="Layout.Apply"/>).
    /// Returns 0 and the game when every entry was built, or when there is no layout. Returns
    /// <see cref="LaunchArgs.BadArgumentExitCode"/>, no game, and the message <c>ERROR file: entry: reason</c> when the
    /// game refuses an entry: the run shows nothing of that game.
    /// </summary>
    public static int Start(Layout? layout, Game game, out Game? started, out string? message)
    {
        if (layout?.Apply(game) is { } problem)
        {
            started = null;
            message = $"ERROR {problem}";

            return LaunchArgs.BadArgumentExitCode;
        }

        started = game;
        message = null;

        return 0;
    }
}
