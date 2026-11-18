using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenSettlers.Tests;

// Reads every project under <root>/src and names what breaks the layout of libraries, test projects, and helper
// projects (INV-0012, INV-0013, ADR 0019). A project is a folder holding a project file of any name; the folder's name
// says what it is: `<Name>.Tests` is the test project of the library `<Name>` beside it, `<Name>.Testing` its helper
// project, and `src/Testing` the helpers shared by every pair. The project files outside src and every source and build
// file of the repository are read for grants of internals and for the assembly names they take.
static class TestPairs
{
    sealed class Project
    {
        public required string Folder { get; init; }

        public required string File { get; init; }

        // How a problem names the project: its folder's name under src, its file's path below the root elsewhere.
        public required string Label { get; init; }

        // Whether this is <root>/src/Testing, the helpers shared by every pair.
        public bool Shared { get; init; }

        // What the project builds: <AssemblyName>, or the project file's name without its extension.
        public required string Assembly { get; init; }

        // The folders of the projects this one references, whether or not they hold a project of src/.
        public required string[] References { get; init; }

        public string Name => Path.GetFileName(Folder);

        public bool IsTests => Name.EndsWith(".Tests", StringComparison.Ordinal);

        public bool IsHelper => Shared || Name.EndsWith(".Testing", StringComparison.Ordinal);

        public bool IsLibrary => !IsTests && !IsHelper;

        // The folder of the library a test or helper project belongs to.
        public string LibraryFolder => Folder[..Folder.LastIndexOf('.')];
    }

    // One grant of internals: where it is written, the assembly it names (null where it names none that can be read),
    // and its text.
    sealed class Grant
    {
        public required string Origin { get; init; }

        public Project? Owner { get; init; }

        public string? Assembly { get; init; }

        public required string Text { get; init; }
    }

    public static List<string> Problems(string root)
    {
        var projects = Read(root);
        var outside = ReadOutside(root);
        var problems = new List<string>();
        foreach (var grant in Grants(root, projects, outside))
        {
            if (grant.Text.StartsWith("using ", StringComparison.Ordinal))
            {
                problems.Add(
                    $"{grant.Origin} aliases the attribute of a grant of internals ({grant.Text}): "
                        + "write the attribute by its name, so the grant can be read"
                );
            }
            else if (grant.Text.StartsWith("IgnoresAccessChecksTo", StringComparison.Ordinal))
            {
                problems.Add(
                    $"{grant.Origin} uses {grant.Text}: it reaches internals with no grant, which a library's own test project alone has, "
                        + "and then only through InternalsVisibleTo"
                );
            }
            else if (grant.Assembly is null)
            {
                problems.Add(
                    $"{grant.Origin} grants its internals to an assembly it does not name as a plain string ({grant.Text}): "
                        + "write the assembly name in full"
                );
            }
            else if (grant.Owner is not { IsLibrary: true } owner)
            {
                problems.Add(
                    $"{grant.Origin} grants its internals to {grant.Assembly}: only a library grants them, to its own test project"
                );
            }
            else if (grant.Assembly != $"OpenSettlers.{owner.Name}.Tests")
            {
                problems.Add(
                    $"{grant.Origin} grants its internals to {grant.Assembly}: a library grants them to "
                        + $"OpenSettlers.{owner.Name}.Tests alone, reflection included"
                );
            }
        }

        var testAssemblies = new HashSet<string>();
        foreach (var library in projects.Values.Where(project => project.IsLibrary))
        {
            testAssemblies.Add($"OpenSettlers.{library.Name}.Tests");
            if (!projects.ContainsKey(library.Folder + ".Tests"))
            {
                problems.Add($"{library.Name} has no test project: add src/{library.Name}.Tests beside it");
            }
        }

        foreach (
            var project in projects.Values.Where(project => project.IsTests || (project.IsHelper && !project.Shared))
        )
        {
            if (!projects.TryGetValue(project.LibraryFolder, out var library) || !library.IsLibrary)
            {
                problems.Add(
                    $"{project.Name} has no library beside it: add src/{Describe(project.LibraryFolder)} or remove the project"
                );
            }
        }

        foreach (
            var project in projects.Values.Where(project =>
                project.IsTests && project.Assembly != $"OpenSettlers.{project.Name}"
            )
        )
        {
            problems.Add(
                $"{project.Name} builds the assembly {project.Assembly}: a test project builds OpenSettlers.{project.Name}, "
                    + "the one assembly its library grants its internals to"
            );
        }

        // No other project builds the assembly a library grants its internals to: a helper project, a library, or a
        // project outside src that did would see them.
        foreach (var project in projects.Values.Where(project => !project.IsTests).Concat(outside))
        {
            if (testAssemblies.Contains(project.Assembly))
            {
                problems.Add(
                    $"{project.Label} builds the assembly {project.Assembly}: only the test project of that library builds it"
                );
            }
        }

        foreach (var project in projects.Values.Where(project => project.IsHelper || project.IsTests))
        {
            if (project.Shared)
            {
                problems.AddRange(SharedHelperProblems(projects, project));
            }
            else
            {
                problems.AddRange(ReachProblems(projects, project));
            }
        }

        return problems;
    }

