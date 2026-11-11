using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    // The content set is the output of tools/open-settlers-art.py, which draws every image from numbers, so the set holds
    // nothing derived from an install. A hand-edited or pasted file, a file the generator does not write, or a manifest
    // the generator does not write fails here. .gdignore keeps Godot from importing the PNGs and is not art. A PNG
    // compares by its chunks with the image data decompressed, since the compressed bytes may differ between zlib builds.
    // The test fails where python3 is not on the path: mise install provides it.
    [TestMethod]
    public void TheOpenSettlersContentSetIsTheGeneratorsOutput()
    {
        var root = RepositoryRoot();
        var generated = Path.Combine(Path.GetTempPath(), "open-settlers-art-" + Guid.NewGuid().ToString("N"));
        try
        {
            var info = new ProcessStartInfo("python3") { WorkingDirectory = root, RedirectStandardError = true };
            info.ArgumentList.Add("-I");
            info.ArgumentList.Add("tools/open-settlers-art.py");
            info.ArgumentList.Add(generated);
            Process process;
            try
            {
                process = Process.Start(info)!;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.Fail("python3 is not on the path; mise install provides it");
                throw;
            }

            using (process)
            {
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode, error);
            }

            var checkedIn = Path.Combine(root, "content", "open-settlers");
            var expected = SetFiles(generated);
            var actual = SetFiles(checkedIn).Where(file => file != ".gdignore").ToList();
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

            Assert.IsEmpty(
                differences,
                $"content/open-settlers/ against the generator: {string.Join(", ", differences)}"
            );
        }
        finally
        {
            if (Directory.Exists(generated))
                Directory.Delete(generated, true);
        }
    }

    // The stages of a stone heap (the object codes 0x48 to 0x4f) shrink as the stonecutter cuts it, so each stage has an image of its
    // own.
    [TestMethod]
    public void EachStageOfAStoneHeapHasItsOwnImage()
    {
        var manifest = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "content", "open-settlers", "manifest.json"))
        );
        byte[] Image(string key) =>
            File.ReadAllBytes(
                Path.Combine(
                    RepositoryRoot(),
                    "content",
                    "open-settlers",
                    manifest.RootElement.GetProperty(key).GetProperty("image").GetString()!
                )
            );

        var heaps = Enumerable.Range(0x48, 8).Select(code => Convert.ToHexString(Image($"object/{code}"))).ToList();

        Assert.HasCount(8, heaps.Distinct(), "the eight stages of a heap");
    }

    // The set's files by path relative to the folder, with forward slashes.
    static List<string> SetFiles(string folder) =>
        Directory.Exists(folder)
            ? Directory
                .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    // A file's bytes; for a PNG its chunk types and data in order, with the image data decompressed.
    static byte[] Comparable(string file)
    {
        var bytes = File.ReadAllBytes(file);
        if (Path.GetExtension(file) != ".png" || bytes.Length < 8)
            return bytes;

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
