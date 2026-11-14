using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

/// <summary>
/// A case-sensitive file system for a test class: a new folder in the temp folder on Linux, on macOS a small
/// case-sensitive APFS image mounted once. Each <see cref="NewFolder"/> is a fresh folder in it. Where none can be made,
/// <see cref="NewFolder"/> reports the test inconclusive.
/// </summary>
public sealed class CaseSensitiveVolume : IDisposable
{
    readonly string? _image;
    readonly string _root;
    readonly string? _problem;
    int _count;

    public CaseSensitiveVolume()
    {
        var name = "open-settlers-tests-" + Guid.NewGuid().ToString("N");
        if (OperatingSystem.IsMacOS())
        {
            _image = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(_image);
            _root = Path.Combine(_image, "mnt");
            Directory.CreateDirectory(_root);
            var dmg = Path.Combine(_image, "cs.dmg");
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
                ) || !Run("hdiutil", "attach", "-nobrowse", "-noverify", "-mountpoint", _root, dmg, "-quiet")
            )
            {
                Directory.Delete(_image, true);
                _image = null;
                _problem = "No case-sensitive APFS image could be made.";
                _root = "";
                return;
            }
        }
        else
        {
            _root = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(_root);
        }

        File.WriteAllBytes(Path.Combine(_root, "probe"), []);
        if (File.Exists(Path.Combine(_root, "PROBE")))
        {
            _problem = "The temp folder is not case-sensitive.";
        }

        File.Delete(Path.Combine(_root, "probe"));
    }

    /// <summary>A new, empty folder in the volume.</summary>
    public CaseSensitiveFolder NewFolder()
    {
        if (_problem != null)
        {
            Assert.Inconclusive(_problem);
        }

        var path = Path.Combine(_root, (++_count).ToString());
        Directory.CreateDirectory(path);

        return new CaseSensitiveFolder(path);
    }

    public void Dispose()
    {
        if (_root == "")
        {
            return;
        }

        if (_image == null)
        {
            Directory.Delete(_root, true);
            return;
        }

        Run("hdiutil", "detach", _root, "-force", "-quiet");
        Directory.Delete(_image, true);
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

/// <summary>A folder of a <see cref="CaseSensitiveVolume"/>, deleted on dispose.</summary>
public sealed class CaseSensitiveFolder(string root) : IDisposable
{
    /// <summary>The case-sensitive folder.</summary>
    public string Root { get; } = root;

    /// <summary>Writes one file under the folder, with its folders.</summary>
    public CaseSensitiveFolder Put(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);

        return this;
    }

    public void Dispose() => Directory.Delete(Root, true);
}
