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
    /// <summary>The launch is refused: print <see cref="Message"/> on <see cref="Stream"/> and quit with <see cref="Code"/>.</summary>
    public sealed record Exit : MapLaunch
    {
        /// <summary>The exit code to quit with.</summary>
        public required int Code { get; init; }

        /// <summary>The message to print.</summary>
        public required string Message { get; init; }

        /// <summary>The stream to print the message on.</summary>
        public required LaunchStream Stream { get; init; }
    }

    /// <summary>
    /// Every check passed: open the view on <see cref="Install"/> and <see cref="Layout"/> (null without
    /// <c>--layout</c>). A play script comes with the folder its shots go to, <c>play-out/&lt;stem&gt;</c> under the
    /// project folder.
    /// </summary>
    public sealed record Open : MapLaunch
    {
        /// <summary>The layout to open the view on; null without <c>--layout</c>.</summary>
        public required Layout? Layout { get; init; }

        /// <summary>The install to open the view on.</summary>
        public required Install Install { get; init; }

        /// <summary>The play script to run; null without one.</summary>
        public required PlayScript? Script { get; init; }

        /// <summary>The folder the play script's shots go to, <c>play-out/&lt;stem&gt;</c> under the project folder; null without a play script.</summary>
        public required string? PlayOutFolder { get; init; }
    }

    /// <summary>
    /// The exit for a bad argument of any mode (<see cref="LaunchArgs.BadArgumentExitCode"/>, stderr, <c>ERROR</c> and
    /// the problems joined by "; "), or null where the arguments are good. The view calls it first for every mode, so
    /// no window opens for a bad argument; <see cref="Decide"/> repeats it to keep the order whole.
    /// </summary>
    public static Exit? BadArgument(LaunchArgs args) =>
        args.Problems.Count > 0
            ? new Exit
            {
                Code = LaunchArgs.BadArgumentExitCode,
                Message = $"ERROR {string.Join("; ", args.Problems)}",
                Stream = LaunchStream.Stderr,
            }
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
                return new Exit
                {
                    Code = load.Code,
                    Message = load.Message ?? "",
                    Stream = LaunchStream.Stdout,
                };
            }

            layout = load.Layout;
        }

        var (install, problem) = openInstall(args);
        if (install is null)
        {
            return new Exit
            {
                Code = 2,
                Message = $"ERROR {problem}",
                Stream = LaunchStream.Stdout,
            };
        }

        PlayScript? script = null;
        string? folder = null;
        if (args.Play is not null)
        {
            script = readScript(Path.Combine(projectFolder(), args.Play));
            if (script.Problems.Count > 0)
            {
                return new Exit
                {
                    Code = PlayRun.FailedExitCode,
                    Message = $"ERROR {string.Join("; ", script.Problems)}",
                    Stream = LaunchStream.Stderr,
                };
            }

            folder = Path.Combine(projectFolder(), "play-out", script.Stem);
        }

        return new Open
        {
            Layout = layout,
            Install = install,
            Script = script,
            PlayOutFolder = folder,
        };
    }
}
