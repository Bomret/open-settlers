using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OpenSettlers.Tests;

// Reads every project under <root>/src and names what breaks the layout of libraries, test projects, and helper
// projects (INV-0012, INV-0013, ADR 0019). A project is a folder holding a project file of any name; the folder's name
// says what it is: `<Name>.Tests` is the test project of the library `<Name>` beside it, `<Name>.Testing` its helper
// project, and `Testing` the helpers shared by every pair.
static class TestPairs
{
    const string SharedHelpers = "Testing";

    sealed class Project
    {
        public required string Folder { get; init; }

        // The folders of the projects this one references, whether or not they hold a project of src/.
        public required string[] References { get; init; }

        public string Name => Path.GetFileName(Folder);

        public bool IsTests => Name.EndsWith(".Tests", StringComparison.Ordinal);

        public bool IsHelper => Name == SharedHelpers || Name.EndsWith(".Testing", StringComparison.Ordinal);

        public bool IsLibrary => !IsTests && !IsHelper;
    }

    public static List<string> Problems(string root)
    {
        var projects = Read(root);
        var problems = new List<string>();
        foreach (var library in projects.Values.Where(project => project.IsLibrary))
        {
            if (!projects.ContainsKey(Sibling(library, ".Tests")))
            {
                problems.Add($"{library.Name} has no test project: add src/{library.Name}.Tests beside it");
            }
        }

        foreach (var project in projects.Values.Where(project => project.IsHelper || project.IsTests))
        {
            if (project.Name == SharedHelpers)
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

    static string Sibling(Project project, string suffix) => project.Folder + suffix;

    static string Describe(string folder) => Path.GetFileName(folder);

    // The shared helpers reference no library: not directly, and not through a helper project.
    static IEnumerable<string> SharedHelperProblems(Dictionary<string, Project> projects, Project shared)
    {
        foreach (var (reached, through) in Closure(projects, shared, project => project.IsLibrary))
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
        var pending = new Stack<string>([project.Folder[..project.Folder.LastIndexOf('.')]]);
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
            Describe(folder) == SharedHelpers
            || libraries.Contains(folder)
            || libraries.Any(name => folder == name + ".Testing");

        foreach (var (reached, through) in Closure(projects, project, _ => false, folder => !Allowed(folder)))
        {
            yield return through is null
                ? $"{project.Name} references {Describe(reached)}, which is outside the reach of its library: "
                    + "a test or helper project references its library, the libraries that library uses, "
                    + $"{SharedHelpers}, and the helper projects of those libraries"
                : $"{project.Name} reaches {Describe(reached)} through {Describe(through)}, which is outside the reach of its library";
        }
    }

    // The projects reached from `start` through project references, each with the project it was reached through
    // (null where `start` references it). A project is reported where `report` holds for it or, for a folder outside
    // src, where `reportFolder` holds; the walk does not continue below a reported project or below the shared helpers.
    static List<(string Reached, string? Through)> Closure(
        Dictionary<string, Project> projects,
        Project start,
        Func<Project, bool> report,
        Func<string, bool>? reportFolder = null
    )
    {
        var found = new List<(string, string?)>();
        var seen = new HashSet<string> { start.Folder };
        var pending = new Queue<(string Folder, string? Through)>(start.References.Select(reference => (reference, (string?)null)));
        while (pending.TryDequeue(out var next))
        {
            if (!seen.Add(next.Folder))
            {
                continue;
            }

            var known = projects.GetValueOrDefault(next.Folder);
            var reported = known is null
                ? reportFolder?.Invoke(next.Folder) ?? true
                : report(known) || (reportFolder?.Invoke(next.Folder) ?? false);
            if (reported)
            {
                found.Add((next.Folder, next.Through));
            }
            else if (known is not null && known.Name != SharedHelpers)
            {
                foreach (var reference in known.References)
                {
                    pending.Enqueue((reference, next.Through ?? next.Folder));
                }
            }
        }

        return found;
    }

    static bool IsBuildOutput(string folder, string path) =>
        Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar)[0] is "obj" or "bin";

    // The projects under <root>/src by folder: every folder with a project file, whatever the file is called.
    static Dictionary<string, Project> Read(string root)
    {
        var source = Path.Combine(root, "src");
        var projects = new Dictionary<string, Project>();
        foreach (var file in Directory.EnumerateFiles(source, "*.csproj", SearchOption.AllDirectories))
        {
            var folder = Path.GetFullPath(Path.GetDirectoryName(file)!);
            if (IsBuildOutput(source, folder))
            {
                continue;
            }

            var references = XDocument
                .Load(file)
                .Descendants("ProjectReference")
                .Select(item => (string)item.Attribute("Include")!)
                .Select(include => Path.GetFullPath(Path.Combine(folder, include.Replace('\\', '/'))))
                .Select(path => Path.GetDirectoryName(path)!)
                .ToArray();
            projects[folder] = new Project { Folder = folder, References = references };
        }

        return projects;
    }
}
