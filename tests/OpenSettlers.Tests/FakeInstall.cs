using System;
using System.IO;

namespace OpenSettlers.Tests;

/// <summary>An install built in a temporary folder, with the files a test puts in it. Delete it on dispose.</summary>
public sealed class FakeInstall : IDisposable
{
    public FakeInstall(bool withProgram = true)
    {
        Root = Path.Combine(Path.GetTempPath(), "open-settlers-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        if (withProgram)
        {
            Put("Siedler1.exe", [1, 2, 3]);
        }
    }

    public string Root { get; }

    /// <summary>Writes one file under the root, with its folders.</summary>
    public FakeInstall Put(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);

        return this;
    }

    public void Dispose() => Directory.Delete(Root, true);
}
