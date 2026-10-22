using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class RepositoryContentTests
{
    // The file types of a Die Siedler install: archives, palettes, sounds, music, save games, the intro
    // video, and the game's programs.
    static readonly string[] InstallExtensions =
    [
        ".pa",
        ".pal",
        ".snd",
        ".xmi",
        ".midi",
        ".ad",
        ".mt",
        ".opl",
        ".ds",
        ".wmv",
        ".exe",
        ".dll",
        ".adv",
    ];

    static readonly string[] SkippedFolders = [".git", ".godot", "bin", "obj", ".superdev"];

    [TestMethod]
    public void TheRepositoryHoldsNoFileOfAnInstallsFileTypes()
    {
        var found = Files(RepositoryRoot())
            .Where(path => InstallExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.IsEmpty(found, $"install file types in the repository: {string.Join(", ", found)}");
    }

    static IEnumerable<string> Files(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
            yield return file;

        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            if (SkippedFolders.Contains(Path.GetFileName(child)))
                continue;

            foreach (var file in Files(child))
                yield return file;
        }
    }

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
            folder = folder.Parent;

        return folder?.FullName
            ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output");
    }
}
