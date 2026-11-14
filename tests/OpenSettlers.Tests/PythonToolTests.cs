using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The shared python-spawn helper: a missing python3 fails the test with the hint to run mise install
// (docs/agents/verification.md), and the child's environment is the one the caller sets.
[TestClass]
public sealed class PythonToolTests
{
    string _folder = "";

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "python-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    [TestMethod]
    public void AMissingPython3FailsTheTestWithTheMiseInstallHint()
    {
        var exception = Assert.ThrowsExactly<AssertFailedException>(() => PythonTool.Resolve(_folder));

        StringAssert.Contains(exception.Message, "mise install");
    }

    [TestMethod]
    public void APython3ThatCannotBeStartedFailsTheTestWithTheMiseInstallHint()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("a file without the execute bit is a Unix case");
            return;
        }

        var script = Path.Combine(_folder, "python3");
        File.WriteAllText(script, "#!/bin/sh\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var exception = Assert.ThrowsExactly<AssertFailedException>(() =>
            PythonTool.Start(new ProcessStartInfo(script))
        );

        StringAssert.Contains(exception.Message, "mise install");
    }

    [TestMethod]
    public void AStartFailureNamesItsCauseBesideTheHint()
    {
        var missing = Path.Combine(_folder, "no-such-directory");

        var exception = Assert.ThrowsExactly<AssertFailedException>(() =>
            PythonTool.Start(new ProcessStartInfo("sh") { WorkingDirectory = missing })
        );

        StringAssert.Contains(exception.Message, "mise install");
        StringAssert.Contains(exception.Message, missing, "the cause: the working directory that does not exist");
    }

    [TestMethod]
    public void AnEmptyPathFailsTheTestWithTheMiseInstallHint()
    {
        var exception = Assert.ThrowsExactly<AssertFailedException>(() => PythonTool.Resolve(""));

        StringAssert.Contains(exception.Message, "mise install");
    }

    [TestMethod]
    public void APython3OnThePathIsFoundAsItsFullPath()
    {
        var name = OperatingSystem.IsWindows() ? "python3.exe" : "python3";
        File.WriteAllText(Path.Combine(_folder, name), "");

        Assert.AreEqual(Path.Combine(_folder, name), PythonTool.Resolve(_folder));
    }

    [TestMethod]
    public void TheChildGetsNoSiedlerDirUnlessTheCallerPassesOne()
    {
        var script = Path.Combine(_folder, "env.py");
        File.WriteAllText(script, "import os, sys\nprint(os.environ.get('SIEDLER_DIR', 'unset'))\n");
        Assert.AreEqual("unset", PythonTool.Run(_folder, [script]).Stdout.Trim(), "whatever the host sets");
        Assert.AreEqual("given", PythonTool.Run(_folder, [script], siedlerDir: "given").Stdout.Trim());
    }
}
