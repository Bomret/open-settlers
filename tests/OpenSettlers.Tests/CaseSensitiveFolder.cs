using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

/// <summary>
/// A folder on a case-sensitive file system, deleted on dispose: a new folder in the temp folder on Linux, on macOS
/// a small case-sensitive APFS image mounted for the test. Where none can be made, the test reports inconclusive.
/// </summary>
public sealed class CaseSensitiveFolder : IDisposable
{
    readonly string? image;

    public CaseSensitiveFolder()
    {
        var name = "open-settlers-tests-" + Guid.NewGuid().ToString("N");
        if (OperatingSystem.IsMacOS())
        {
            image = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(image);
            Root = Path.Combine(image, "mnt");
            Directory.CreateDirectory(Root);
            var dmg = Path.Combine(image, "cs.dmg");
            if (
                !Run(
                    "hdiutil",
                    "create",
                    "-size",
                    "16m",
                    "-fs",
                    "Case-sensitive APFS",
                    "-volname",
                    "oscs",
                    "-type",
                    "UDIF",
                    dmg,
                    "-quiet"
                ) || !Run("hdiutil", "attach", "-nobrowse", "-noverify", "-mountpoint", Root, dmg, "-quiet")
            )
            {
                Directory.Delete(image, true);
                Assert.Inconclusive("No case-sensitive APFS image could be made.");
            }
        }
        else
        {
            Root = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(Root);
        }

        File.WriteAllBytes(Path.Combine(Root, "probe"), []);
        if (File.Exists(Path.Combine(Root, "PROBE")))
        {
            Dispose();
            Assert.Inconclusive("The temp folder is not case-sensitive.");
        }

        File.Delete(Path.Combine(Root, "probe"));
    }

    /// <summary>The case-sensitive folder.</summary>
    public string Root { get; }

    /// <summary>Writes one file under the folder, with its folders.</summary>
    public CaseSensitiveFolder Put(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);

        return this;
    }

    public void Dispose()
    {
        if (image == null)
        {
            Directory.Delete(Root, true);
            return;
        }

        Run("hdiutil", "detach", Root, "-force", "-quiet");
        Directory.Delete(image, true);
    }

    static bool Run(string program, params string[] args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(program, args) { RedirectStandardError = true })!;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
