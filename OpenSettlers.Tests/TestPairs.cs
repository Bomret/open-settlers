using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSettlers.Tests;

// Reads every project under <root>/src and names what breaks the layout of libraries, test projects, and helper
// projects (INV-0012, INV-0013, ADR 0019). A project is a folder holding a project file of any name; the folder's name
// says what it is: `<Name>.Tests` is the test project of the library `<Name>` beside it, `<Name>.Testing` its helper
// project, and `Testing` the helpers shared by every pair.
static class TestPairs
{
    sealed class Project
    {
        public required string Folder { get; init; }

        public string Name => Path.GetFileName(Folder);

        public bool IsTests => Name.EndsWith(".Tests", StringComparison.Ordinal);

        public bool IsHelper => Name == "Testing" || Name.EndsWith(".Testing", StringComparison.Ordinal);

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

        return problems;
    }

    static string Sibling(Project project, string suffix) => project.Folder + suffix;

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
            if (!IsBuildOutput(source, folder))
            {
                projects[folder] = new Project { Folder = folder };
            }
        }

        return projects;
    }
}
