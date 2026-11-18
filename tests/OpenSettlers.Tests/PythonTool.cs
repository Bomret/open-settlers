using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

/// <summary>The exit status and the output of one run of a python tool.</summary>
readonly record struct ToolRun
{
    public required int Exit { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }
}

/// <summary>
/// The one place the tests spawn python3 for the scripts under <c>tools/</c>. A missing python3 fails the test with the hint
/// to run <c>mise install</c>, as <c>docs/agents/verification.md</c> states, except in <see cref="RunOnPath"/>, which reports
/// inconclusive. Each run sets its environment: <c>SIEDLER_DIR</c>
/// is absent unless the caller passes it, no <c>GIT_*</c> variable of the host reaches the child, and the path holds no <c>analyzeHeadless</c> of the host unless the caller asks for it.
/// <see cref="Worktree"/> builds a git repository with a linked worktree for the tools that find the main checkout, and
/// <see cref="Git"/> runs git with every <c>GIT_*</c> variable of the host removed.
/// </summary>
static class PythonTool
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
        var found = Locate(searchPath);
        if (found is null)
        {
            Assert.Fail(MissingHint);
        }

        return found;
    }

    /// <summary>The program <paramref name="name"/> on <paramref name="searchPath"/> (the host's path where null), or null.</summary>
    public static string? Locate(string? searchPath, string name = "python3") =>
        (searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(dir => new[] { Path.Combine(dir, name), Path.Combine(dir, name + ".exe") })
            .FirstOrDefault(File.Exists);

    /// <summary>
    /// Builds a git repository <c>main</c> with one commit in <paramref name="folder"/>, and a linked worktree of it at
    /// <c>.scopes/tree</c>, under a folder whose name starts with a dot as a superdev scopetree's path does. git runs with
    /// every <c>GIT_*</c> variable of the host removed and the commit identity on its command line. Returns the host's git
    /// and both trees. Reports inconclusive where the host has no git.
    /// </summary>
    public static (string Git, string Main, string Worktree) Worktree(string folder)
    {
        var git = Locate(null, "git");
        if (git is null)
        {
            Assert.Inconclusive("git is not on the path");
        }

        var main = Path.Combine(folder, "main");
        var worktree = Path.Combine(folder, ".scopes", "tree");
        Directory.CreateDirectory(main);
        RunGit(git, main, "init", "-q");
        RunGit(
            git,
            main,
            "-c",
            "user.name=test",
            "-c",
            "user.email=test@example.invalid",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "-q",
            "--allow-empty",
            "-m",
            "start"
        );
        RunGit(git, main, "worktree", "add", "-q", worktree);

        return (git, main, worktree);
    }

    static void RunGit(string git, string workingDirectory, params string[] arguments)
    {
        var run = Git(git, workingDirectory, arguments);
        if (run.Exit != 0)
        {
            Assert.Fail($"git {string.Join(' ', arguments)} exited {run.Exit}: {run.Stderr}");
        }
    }

    /// <summary>
    /// Runs <paramref name="git"/> with the arguments in <paramref name="workingDirectory"/>, with every <c>GIT_*</c> variable
    /// of the host removed, and returns its exit status and output, read as UTF-8.
    /// </summary>
    public static ToolRun Git(string git, string workingDirectory, params string[] arguments)
    {
        var info = new ProcessStartInfo(git)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // git writes paths in UTF-8; a console code page of the host must not decode them.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        RemoveGitVariables(info);

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ToolRun
        {
            Exit = process.ExitCode,
            Stdout = stdout.Result,
            Stderr = stderr.Result,
        };
    }

    // A GIT_DIR of the host, as a git hook sets it, would make git resolve the host's repository.
    static void RemoveGitVariables(ProcessStartInfo info)
    {
        foreach (
            var name in info.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToList()
        )
        {
            info.Environment.Remove(name);
        }
    }

    /// <summary>Starts the process, or fails the test with the hint where the program cannot be started.</summary>
    public static Process Start(ProcessStartInfo info)
    {
        try
        {
            return Process.Start(info)!;
        }
        catch (Win32Exception exception)
        {
            Assert.Fail($"{MissingHint} ({exception.Message})");
            throw;
        }
    }

    static bool HoldsHeadless(string dir) =>
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

        return Wait(info, siedlerDir);
    }

    /// <summary>
    /// Runs <c>python3 -I -B</c> with the arguments in <paramref name="workingDirectory"/>, the child's path replaced by
    /// <paramref name="path"/> alone, so no tool of the host's path reaches the child. python3 runs by its absolute path.
    /// Reports inconclusive where the host has no python3.
    /// </summary>
    public static ToolRun RunOnPath(string workingDirectory, string[] arguments, string[] path, string? siedlerDir)
    {
        var python = Locate(null);
        if (python is null)
        {
            Assert.Inconclusive(MissingHint);
        }

        var info = new ProcessStartInfo(python) { WorkingDirectory = workingDirectory };
        foreach (var argument in (string[])["-I", "-B", .. arguments])
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["PATH"] = string.Join(Path.PathSeparator, path);

        return Wait(info, siedlerDir);
    }

    static ToolRun Wait(ProcessStartInfo info, string? siedlerDir)
    {
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.Environment.Remove("SIEDLER_DIR");
        RemoveGitVariables(info);
        if (siedlerDir is not null)
        {
            info.Environment["SIEDLER_DIR"] = siedlerDir;
        }

        using var process = Start(info);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ToolRun
        {
            Exit = process.ExitCode,
            Stdout = stdout.Result,
            Stderr = stderr.Result,
        };
    }

    /// <summary>
    /// Writes a shell script named analyzeHeadless into <paramref name="folder"/> and returns the folder, to prepend to the
    /// path. The body runs as <c>/bin/sh</c> with the tool's arguments.
    /// </summary>
    public static string StandInHeadless(string folder, string body) => StandIn(folder, "analyzeHeadless", body);

    /// <summary>
    /// Writes a shell script named <paramref name="name"/> into <paramref name="folder"/> and returns the folder. The body
    /// runs as <c>/bin/sh</c> with the tool's arguments. Reports inconclusive on Windows.
    /// </summary>
    public static string StandIn(string folder, string name, string body)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive($"the stand-in {name} is a shell script");
            return folder;
        }

        Directory.CreateDirectory(folder);
        var script = Path.Combine(folder, name);
        File.WriteAllText(script, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return folder;
    }
}