    static string Describe(string folder) => Path.GetFileName(folder);

    // The shared helpers reference no library: not directly, and not through a helper project.
    static IEnumerable<string> SharedHelperProblems(Dictionary<string, Project> projects, Project shared)
    {
        foreach (var (reached, through) in Closure(projects, shared, (_, known) => known is null || known.IsLibrary))
        {
            yield return through is null
                ? $"{shared.Name} references {Describe(reached)}: the shared helpers reference no library"
                : $"{shared.Name} reaches {Describe(reached)} through {Describe(through)}: the shared helpers reference no library";
        }
    }

    // A test or helper project may reach its library, the libraries that library uses (directly or through another
    // library), the shared helpers, and the helper project of each of those libraries.
    static IEnumerable<string> ReachProblems(Dictionary<string, Project> projects, Project project)
    {
        var libraries = new HashSet<string>();
        var pending = new Stack<string>([project.LibraryFolder]);
        while (pending.TryPop(out var next))
        {
            if (libraries.Add(next) && projects.TryGetValue(next, out var found))
            {
                foreach (var reference in found.References)
                {
                    pending.Push(reference);
                }
            }
        }

        bool Allowed(string folder) =>
            projects.GetValueOrDefault(folder)?.Shared == true
            || libraries.Contains(folder)
            || libraries.Any(name => folder == name + ".Testing");

        foreach (var (reached, through) in Closure(projects, project, (folder, _) => !Allowed(folder)))
        {
            yield return through is null
                ? $"{project.Name} references {Describe(reached)}, which is outside the reach of its library: "
                    + "a test or helper project references its library, the libraries that library uses, "
                    + "src/Testing, and the helper projects of those libraries"
                : $"{project.Name} reaches {Describe(reached)} through {Describe(through)}, which is outside the reach of its library";
        }
    }

    // The projects reached from `start` through project references, each with the project it was reached through
    // (null where `start` references it). A project is reported where `report` holds for its folder and the project
    // there (null for a folder that holds none of src); the walk does not continue below a reported project or below the
    // shared helpers.
    static List<(string Reached, string? Through)> Closure(
        Dictionary<string, Project> projects,
        Project start,
        Func<string, Project?, bool> report
    )
    {
        var found = new List<(string, string?)>();
        var seen = new HashSet<string> { start.Folder };
        var pending = new Queue<(string Folder, string? Through)>(
            start.References.Select(reference => (reference, (string?)null))
        );
        while (pending.TryDequeue(out var next))
        {
            if (!seen.Add(next.Folder))
            {
                continue;
            }

            var known = projects.GetValueOrDefault(next.Folder);
            if (report(next.Folder, known))
            {
                found.Add((next.Folder, next.Through));
            }
            else if (known is { Shared: false })
            {
                foreach (var reference in known.References)
                {
                    pending.Enqueue((reference, next.Through ?? next.Folder));
                }
            }
        }

        return found;
    }

