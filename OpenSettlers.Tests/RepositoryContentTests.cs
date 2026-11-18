using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    // The content set is the output of tools/basic-art.py, which draws every image from numbers, so the set holds
    // nothing derived from an install. A hand-edited or pasted file, a file the generator does not write, or a manifest
    // the generator does not write fails here. .gdignore keeps Godot from importing the PNGs and is not art. A PNG
    // compares by its chunks with the image data decompressed, since the compressed bytes may differ between zlib builds.
    // The test fails where python3 is not on the path: mise install provides it.
    [TestMethod]
    public void TheBasicContentSetIsTheGeneratorsOutput()
    {
        var root = RepositoryRoot();
        var generated = Path.Combine(Path.GetTempPath(), "basic-art-" + Guid.NewGuid().ToString("N"));
        try
        {
            var run = PythonTool.Run(root, [PythonTool.InRepo("tools", "basic-art.py"), generated]);
            Assert.AreEqual(0, run.Exit, run.Stderr);

            var checkedIn = Path.Combine(root, "content", "basic");
            var expected = SetFiles(generated);
            // The rule files and the walk steps file are hand-written, not generated: exactly the files the rule tables port
            // and the walk steps port name are exempt.
            var actual = SetFiles(checkedIn)
                .Where(file =>
                    file != ".gdignore"
                    && !OpenSettlersRuleTables.Files.Contains(file)
                    && file != OpenSettlersWalkSteps.WalkStepsFile
                )
                .ToList();
            var differences = expected
                .Except(actual)
                .Select(file => $"missing {file}")
                .Concat(actual.Except(expected).Select(file => $"not generated {file}"))
                .Concat(
                    expected
                        .Intersect(actual)
                        .Where(file =>
                            !Comparable(Path.Combine(generated, file))
                                .SequenceEqual(Comparable(Path.Combine(checkedIn, file)))
                        )
                        .Select(file => $"differs {file}")
                )
                .ToList();

            Assert.IsEmpty(differences, $"content/basic/ against the generator: {string.Join(", ", differences)}");
        }
        finally
        {
            if (Directory.Exists(generated))
            {
                Directory.Delete(generated, true);
            }
        }
    }

    // The stages of a stone heap (the object codes 0x48 to 0x4f) shrink as the stonecutter cuts it, so each stage has an image of its
    // own.
    [TestMethod]
    public void EachStageOfAStoneHeapHasItsOwnImage()
    {
        var manifest = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "content", "basic", "manifest.json"))
        );
        byte[] Image(string key) =>
            File.ReadAllBytes(
                Path.Combine(
                    RepositoryRoot(),
                    "content",
                    "basic",
                    manifest.RootElement.GetProperty(key).GetProperty("image").GetString()!
                )
            );

        var heaps = Enumerable.Range(0x48, 8).Select(code => Convert.ToHexString(Image($"object/{code}"))).ToList();

        Assert.HasCount(8, heaps.Distinct(), "the eight stages of a heap");
    }

    // Each road marker the basic set draws, the end, blocked, and way-back marks and the slope of each difference from -4 to 4,
    // has an image of its own, so the player tells them apart.
    [TestMethod]
    public void EachRoadMarkerHasItsOwnImage()
    {
        var manifest = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "content", "basic", "manifest.json"))
        );
        string Image(string key) =>
            Convert.ToHexString(
                File.ReadAllBytes(
                    Path.Combine(
                        RepositoryRoot(),
                        "content",
                        "basic",
                        manifest.RootElement.GetProperty(key).GetProperty("image").GetString()!
                    )
                )
            );

        var keys = new[] { "road-marker/end", "road-marker/blocked", "road-marker/way-back" }.Concat(
            Enumerable.Range(-4, 9).Select(difference => $"road-marker/slope/{difference}")
        );

        Assert.HasCount(12, keys.Select(Image).Distinct(), "the twelve road markers");
    }

    // The two saplings a forester plants (the object codes 0x67 and 0x68) have images of their own, not the litter the generator
    // draws for the codes it names nothing for, and differ from each other, as the two grow into different trees.
    [TestMethod]
    public void EachSaplingHasItsOwnImage()
    {
        var manifest = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "content", "basic", "manifest.json"))
        );
        string Image(string key) => manifest.RootElement.GetProperty(key).GetProperty("image").GetString()!;

        Assert.AreEqual("object/sapling-0.png", Image("object/103"));
        Assert.AreEqual("object/sapling-1.png", Image("object/104"));
    }

    // A project folder the Godot project leaves out of its compile holds a .gdignore, so Godot imports nothing of it, and the
    // solution lists each of its projects, so the solution builds it.
    [TestMethod]
    public void EachProjectFolderTheGodotProjectLeavesOutIsGodotIgnoredAndInTheSolution()
    {
        var root = RepositoryRoot();
        var removed = System
            .Xml.Linq.XDocument.Load(Path.Combine(root, "OpenSettlers.csproj"))
            .Descendants("Compile")
            .Select(compile => (string?)compile.Attribute("Remove"))
            .OfType<string>()
            .SelectMany(remove => remove.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Select(pattern => pattern.Trim().TrimEnd('*').TrimEnd('/'))
            .ToList();
        var solution = System
            .Xml.Linq.XDocument.Load(Path.Combine(root, "OpenSettlers.slnx"))
            .Descendants("Project")
            .Select(project => (string?)project.Attribute("Path"))
            .ToHashSet();

        // A pattern such as **/*.Tests names every project folder whose name ends the way it does.
        var everyProject = Files(root)
            .Where(file => file.EndsWith(".csproj", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, Path.GetDirectoryName(file)!).Replace('\\', '/'))
            .ToList();
        var folders = removed
            .SelectMany(pattern =>
                pattern.Contains('*')
                    ? everyProject.Where(folder => folder.EndsWith(pattern.Split('*')[^1], StringComparison.Ordinal))
                    : [pattern]
            )
            .Distinct()
            .ToList();
        // A folder inside another listed one is ignored and built with it.
        folders =
        [
            .. folders.Where(folder => !folders.Any(outer => folder.StartsWith(outer + "/", StringComparison.Ordinal))),
        ];

        var projectFolders = 0;
        foreach (var folder in folders)
        {
            var projects = Files(Path.Combine(root, folder))
                .Where(file => file.EndsWith(".csproj", StringComparison.Ordinal))
                .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
                .ToList();
            if (projects.Count == 0)
            {
                continue;
            }

            projectFolders++;
            Assert.IsTrue(File.Exists(Path.Combine(root, folder, ".gdignore")), $"{folder} holds no .gdignore");
            foreach (var project in projects)
            {
                Assert.Contains(project, solution, $"{project} is not in OpenSettlers.slnx");
            }
        }

        // The layer libraries, the two sources, the ports, the map view, the launch code, the assets, the generator, the
        // shared test helpers, the six library helper projects, the tests, and the root test project.
        Assert.AreEqual(19, projectFolders);
    }

    // The set's files by path relative to the folder, with forward slashes.
    static List<string> SetFiles(string folder) =>
        Directory.Exists(folder)
            ?
            [
                .. Directory
                    .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/'))
                    .Order(StringComparer.Ordinal),
            ]
            : [];

    // A file's bytes; for a PNG its chunk types and data in order, with the image data decompressed.
    static byte[] Comparable(string file)
    {
        var bytes = File.ReadAllBytes(file);
        if (Path.GetExtension(file) != ".png" || bytes.Length < 8)
        {
            return bytes;
        }

        using var result = new MemoryStream();
        using var image = new MemoryStream();
        for (var at = 8; at + 12 <= bytes.Length; )
        {
            var length = (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
            var type = bytes.AsSpan(at + 4, 4);
            var data = bytes.AsSpan(at + 8, Math.Min(length, bytes.Length - at - 8));
            if (type.SequenceEqual("IDAT"u8))
            {
                image.Write(data);
            }
            else
            {
                result.Write(type);
                result.Write(data);
            }

            at += 12 + length;
        }

        image.Position = 0;
        using var inflated = new ZLibStream(image, CompressionMode.Decompress);
        result.Write("IDAT"u8);
        inflated.CopyTo(result);

        return result.ToArray();
    }

    static IEnumerable<string> Files(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            yield return file;
        }

        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            if (SkippedFolders.Contains(Path.GetFileName(child)))
            {
                continue;
            }

            foreach (var file in Files(child))
            {
                yield return file;
            }
        }
    }

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output");
    }
}
