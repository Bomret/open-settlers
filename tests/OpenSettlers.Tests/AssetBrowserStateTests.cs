using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class AssetBrowserStateTests
{
    sealed record TextPreview : Preview;

    sealed class ListedKind(string name, params Asset[] assets) : AssetKind
    {
        public override string Name => name;

        public override IEnumerable<Asset> Enumerate(Install install) => assets;
    }

    static Asset Item(string kind, string name, Func<Preview>? load = null) =>
        new()
        {
            Kind = kind,
            File = $"x/{name}",
            Name = name,
            Load = load ?? (() => new TextPreview { Summary = $"preview of {name}" }),
        };

    static AssetBrowserState Open(FakeInstall fake, params AssetKind[] kinds) =>
        AssetBrowserState.Open(LaunchArgs.Parse(["--game", fake.Root], null), kinds);

    [TestMethod]
    public void WithoutAnInstallTheBrowserHoldsOneMessageThatNamesBothWaysToGiveOne()
    {
        var state = AssetBrowserState.Open(LaunchArgs.Parse([], null), [new SoundKind()]);

        StringAssert.Contains(state.Message, "--game");
        StringAssert.Contains(state.Message, "SIEDLER_DIR");
        Assert.IsEmpty(state.Categories);
    }

    [TestMethod]
    public void AFolderWithoutTheProgramShowsTheMessageToo()
    {
        using var fake = new FakeInstall(withProgram: false);

        var state = Open(fake, new SoundKind());

        StringAssert.Contains(state.Message, "Siedler1.exe");
        StringAssert.Contains(state.Message, "SIEDLER_DIR");
    }

    [TestMethod]
    public void TheCategoriesAreTheKindsByNameAndTheFirstIsSelectedWithItsFirstAsset()
    {
        using var fake = new FakeInstall();
        var state = Open(
            fake,
            new ListedKind("text", Item("text", "a")),
            new ListedKind("palette", Item("palette", "p"))
        );

        CollectionAssert.AreEqual(new[] { "palette", "text" }, state.Categories.ToList());
        Assert.AreEqual("palette", state.SelectedKind);
        Assert.AreEqual("p", state.SelectedAsset!.Name);
        Assert.IsNull(state.Message);
    }

    [TestMethod]
    public void SelectingAKindListsItsAssetsInTheOrderTheKindGaveThem()
    {
        using var fake = new FakeInstall().Put("snd/10.SND", [1]).Put("snd/02.SND", [1]);
        var state = Open(fake, new ListedKind("text", Item("text", "a")), new SoundKind());

        state.SelectKind("sound");

        CollectionAssert.AreEqual(new[] { "02.SND", "10.SND" }, state.Assets.Select(a => a.Name).ToList());
        Assert.AreEqual("02.SND", state.SelectedAsset!.Name);
    }

    [TestMethod]
    public void SelectingAnAssetShowsItsPreview()
    {
        using var fake = new FakeInstall();
        var state = Open(fake, new ListedKind("text", Item("text", "a"), Item("text", "b")));

        state.SelectAsset(1);

        Assert.AreEqual("b", state.SelectedAsset!.Name);
        Assert.AreEqual("preview of b", state.Preview!.Summary);
        Assert.IsNull(state.Failure);
    }

    [TestMethod]
    public void AFailedAssetShowsItsReasonInPlaceOfAPreview()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", []);
        var state = Open(fake, new SoundKind());

        Assert.IsNull(state.Preview);
        StringAssert.Contains(state.Failure, "empty");
    }

    [TestMethod]
    public void AFailedAssetDoesNotStopTheNextSelection()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", []).Put("snd/02.SND", [0x80]);
        var state = Open(fake, new SoundKind());

        state.SelectAsset(1);

        Assert.IsNotNull(state.Preview);
        Assert.IsNull(state.Failure);
    }

    [TestMethod]
    public void AKindThatCannotListItsAssetsShowsANoticeAndNoAssets()
    {
        using var fake = new FakeInstall();
        var state = Open(fake, new ThrowingKind());

        Assert.IsEmpty(state.Assets);
        StringAssert.Contains(state.Notice, "cannot list");
        Assert.IsNull(state.SelectedAsset);
    }

    sealed class ThrowingKind : AssetKind
    {
        public override string Name => "broken";

        public override IEnumerable<Asset> Enumerate(Install install) => throw new IOException("cannot list");
    }

    [TestMethod]
    public void TwoKindsOfTheSameNameShowOneMessageAndNoCategoriesInsteadOfThrowing()
    {
        using var fake = new FakeInstall();

        var state = Open(fake, new ListedKind("text", Item("text", "a")), new ListedKind("text", Item("text", "b")));

        StringAssert.Contains(state.Message, "\"text\"");
        Assert.IsEmpty(state.Categories);
    }

    [TestMethod]
    public void TheRealKindsHaveUniqueNamesAndStampTheirAssetsWithTheirOwnName()
    {
        // One minimal file per kind, built here: every entry of the three archives is one byte, so each kind's normal
        // listing path runs and not only its missing-file path.
        var archive = ArchiveBytes.Build(4000, Enumerable.Range(1, 3999).ToDictionary(n => n, _ => new byte[] { 0 }));
        byte[] midi =
        [
            .. "MThd"u8.ToArray(),
            0,
            0,
            0,
            6,
            0,
            0,
            0,
            1,
            0,
            96,
            .. "MTrk"u8.ToArray(),
            0,
            0,
            0,
            4,
            0,
            0xFF,
            0x2F,
            0,
        ];
        using var fake = new FakeInstall()
            .Put("loca/SPAD.PA", archive)
            .Put("loca/SPAE.PA", archive)
            .Put("loca/SPAF.PA", archive)
            .Put("snd/01.SND", [1])
            .Put("snd/music_1.midi", midi)
            .Put("loca/eng.txt", "A=\"x\"\r\n"u8.ToArray())
            .Put("loca/ger.txt", "A=\"y\"\r\n"u8.ToArray())
            .Put("loca/fre.txt", "A=\"z\"\r\n"u8.ToArray());
        var kinds = AssetKind.Discover().Where(k => k.GetType().Namespace == "OpenSettlers.Assets").ToList();

        Assert.AreEqual(kinds.Count, kinds.Select(k => k.Name).Distinct().Count());
        foreach (var kind in kinds)
        {
            var assets = kind.Enumerate(new Install { Root = fake.Root }).ToList();

            Assert.IsNotEmpty(assets, kind.Name);
            Assert.IsTrue(assets.All(a => a.Kind == kind.Name), kind.Name);
        }
    }

    [TestMethod]
    public void SelectingAnUnknownKindOrAssetChangesNothing()
    {
        using var fake = new FakeInstall();
        var state = Open(fake, new ListedKind("text", Item("text", "a")));

        state.SelectKind("nope");
        state.SelectAsset(5);

        Assert.AreEqual("text", state.SelectedKind);
        Assert.AreEqual("a", state.SelectedAsset!.Name);
    }

    [TestMethod]
    public void AnArchiveKindShowsAMissingArchiveAsTheFailureOfItsSelectedAsset()
    {
        using var fake = new FakeInstall();

        var state = Open(fake, new PaletteKind());

        StringAssert.Contains(state.Failure, "loca/SPAD.PA");
    }
}
