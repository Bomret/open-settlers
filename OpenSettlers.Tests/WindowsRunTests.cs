using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// windows-run.txt holds the rows the operator read from the original in Windows under a debugger
// (tools/WindowsCheck/README.md, ADR 0008). Each row, after its "Windows run | " origin, must equal the row of
// the same size, seed, and pass in map-digests.txt, which holds the emulator's values the engine matches.
[TestClass]
public sealed class WindowsRunTests
{
    const string Origin = "Windows run | ";

    [TestMethod]
    public void EveryWindowsRunRowEqualsTheEmulatorRow()
    {
        var emulator = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "map-digests.txt"));
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "windows-run.txt"))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        foreach (var row in rows)
        {
            Assert.IsTrue(
                row.StartsWith(Origin, StringComparison.Ordinal),
                $"the row names no Windows run origin: {row}"
            );
            var values = row[Origin.Length..];
            var key = string.Join(' ', values.Split(' ').Take(3));
            var expected = emulator.Single(line => line.StartsWith(key + " ", StringComparison.Ordinal));
            Assert.AreEqual(expected, values, $"the Windows run differs from the emulator at {key}");
        }
    }
}