    static bool IsBuildOutput(string name) => name is "obj" or "bin" || name.StartsWith('.');

    // The files with one of the extensions below `folder`, build output and hidden folders (.git, .superdev) left out.
    static List<string> FilesUnder(string folder, params string[] extensions)
    {
        var found = new List<string>();
        var pending = new Stack<string>([folder]);
        while (pending.TryPop(out var next))
        {
            found.AddRange(
                Directory
                    .EnumerateFiles(next)
                    .Where(file => extensions.Any(extension => file.EndsWith(extension, StringComparison.Ordinal)))
            );
            foreach (
                var child in Directory
                    .EnumerateDirectories(next)
                    .Where(child => !IsBuildOutput(Path.GetFileName(child)))
            )
            {
                pending.Push(child);
            }
        }

        return found;
    }

    // The assembly of a grant of internals as the attribute or the item writes it: its name, without a public key.
    static string AssemblyOf(string written) => written.Split(',')[0].Trim();

    static readonly Regex AttributeOpening = new(
        @"\b(?:InternalsVisibleTo|IgnoresAccessChecksTo)(?:Attribute)?\s*\(",
        RegexOptions.CultureInvariant
    );

    // `using Alias = ...InternalsVisibleToAttribute;`: the attribute is then written under another name.
    static readonly Regex AttributeAlias = new(
        @"\busing\s+\w+\s*=\s*(?:[\w.]*\.)?(?:InternalsVisibleTo|IgnoresAccessChecksTo)(?:Attribute)?\s*;",
        RegexOptions.CultureInvariant
    );

    static readonly Regex VisibleToType = new(
        @"(?:^|\.)InternalsVisibleTo(?:Attribute)?$",
        RegexOptions.CultureInvariant
    );

    static readonly Regex AttributeArgument = new(
        @"\G\s*(?:assemblyName\s*:\s*)?(?:@""(?<name>[^""]*)""|""(?<name>[^""\\]*)"")\s*\)",
        RegexOptions.CultureInvariant
    );

    // The grants a project file or a build file states: an InternalsVisibleTo item, and an AssemblyAttribute item of that
    // attribute, whose parameter is a metadata attribute or element. The assembly is null where none is stated or the text
    // holds a property expression.
    static IEnumerable<(string? Assembly, string Text)> ItemGrants(XDocument document)
    {
        foreach (var item in document.Descendants())
        {
            // MSBuild item types are case-insensitive.
            var kind = item.Name.LocalName.ToLowerInvariant();
            var include = (string?)item.Attribute("Include") ?? "";
            string? written;
            if (kind == "internalsvisibleto")
            {
                written = (string?)(item.Attribute("Include") ?? item.Attribute("Update"));
            }
            else if (kind == "assemblyattribute" && VisibleToType.IsMatch(include))
            {
                written = (string?)item.Attribute("_Parameter1") ?? (string?)item.Element("_Parameter1");
            }
            else if (kind == "ignoresaccesschecksto")
            {
                yield return (null, $"IgnoresAccessChecksTo {include}");
                continue;
            }
            else
            {
                continue;
            }

            var assembly = written is null ? null : AssemblyOf(written);
            yield return (
                assembly is null || assembly.Contains("$(") || assembly.Length == 0 ? null : assembly,
                written ?? item.ToString()
            );
        }
    }

    // The attributes of a source file. An attribute whose argument is no plain string literal names no readable assembly.
    static IEnumerable<(string? Assembly, string Text)> SourceGrants(string text)
    {
        foreach (Match opening in AttributeOpening.Matches(text))
        {
            var argument = AttributeArgument.Match(text, opening.Index + opening.Length);
            var rest = text[opening.Index..Math.Min(text.Length, opening.Index + 80)].Split('\n')[0].Trim();
            yield return argument.Success ? (AssemblyOf(argument.Groups["name"].Value), rest) : (null, rest);
        }

        foreach (Match alias in AttributeAlias.Matches(text))
        {
            yield return (null, alias.Value);
        }
    }

