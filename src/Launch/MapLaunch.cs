using System;
using System.IO;
using OpenSettlers.Assets;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Launch;

/// <summary>The stream a refused launch writes its message to.</summary>
public enum LaunchStream
{
    /// <summary>Standard output.</summary>
    Stdout,

    /// <summary>Standard error.</summary>
    Stderr,
}

/// <summary>
/// The outcome of a map launch (<see cref="LaunchMode.Map"/> or <see cref="LaunchMode.GameView"/>): the exit that
/// refuses it, or the view that opens.
/// </summary>
public abstract record MapLaunch
{
    /// <summary>The launch is refused: print <paramref name="Message"/> on <paramref name="Stream"/> and quit with <paramref name="Code"/>.</summary>
    public sealed record Exit(int Code, string Message, LaunchStream Stream) : MapLaunch;

    /// <summary>
    /// Every check passed: open the view on <paramref name="Install"/> and <paramref name="Layout"/> (null without
    /// <c>--layout</c>). A play script comes with the folder its shots go to, <c>play-out/&lt;stem&gt;</c> under the
    /// project folder.
    /// </summary>
    public sealed record Open(Layout? Layout, Install Install, PlayScript? Script, string? PlayOutFolder) : MapLaunch;

    /// <summary>
    /// The exit for a bad argument of any mode (<see cref="LaunchArgs.BadArgumentExitCode"/>, stderr, <c>ERROR</c> and
    /// the problems joined by "; "), or null where the arguments are good. The view calls it first for every mode, so
    /// no window opens for a bad argument; <see cref="Decide"/> repeats it to keep the order whole.
    /// </summary>
    public static Exit? BadArgument(LaunchArgs args) =>
        args.Problems.Count > 0
            ? new Exit(LaunchArgs.BadArgumentExitCode, $"ERROR {string.Join("; ", args.Problems)}", LaunchStream.Stderr)
            : null;

    /// <summary>
    /// Decides a map launch, checking in this order, the first failure winning: a bad argument exits
    /// <see cref="LaunchArgs.BadArgumentExitCode"/> (stderr); a layout file that fails to load exits with its own code
    /// and message (stdout); a missing install exits 2 (stdout, <c>ERROR problem</c>); an unreadable or bad play script
    /// exits <see cref="PlayRun.FailedExitCode"/> (stderr). A layout entry the game refuses after the view opens
    /// (<see cref="LayoutLoad.Start"/>) is outside this decision. The engine services come in as delegates:
    /// <paramref name="loadLayout"/> (<see cref="LayoutLoad.Run"/>), <paramref name="openInstall"/>
    /// (<see cref="Install.TryOpen"/>), <paramref name="readScript"/> (<see cref="PlayScript.Read"/>), and
    /// <paramref name="projectFolder"/> (the folder of the project, which a relative play script path and
    /// <c>play-out</c> sit under).
    /// </summary>
    public static MapLaunch Decide(
        LaunchArgs args,
        Func<string, (int Code, Layout? Layout, string? Message)> loadLayout,
        Func<LaunchArgs, (Install? Install, string? Problem)> openInstall,
        Func<string, PlayScript> readScript,
        Func<string> projectFolder
    )
    {
        if (BadArgument(args) is { } bad)
        {
            return bad;
        }

        Layout? layout = null;
        if (args.Layout is not null)
        {
            var load = loadLayout(args.Layout);
            if (load.Code != 0)
            {
                return new Exit(load.Code, load.Message ?? "", LaunchStream.Stdout);
            }

            layout = load.Layout;
        }

        var (install, problem) = openInstall(args);
        if (install is null)
        {
            return new Exit(2, $"ERROR {problem}", LaunchStream.Stdout);
        }

        PlayScript? script = null;
        string? folder = null;
        if (args.Play is not null)
        {
            script = readScript(Path.Combine(projectFolder(), args.Play));
            if (script.Problems.Count > 0)
            {
                return new Exit(
                    PlayRun.FailedExitCode,
                    $"ERROR {string.Join("; ", script.Problems)}",
                    LaunchStream.Stderr
                );
            }

            folder = Path.Combine(projectFolder(), "play-out", script.Stem);
        }

        return new Open(layout, install, script, folder);
    }
}
