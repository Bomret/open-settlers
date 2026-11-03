using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.MapView;

/// <summary>
/// A play run of a <see cref="PlayScript"/> (<c>CONTEXT.md</c>) on simulated time: the caller feeds each frame's time
/// to <see cref="Advance"/> and the run executes every step that is due, in file order. <c>wait</c>, <c>dump</c>, and
/// <c>expect</c> are the run's own; every other step goes to the <c>input</c> function, which sends it to the view.
/// A step that fails or throws ends the run with a problem naming the script and the line. The run never logs and
/// never throws for a step; the caller logs <see cref="Problems"/> and quits with the exit code.
/// </summary>
public sealed class PlayRun(
    PlayScript script,
    Func<PlayState> state,
    string outputFolder,
    Func<PlayStep, string?> input
)
{
    /// <summary>The exit code of a run that ended with a problem.</summary>
    public const int FailedExitCode = 1;

    readonly List<string> _problems = [];
    HoldStep? _held;
    double _clock;
    double _due;
    int _next;

    /// <summary>Whether the last step has run and its wait has passed, or a step failed.</summary>
    public bool Finished { get; private set; }

    /// <summary>The problems that ended the run; none for a run that finished its last step.</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>
    /// Lets <c>seconds</c> of simulated time pass and runs every step that is due, in file order. A wait counts from
    /// the time its step was due, not from the frame that reached it, so the frame rate causes no drift.
    /// </summary>
    public void Advance(double seconds)
    {
        if (Finished)
            return;

        _clock += seconds;
        // simplicity: a wait of whole frames can come out a rounding error short; this tolerance counts it as passed.
        const double Tolerance = 1e-9;
        while (_due <= _clock + Tolerance)
        {
            // A hold ends when its time has passed, before the step after it runs.
            PlayStep step;
            if (_held is { } held)
            {
                step = new ReleaseStep { Line = held.Line, Key = held.Key };
                _held = null;
            }
            else if (_next == script.Steps.Count)
            {
                Finished = true;

                return;
            }
            else
            {
                step = script.Steps[_next++];
            }

            string? problem;
            try
            {
                problem = Run(step);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                problem = $"step threw {e.GetType().Name}: {e.Message}";
            }

            if (problem is not null)
            {
                _problems.Add($"{script.Path} line {step.Line}: {problem}");
                Finished = true;

                return;
            }
        }
    }

    string? Run(PlayStep step)
    {
        switch (step)
        {
            case WaitStep wait:
                _due += wait.Seconds;

                return null;
            case DumpStep dump:
                Directory.CreateDirectory(outputFolder);
                File.WriteAllText(Path.Combine(outputFolder, dump.Name + ".txt"), state().Dump());

                return null;
            case ExpectStep expect:
                return state().Check(expect);
            case HoldStep hold:
                _held = hold;
                _due += hold.Seconds;

                return input(step);
            default:
                return input(step);
        }
    }
}
