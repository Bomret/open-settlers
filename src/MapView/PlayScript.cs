using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenSettlers.MapView;

/// <summary>One step of a play script; every step keeps the line of the script it came from.</summary>
public abstract record PlayStep
{
    /// <summary>The 1-based line of the step in the script.</summary>
    public required int Line { get; init; }
}

/// <summary><c>wait &lt;seconds&gt;</c>: lets simulated time pass.</summary>
public sealed record WaitStep : PlayStep
{
    /// <summary>The simulated seconds to wait; zero or more.</summary>
    public required double Seconds { get; init; }
}

/// <summary>
/// A play script (<c>CONTEXT.md</c>): a text file with one step per line, run in file order. Blank lines and lines
/// that start with <c>#</c> are skipped. A line that cannot be read is a problem naming the script and the line and
/// adds no step. Reading never throws for bad text; the play run reports the problems.
/// </summary>
public sealed class PlayScript
{
    /// <summary>The script's path as given; problems name it.</summary>
    public required string Path { get; init; }

    /// <summary>The script's file name without extension: the name of its output folder under <c>play-out/</c>.</summary>
    public string Stem => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>The steps in file order.</summary>
    public required IReadOnlyList<PlayStep> Steps { get; init; }

    /// <summary>One line per problem, each naming the script and the line.</summary>
    public required IReadOnlyList<string> Problems { get; init; }

    /// <summary>Reads the script at <c>path</c>; a file that cannot be read is one problem and no steps.</summary>
    public static PlayScript Read(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path), path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new PlayScript
            {
                Path = path,
                Steps = [],
                Problems = [$"{path}: script cannot be read: {e.Message}"],
            };
        }
    }

    /// <summary>Parses <c>text</c>; <c>path</c> only names the script in problems and in <see cref="Path"/>.</summary>
    public static PlayScript Parse(string text, string path)
    {
        var steps = new List<PlayStep>();
        var problems = new List<string>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var (step, problem) = ParseLine(line, i + 1);
            if (step is not null)
                steps.Add(step);
            else
                problems.Add($"{path} line {i + 1}: {problem}");
        }

        return new PlayScript
        {
            Path = path,
            Steps = steps,
            Problems = problems,
        };
    }

    static (PlayStep? Step, string? Problem) ParseLine(string line, int number)
    {
        var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var args = words.Skip(1).ToArray();
        switch (words[0])
        {
            case "wait":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);
                if (!Number(args[0], out var seconds) || seconds < 0)
                    return (null, $"'{args[0]}' is not a number of seconds of zero or more");

                return (new WaitStep { Line = number, Seconds = seconds }, null);
            default:
                return (null, $"unknown step '{words[0]}'");
        }
    }

    static (PlayStep?, string?) Wrong(string step, string want, int got) =>
        (null, $"'{step}' takes {want}, found {got}");

    static bool Number(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
