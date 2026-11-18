using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PreviewClashTests
{
    record BasePreview : Preview;

    sealed record DerivedPreview : BasePreview;

    sealed record OtherPreview : Preview;

    [TestMethod]
    public void TwoViewsOfOnePreviewTypeClashAndNameTheTypeAndBothViews()
    {
        var clash = PreviewClash.Of([("FirstView", typeof(BasePreview)), ("OtherView", typeof(BasePreview))]);

        StringAssert.Contains(clash, nameof(BasePreview));
        StringAssert.Contains(clash, "FirstView");
        StringAssert.Contains(clash, "OtherView");
    }

    [TestMethod]
    public void UniquePreviewTypesAndNoViewsGiveNoClash()
    {
        Assert.IsNull(PreviewClash.Of([("FirstView", typeof(BasePreview)), ("OtherView", typeof(OtherPreview))]));
        Assert.IsNull(PreviewClash.Of([]));
    }

    [TestMethod]
    public void ViewsOfABaseAndADerivedPreviewTypeAreNoClash()
    {
        Assert.IsNull(PreviewClash.Of([("BaseView", typeof(BasePreview)), ("DerivedView", typeof(DerivedPreview))]));
    }

    [TestMethod]
    public void AMissingInstallWinsOverTheClash()
    {
        var args = LaunchArgs.Parse([], null);

        var state = AssetBrowserState.Open(LaunchInstall.Assets(args), selectFirst: false, previewClash: "clash");

        StringAssert.Contains(state.Message, "no install given");
    }

    [TestMethod]
    public void TheClashOpensTheBrowserOnAMessageWithNoCategories()
    {
        var state = AssetBrowserState.Open(new FakeAssets(new ListedKind("sound")), previewClash: "clash");

        Assert.AreEqual("clash", state.Message);
        Assert.IsEmpty(state.Categories);
    }
}
