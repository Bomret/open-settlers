using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

/// <summary>The exit status and the output of one run of a python tool.</summary>
internal readonly record struct ToolRun(int Exit, string Stdout, string Stderr);

/// <summary>
/// The one place the tests spawn python3 for the scripts under <c>tools/</c>. A missing python3 fails the test with the hint
/// to run <c>mise install</c>, as <c>docs/agents/verification.md</c> states. Each run sets its environment: <c>SIEDLER_DIR</c>
/// is absent unless the caller passes it, and the path holds no <c>analyzeHeadless</c> of the host unless the caller asks for it.
/// </summary>
internal static class PythonTool
{
    public const string MissingHint = "python3 is not on the path; mise install provides it";

    public static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "GhidraDump", "emulate.py")))
        {
            dir =
                Path.GetDirectoryName(dir)
                ?? throw new InvalidOperationException("tools/GhidraDump/emulate.py not found");
        }

        return dir;
    }

    /// <summary>The absolute path of a file under the repository root.</summary>
    public static string InRepo(params string[] parts) => Path.Combine([RepoRoot(), .. parts]);

    /// <summary>The python3 executable on <paramref name="searchPath"/> (the host's path where null), or a failed test.</summary>
    public static string Resolve(string? searchPath = null)
    {
        var found = (searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(dir => new[] { Path.Combine(dir, "python3"), Path.Combine(dir, "python3.exe") })
            .FirstOrDefault(File.Exists);
        if (found is null)
        {
            Assert.Fail(MissingHint);
        }

        return found;
    }

    /// <summary>Starts the process, or fails the test with the hint where the program cannot be started.</summary>
    public static Process Start(ProcessStartInfo info)
    {
        try
        {
            return Process.Start(info)!;
        }
        catch (Win32Exception)
        {
            Assert.Fail(MissingHint);
            throw;
        }
    }

    private static bool HoldsHeadless(string dir) =>
        File.Exists(Path.Combine(dir, "analyzeHeadless")) || File.Exists(Path.Combine(dir, "analyzeHeadless.bat"));

    /// <summary>
    /// Runs <c>python3 -I -B</c> with the arguments in <paramref name="workingDirectory"/>. The child's path is
    /// <paramref name="prependPath"/> before the host's path without the folders that hold <c>analyzeHeadless</c>, unless
    /// <paramref name="realHeadless"/> keeps them.
    /// </summary>
    public static ToolRun Run(
        string workingDirectory,
        string[] arguments,
        string? prependPath = null,
        string? siedlerDir = null,
        bool realHeadless = false
    )
    {
        var info = new ProcessStartInfo(Resolve())
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-I");
        info.ArgumentList.Add("-B");
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var host = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );
        var path = (prependPath is null ? [] : new[] { prependPath }).Concat(
            realHeadless ? host : host.Where(dir => !HoldsHeadless(dir))
        );
        info.Environment["PATH"] = string.Join(Path.PathSeparator, path);
        info.Environment.Remove("SIEDLER_DIR");
        if (siedlerDir is not null)
        {
            info.Environment["SIEDLER_DIR"] = siedlerDir;
        }

        using var process = Start(info);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ToolRun(process.ExitCode, stdout.Result, stderr.Result);
    }

    /// <summary>
    /// Writes a shell script named analyzeHeadless into <paramref name="folder"/> and returns the folder, to prepend to the
    /// path. The body runs as <c>/bin/sh</c> with the tool's arguments.
    /// </summary>
    public static string StandInHeadless(string folder, string body)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the stand-in analyzeHeadless is a shell script");
            return folder;
        }

        Directory.CreateDirectory(folder);
        var script = Path.Combine(folder, "analyzeHeadless");
        File.WriteAllText(script, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return folder;
    }
}
