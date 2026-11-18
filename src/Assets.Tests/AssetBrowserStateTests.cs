using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

/// <summary>The asset browser's state on a fake assets port: no install on disk.</summary>
[TestClass]
public sealed class AssetBrowserStateTests
{
    static Asset Item(string kind, string name, Func<Preview>? load = null) =>
        new()
        {
            Kind = kind,
            File = $"x/{name}",
            Name = name,
            Load = load ?? (() => new NotePreview { Summary = $"preview of {name}" }),
        };

    static AssetBrowserState Open(params IAssetKind[] kinds) => AssetBrowserState.Open(new FakeAssets(kinds));

    [TestMethod]
    public void APortWithNoUsableDataGivesOneMessageAndNoCategories()
    {
        var state = AssetBrowserState.Open(LaunchInstall.Assets(LaunchArgs.Parse([], null)));

        Assert.AreEqual($"no install given: {LaunchInstall.HowToGive}", state.Message);
        Assert.IsEmpty(state.Categories);
    }

    [TestMethod]
    public void TwoKindsOfOneNameOpenTheBrowserOnAMessageThatNamesIt()
    {
        var state = Open(new ListedKind("text", Item("text", "a")), new ListedKind("text", Item("text", "b")));

        StringAssert.Contains(state.Message, "\"text\"");
        Assert.IsEmpty(state.Categories);
    }

    [TestMethod]
    public void KindsOfUniqueNamesOpenTheBrowserWithNoMessage()
    {
        var state = Open(new ListedKind("text"), new ListedKind("palette"));

        Assert.IsNull(state.Message);
    }

    [TestMethod]
    public void APreviewClashShowsItsMessageAndNoCategories()
    {
        var state = AssetBrowserState.Open(
            new FakeAssets(new ListedKind("text")),
            previewClash: "two preview views draw X"
        );

        Assert.AreEqual("two preview views draw X", state.Message);
        Assert.IsEmpty(state.Categories);
    }

    [TestMethod]
    public void TheCategoriesAreTheKindsByNameAndTheFirstIsSelectedWithItsFirstAsset()
    {
        var state = Open(new ListedKind("text", Item("text", "a")), new ListedKind("palette", Item("palette", "p")));

        CollectionAssert.AreEqual(new[] { "palette", "text" }, state.Categories.ToList());
        Assert.AreEqual("palette", state.SelectedKind);
        Assert.AreEqual("p", state.SelectedAsset!.Name);
        Assert.IsNull(state.Message);
    }

    [TestMethod]
    public void SelectingAKindListsItsAssetsInTheOrderTheKindGaveThem()
    {
        var state = Open(
            new ListedKind("text", Item("text", "a")),
            new ListedKind("sound", Item("sound", "02.SND"), Item("sound", "10.SND"))
        );

        state.SelectKind("sound");

        CollectionAssert.AreEqual(new[] { "02.SND", "10.SND" }, state.Assets.Select(a => a.Name).ToList());
        Assert.AreEqual("02.SND", state.SelectedAsset!.Name);
    }

    [TestMethod]
    public void SelectingAnAssetShowsItsPreview()
    {
        var state = Open(new ListedKind("text", Item("text", "a"), Item("text", "b")));

        state.SelectAsset(1);

        Assert.AreEqual("b", state.SelectedAsset!.Name);
        Assert.AreEqual("preview of b", state.Preview!.Summary);
        Assert.IsNull(state.Failure);
    }

    [TestMethod]
    public void AFailedAssetShowsItsReasonInPlaceOfAPreview()
    {
        var empty = Item("sound", "01.SND", () => throw new InvalidDataException("the file is empty"));

        var state = Open(new ListedKind("sound", empty));

        Assert.IsNull(state.Preview);
        StringAssert.Contains(state.Failure, "empty");
    }

    [TestMethod]
    public void AFailedAssetDoesNotStopTheNextSelection()
    {
        var empty = Item("sound", "01.SND", () => throw new InvalidDataException("the file is empty"));
        var state = Open(new ListedKind("sound", empty, Item("sound", "02.SND")));

        state.SelectAsset(1);

        Assert.IsNotNull(state.Preview);
        Assert.IsNull(state.Failure);
    }

    [TestMethod]
    public void AKindThatCannotListItsAssetsShowsANoticeAndNoAssets()
    {
        var state = Open(new ListedKind("broken") { ListingFails = new IOException("cannot list") });

        Assert.IsEmpty(state.Assets);
        StringAssert.Contains(state.Notice, "cannot list");
        Assert.IsNull(state.SelectedAsset);
    }

    [TestMethod]
    public void AProgrammingErrorWhileLoadingAPreviewIsNotShownAsAFailure()
    {
        var bug = Item("sprite", "A", () => throw new IndexOutOfRangeException("reader bug"));

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => Open(new ListedKind("sprite", bug)));
    }

    [TestMethod]
    public void AnAssetThatThrowsLeavesNoPreviewOrFailureOfThePreviousAsset()
    {
        var failed = Item("text", "f", () => throw new InvalidDataException("bad"));
        var bug = Item("text", "bug", () => throw new IndexOutOfRangeException("reader bug"));
        var state = Open(new ListedKind("text", Item("text", "a"), failed, bug));

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => state.SelectAsset(2));
        Assert.AreSame(bug, state.SelectedAsset);
        Assert.IsNull(state.Preview);
        Assert.IsNull(state.Failure);

        state.SelectAsset(1);
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => state.SelectAsset(2));
        Assert.IsNull(state.Failure);
    }

    [TestMethod]
    public void AKindThatThrowsWhileListingLeavesNoAssetsOfThePreviousKind()
    {
        var buggy = new ListedKind("buggy") { ListingFails = new IndexOutOfRangeException("bug") };
        var state = Open(new ListedKind("a", Item("a", "x")), buggy);

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => state.SelectKind("buggy"));
        Assert.AreEqual("buggy", state.SelectedKind);
        Assert.IsEmpty(state.Assets);
        Assert.IsNull(state.SelectedAsset);
        Assert.IsNull(state.Preview);
        Assert.IsNull(state.Notice);

        state.SelectKind("a");
        Assert.AreEqual("x", state.SelectedAsset!.Name);
    }

    [TestMethod]
    public void OpeningWithoutTheFirstSelectionLeavesItToTheCaller()
    {
        var buggy = new ListedKind("buggy") { ListingFails = new IndexOutOfRangeException("bug") };
        var state = AssetBrowserState.Open(new FakeAssets(buggy), false);

        Assert.IsNull(state.SelectedKind);
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => state.SelectKind(state.Categories[0]));
        Assert.AreEqual("buggy", state.SelectedKind);
    }

    [TestMethod]
    public void SelectingAnUnknownKindOrAssetChangesNothing()
    {
        var state = Open(new ListedKind("text", Item("text", "a")));

        state.SelectKind("nope");
        state.SelectAsset(5);

        Assert.AreEqual("text", state.SelectedKind);
        Assert.AreEqual("a", state.SelectedAsset!.Name);
    }

    [TestMethod]
    public void AKindThatListsNoAssetsShowsANotice()
    {
        var state = Open(new ListedKind("unrecovered"));

        Assert.AreEqual("no unrecovered assets", state.Notice);
    }
}
