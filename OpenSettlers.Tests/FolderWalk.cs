using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;

namespace OpenSettlers.Tests;

// The repository scans walk the tree once and never enter a folder they leave out, so the size and the permissions of
// .git, .superdev, bin, or obj do not matter to them.
static class FolderWalk
{
    // Every file below `folder`, dot files included, as a full path. The walk does not enter a folder whose name `skip`
    // holds for; an unreadable folder it does enter throws.
    public static IEnumerable<string> Files(string folder, Func<string, bool> skip) =>
        new FileSystemEnumerable<string>(
            folder,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = 0,
                IgnoreInaccessible = false,
            }
        )
        {
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !skip(entry.FileName.ToString()),
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
        };
}
