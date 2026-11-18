using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSettlers.Testing;

/// <summary>
/// A fixture of the emulator verb remove (tools/GhidraDump/emulate.py, ADR 0008): scenarios, each a "#" line that names its verb
/// command and the lines that follow it.
/// </summary>
public static class EmulatorFixture
{
    /// <summary>A scenario of a fixture: its name, its verb command, and its lines.</summary>
    public sealed record Scenario
    {
        public required string Name { get; init; }

        public required string Command { get; init; }

        public required List<Dictionary<string, string>> Lines { get; init; }
    }

    /// <summary>The scenarios of the fixture, in its order.</summary>
    public static List<Scenario> Load(string fixture)
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(fixture))
        {
            // A scenario's "# name command" line has a verb command (no space before its first colon); the other "#" lines and
            // the blank lines of a fixture are notes.
            if (line.StartsWith("# ", StringComparison.Ordinal) && line[2..].Split(' ', 2) is [var head, var command])
            {
                if (!command.Split(':')[0].Contains(' ') && command.Contains(':'))
                {
                    scenarios.Add(
                        new Scenario
                        {
                            Name = head,
                            Command = command,
                            Lines = [],
                        }
                    );
                }

                continue;
            }

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            scenarios[^1]
                .Lines.Add(
                    line.Split(' ')
                        .Select(field => field.Split('=', 2))
                        .ToDictionary(pair => pair[0], pair => pair.Length > 1 ? pair[1] : "")
                );
        }

        return scenarios;
    }
}
