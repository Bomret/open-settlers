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

    // The scripts and shaders Godot writes a .uid for (Godot 4.4 and later). Scenes, resources, and imported assets carry their
    // UID elsewhere.
    static readonly string[] ScriptExtensions = [".cs", ".gd", ".gdshader", ".gdshaderinc"];

    [TestMethod]
    public void TheRepositoryHoldsNoFileOfAnInstallsFileTypes()
    {
        var found = InstallFiles(RepositoryRoot());

        Assert.IsEmpty(found, $"install file types in the repository: {string.Join(", ", found)}");
    }

    // The check judges the files git would commit: a tracked file, a force-added one, a tracked one deleted from disk, and
    // an untracked file no ignore rule covers, each by its real name and its last extension in any letter case. A gitignored
    // folder and a linked worktree under the root hold nothing it judges, and run in a linked worktree it judges that worktree's own files.
    [TestMethod]
    public void TheInstallCheckJudgesTheFilesGitWouldCommit()
    {
        var folder = Directory.CreateTempSubdirectory("install-check-").FullName;
        try
        {
            var (git, main, worktree) = PythonTool.Worktree(folder);
            void Run(params string[] arguments) =>
                Assert.AreEqual(0, PythonTool.Git(git, main, arguments).Exit, string.Join(' ', arguments));
            void Write(string path) =>
                File.WriteAllText(Path.Combine(main, path.Replace('/', Path.DirectorySeparatorChar)), "");

            Directory.CreateDirectory(Path.Combine(main, "ignored"));
            File.WriteAllText(Path.Combine(main, ".gitignore"), "ignored/\n");
            Write("ignored/hidden.pal");
            Write("ignored/forced.pal");
            Write("X.PAL");
            Write("loose.pal");
            Write("é b.pal");
            Write("a.pal.txt");
            Write("a.txt.pal");
            Run("add", "X.PAL");
            Run("add", "-f", "ignored/forced.pal");
            Write("gone.pal");
            Run("add", "gone.pal");
            File.Delete(Path.Combine(main, "gone.pal"));
            Run("worktree", "add", "-q", Path.Combine(main, "nested"));
            Write("nested/other.pal");
            File.WriteAllText(Path.Combine(worktree, "own.pal"), "");

            CollectionAssert.AreEquivalent(
                new[] { "X.PAL", "ignored/forced.pal", "gone.pal", "loose.pal", "é b.pal", "a.txt.pal" },
                InstallFiles(main)
            );
            // A git hook runs the suite with GIT_DIR set; the check must not list that repository. The case sets GIT_DIR on
            // the test process for the listing and restores it.
            var before = Environment.GetEnvironmentVariable("GIT_DIR");
            Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(main, ".git"));
            try
            {
                CollectionAssert.AreEquivalent(new[] { "own.pal" }, InstallFiles(worktree));
            }
            finally
            {
                Environment.SetEnvironmentVariable("GIT_DIR", before);
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    // A checkout that lists no file at all gives the check nothing to judge, so it fails rather than passes.
    [TestMethod]
    public void TheInstallCheckFailsOnAnEmptyListing()
    {
        var folder = Directory.CreateTempSubdirectory("empty-checkout-").FullName;
        try
        {
            var (_, main, _) = PythonTool.Worktree(folder);

            var failure = Assert.ThrowsExactly<AssertFailedException>(() => InstallFiles(main));

            StringAssert.Contains(failure.Message, main);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void TheInstallCheckFailsWithoutGit()
    {
        var failure = Assert.ThrowsExactly<AssertFailedException>(() => InstallFiles(RepositoryRoot(), searchPath: ""));

        StringAssert.Contains(failure.Message, "git");
        StringAssert.Contains(failure.Message, RepositoryRoot());
    }

    // A folder outside any work tree makes git exit non-zero. The case reports inconclusive itself where the host's temporary
    // folder lies inside a work tree.
    [TestMethod]
    public void TheInstallCheckFailsOutsideAGitCheckoutWithGitsMessage()
    {
        var folder = Directory.CreateTempSubdirectory("no-checkout-").FullName;
        try
        {
            var git = PythonTool.Locate(null, "git");
            if (git is null)
            {
                Assert.Inconclusive("git is not on the path");
            }

            var probe = PythonTool.Git(git, folder, "ls-files");
            if (probe.Exit == 0)
            {
                Assert.Inconclusive($"the temporary folder {folder} lies inside a git work tree");
            }

            var failure = Assert.ThrowsExactly<AssertFailedException>(() => InstallFiles(folder));

            StringAssert.Contains(failure.Message, "git");
            StringAssert.Contains(failure.Message, folder);
            StringAssert.Contains(failure.Message, probe.Stderr.Trim());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
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

        // Every project of the solution but the Godot project itself is left out of its compile.
        Assert.AreEqual(
            solution.Count - 1,
            projectFolders,
            "a project of the solution is not left out of the Godot compile"
        );
    }

    // Godot writes a .uid beside each script and shader in a folder it scans, so a Godot run leaves the working tree clean only
    // where each of them is tracked; a .uid beside no source, or one Godot skips, is stale.
    [TestMethod]
    [DataRow("src/Launch/A.cs", "no .uid: src/Launch/A.cs")]
    [DataRow("src/Viewer/W.gdshader", "no .uid: src/Viewer/W.gdshader")]
    [DataRow("src/Viewer/W.gdshaderinc", "no .uid: src/Viewer/W.gdshaderinc")]
    [DataRow("tools/t.gd", "no .uid: tools/t.gd")]
    [DataRow("src/Launch/Gone.cs.uid", "no source: src/Launch/Gone.cs.uid")]
    [DataRow("src/Domain/.gdignore,src/Domain/D.cs,src/Domain/D.cs.uid", "skipped by Godot: src/Domain/D.cs.uid")]
    [DataRow("src/Domain/.gdignore,src/Domain/Deep/D.cs.uid", "skipped by Godot: src/Domain/Deep/D.cs.uid")]
    [DataRow(".claude/x/D.cs,.claude/x/D.cs.uid", "skipped by Godot: .claude/x/D.cs.uid")]
    [DataRow("src/.hidden/D.cs.uid", "skipped by Godot: src/.hidden/D.cs.uid")]
    [DataRow("src/Launch/.H.cs,src/Launch/.H.cs.uid", "skipped by Godot: src/Launch/.H.cs.uid")]
    [DataRow("sub/project.godot,sub/S.cs,sub/S.cs.uid", "skipped by Godot: sub/S.cs.uid")]
    [DataRow("sub/project.godot,sub/Deep/S.cs.uid", "skipped by Godot: sub/Deep/S.cs.uid")]
    [DataRow("src/Launch/A.cs,src/Launch/a.cs.uid", "no .uid: src/Launch/A.cs,no source: src/Launch/a.cs.uid")]
    public void UidPairingNamesEachOffence(string tracked, string offences)
    {
        CollectionAssert.AreEquivalent(offences.Split(','), UidOffences(tracked.Split(',')));
    }

    // A script or shader with its .uid, a hidden one, one in a skipped folder, and a scene or resource need nothing.
    [TestMethod]
    public void UidPairingAcceptsPairedAndSkippedFilesAndNeedsNoneForScenes()
    {
        string[] tracked =
        [
            "src/Launch/A.cs",
            "src/Launch/A.cs.uid",
            "Root.cs",
            "Root.cs.uid",
            "src/Domain/.gdignore",
            "src/Domain/Deep/D.cs",
            ".github/x.gd",
            "src/Launch/.H.cs",
            "project.godot",
            "sub/project.godot",
            "sub/S.cs",
            "sub/Deep/S.gd",
            "scenes/main.tscn",
            "theme.tres",
            "content/basic/object/a.png",
        ];

        Assert.IsEmpty(UidOffences(tracked));
    }

    // The offences against Godot's .uid pairing among the tracked repository-relative paths, each named by its path.
    // The check reads the tracked files alone, so another worktree or an ignored local folder does not change its result. It
    // finds the shaders of src/Viewer, so a wrong root or skip rule cannot pass on an empty listing.
    [TestMethod]
    public void EachScriptAndShaderGodotScansKeepsItsUid()
    {
        var tracked = TrackedFiles(RepositoryRoot());

        CollectionAssert.Contains(tracked, "src/Viewer/Water.gdshader");
        CollectionAssert.Contains(tracked, "src/Viewer/Water.gdshader.uid");
        var offences = UidOffences(tracked);
        Assert.IsEmpty(offences, $"unpaired Godot .uid files: {string.Join(", ", offences)}");
    }

    [TestMethod]
    public void TheUidCheckReportsInconclusiveWithoutGit()
    {
        Assert.ThrowsExactly<AssertInconclusiveException>(() => TrackedFiles(RepositoryRoot(), searchPath: ""));
    }

    // An extracted archive, or a checkout git refuses as unsafe, is no git work tree. The case needs a temporary folder outside
    // any work tree, and reports inconclusive itself where the host's temporary folder lies inside one.
    [TestMethod]
    public void TheUidCheckReportsInconclusiveOutsideAGitWorkTree()
    {
        var folder = Directory.CreateTempSubdirectory("no-work-tree-").FullName;
        try
        {
            var git = PythonTool.Locate(null, "git");
            if (git is not null && PythonTool.Git(git, folder, "rev-parse", "--is-inside-work-tree").Exit == 0)
            {
                Assert.Inconclusive($"the temporary folder {folder} lies inside a git work tree");
            }

            Assert.ThrowsExactly<AssertInconclusiveException>(() => TrackedFiles(folder));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    // The repository's tracked files by repository-relative path, from git ls-files with every GIT_* variable of the host
    // removed. Reports inconclusive where git is not on searchPath (the host's path where null) or root is not in a git work
    // tree.
    static List<string> TrackedFiles(string root, string? searchPath = null)
    {
        var git = PythonTool.Locate(searchPath, "git");
        if (git is null)
        {
            Assert.Inconclusive("git is not on the path");
        }

        var inside = PythonTool.Git(git, root, "rev-parse", "--is-inside-work-tree");
        if (inside.Exit != 0 || inside.Stdout.Trim() != "true")
        {
            Assert.Inconclusive($"{root} is not a git work tree: {inside.Stderr.Trim()}");
        }

        var listing = PythonTool.Git(git, root, "ls-files", "-z");
        Assert.AreEqual(0, listing.Exit, $"git ls-files: {listing.Stderr}");

        return [.. listing.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)];
    }

    // The files git would commit in root whose last extension is an install file type, by repository-relative path: the
    // tracked files and the untracked files no ignore rule covers, from git ls-files with every GIT_* variable of the host
    // removed. The listing is split on NUL, so a name git would quote keeps its real form. A nested repository or linked
    // worktree lists as one folder entry with a trailing slash, which has no extension. Fails, naming git, the folder, and
    // git's error, where git is not on searchPath (the host's path where null), exits non-zero, or lists no file.
    static List<string> InstallFiles(string root, string? searchPath = null)
    {
        var git = PythonTool.Locate(searchPath, "git");
        if (git is null)
        {
            Assert.Fail($"git is not on the path, so the files git would commit in {root} cannot be listed");
        }

        var listing = PythonTool.Git(git, root, "ls-files", "--cached", "--others", "--exclude-standard", "-z");
        if (listing.Exit != 0)
        {
            Assert.Fail($"git ls-files in {root} exited {listing.Exit}: {listing.Stderr.Trim()}");
        }

        var paths = listing.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0)
        {
            Assert.Fail($"git ls-files in {root} listed no file: {listing.Stderr.Trim()}");
        }

        return
        [
            .. paths.Where(path =>
                InstallExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
            ),
        ];
    }

    // Godot skips a file or folder whose name starts with a dot, at any depth, every folder at any depth below a .gdignore, and
    // every folder at any depth below a project.godot other than the project's own (EditorFileSystem::_scan_new_dir and
    // _should_skip_directory in editor/file_system/editor_file_system.cpp, DirAccessUnix::is_hidden in
    // drivers/unix/dir_access_unix.cpp, godotengine/godot tag 4.7.2-stable). On Windows Godot reads a file's hidden attribute
    // in place of the dot; the check follows the macOS and Linux rule. Names match by exact case.
    internal static List<string> UidOffences(IReadOnlyCollection<string> tracked)
    {
        var files = tracked.ToHashSet(StringComparer.Ordinal);
        var ignored = tracked
            .Where(path =>
                path == ".gdignore"
                || path.EndsWith("/.gdignore", StringComparison.Ordinal)
                || path.EndsWith("/project.godot", StringComparison.Ordinal)
            )
            .Select(path => path[..(path.LastIndexOf('/') + 1)])
            .ToList();
        bool Skipped(string path) =>
            path.Split('/').Any(name => name.StartsWith('.'))
            || ignored.Any(folder => path.StartsWith(folder, StringComparison.Ordinal));

        var offences = new List<string>();
        foreach (var path in tracked.Order(StringComparer.Ordinal))
        {
            if (path.EndsWith(".uid", StringComparison.Ordinal))
            {
                if (Skipped(path))
                {
                    offences.Add($"skipped by Godot: {path}");
                }
                else if (!files.Contains(path[..^".uid".Length]))
                {
                    offences.Add($"no source: {path}");
                }
            }
            else if (
                ScriptExtensions.Contains(Path.GetExtension(path), StringComparer.Ordinal)
                && !Skipped(path)
                && !files.Contains(path + ".uid")
            )
            {
                offences.Add($"no .uid: {path}");
            }
        }

        return offences;
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
