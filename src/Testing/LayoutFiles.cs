using System;
using System.IO;

namespace OpenSettlers.Testing;

/// <summary>The layout files of the repository.</summary>
public static class LayoutFiles
{
    /// <summary>The <c>layouts</c> folder of the repository, found above the test output.</summary>
    public static string Folder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
        {
            folder = folder.Parent;
        }

        return Path.Combine(
            folder?.FullName
                ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output"),
            "layouts"
        );
    }
}