    // Every grant of internals in the repository: in the project files of src and outside it, in every source file, and
    // in every build file (props and targets files apply to more than one project).
    static List<Grant> Grants(string root, Dictionary<string, Project> projects, List<Project> outside)
    {
        var grants = new List<Grant>();
        foreach (var project in projects.Values.Concat(outside))
        {
            foreach (var (assembly, text) in ItemGrants(XDocument.Load(project.File)))
            {
                grants.Add(
                    new Grant
                    {
                        Origin = project.Label,
                        Owner = projects.GetValueOrDefault(project.Folder),
                        Assembly = assembly,
                        Text = text,
                    }
                );
            }
        }

        foreach (var file in FilesUnder(root, ".props", ".targets"))
        {
            foreach (var (assembly, text) in ItemGrants(XDocument.Load(file)))
            {
                grants.Add(
                    new Grant
                    {
                        Origin =
                            $"{Path.GetRelativePath(root, file).Replace('\\', '/')}, a build file shared by several projects,",
                        Assembly = assembly ?? "<unreadable>",
                        Text = text,
                    }
                );
            }
        }

        var fullRoot = Path.GetFullPath(root);
        foreach (var file in FilesUnder(root, ".cs"))
        {
            // These two files hold the grants they test as text.
            if (
                Path.GetRelativePath(root, file).Replace('\\', '/')
                is "OpenSettlers.Tests/TestPairs.cs"
                    or "OpenSettlers.Tests/TestPairTests.cs"
            )
            {
                continue;
            }

            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            while (folder is not null && !projects.ContainsKey(folder) && folder.Length > fullRoot.Length)
            {
                folder = Path.GetDirectoryName(folder);
            }

            var owner = folder is not null && projects.TryGetValue(folder, out var found) ? found : null;
            var origin = owner?.Name ?? Path.GetRelativePath(root, Path.GetDirectoryName(file)!).Replace('\\', '/');
            foreach (var (assembly, text) in SourceGrants(File.ReadAllText(file)))
            {
                grants.Add(
                    new Grant
                    {
                        Origin = origin,
                        Owner = owner,
                        Assembly = assembly,
                        Text = text,
                    }
                );
            }
        }

        return grants;
    }

    // The projects under <root>/src by folder: every folder with a project file, whatever the file is called.
    static Dictionary<string, Project> Read(string root)
    {
        var shared = Path.GetFullPath(Path.Combine(root, "src", "Testing"));
        var projects = new Dictionary<string, Project>();
        foreach (var file in FilesUnder(Path.Combine(root, "src"), ".csproj"))
        {
            var folder = Path.GetFullPath(Path.GetDirectoryName(file)!);
            projects[folder] = Load(file, Path.GetFileName(folder), folder == shared);
        }

        return projects;
    }

    // The project files outside <root>/src (the root project and the repository's own test project), named by their
    // path below the root.
    static List<Project> ReadOutside(string root)
    {
        var source = Path.GetFullPath(Path.Combine(root, "src"));
        return
        [
            .. FilesUnder(root, ".csproj")
                .Where(file =>
                    !Path.GetFullPath(file).StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                )
                .Select(file => Load(file, Path.GetRelativePath(root, file).Replace('\\', '/'), false)),
        ];
    }

    static Project Load(string file, string label, bool shared)
    {
        var folder = Path.GetFullPath(Path.GetDirectoryName(file)!);
        var document = XDocument.Load(file);
        return new Project
        {
            Folder = folder,
            File = file,
            Label = label,
            Shared = shared,
            Assembly =
                document.Descendants("AssemblyName").Select(item => item.Value.Trim()).LastOrDefault()
                ?? Path.GetFileNameWithoutExtension(file),
            References =
            [
                .. document
                    .Descendants("ProjectReference")
                    .Select(item => (string)item.Attribute("Include")!)
                    .Select(include => Path.GetFullPath(Path.Combine(folder, include.Replace('\\', '/'))))
                    .Select(path => Path.GetDirectoryName(path)!),
            ],
        };
    }
}
