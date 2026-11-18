using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The asset check's and the browser's reading of an install; the launch's half of this class is in src/Launch.Tests.
[TestClass]
public sealed class InstallTests
{
    [TestMethod]
    public void TheCheckAndTheBrowserNeverWriteToTheInstall()
    {
        using var fake = new FakeInstall()
            .Put("snd/01.SND", [0x80, 0x90])
            .Put("loca/SPAD.PA", ArchiveBytes.Build(8, new Dictionary<int, byte[]> { [3] = new byte[768], [5] = [1] }));
        foreach (var file in Directory.EnumerateFiles(fake.Root, "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTimeUtc(file, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        var before = Snapshot(fake.Root);
        var args = LaunchArgs.Parse(["--game", fake.Root], null);
        var assets = LaunchInstall.Assets(args);
        AssetCheck.Run(args, assets);
        var state = AssetBrowserState.Open(assets);
        foreach (var category in state.Categories)
        {
            state.SelectKind(category);
            for (var i = 0; i < state.Assets.Count; i++)
            {
                state.SelectAsset(i);
            }
        }

        CollectionAssert.AreEqual(before, Snapshot(fake.Root));
    }

    // Each file under the root with its size and modification time, in path order.
    static List<string> Snapshot(string root) =>
        [
            .. Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(f => $"{f} {new FileInfo(f).Length} {File.GetLastWriteTimeUtc(f):O}"),
        ];
}
