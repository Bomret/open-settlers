using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;

namespace OpenSettlers.Tests;

// The crash subject of the asset browser state; the launch's half of this class is in src/Launch.Tests.
[TestClass]
public sealed class MapLaunchTests
{
    [TestMethod]
    public void TheCrashSubjectIsTheSelectedAssetFile()
    {
        var asset = new Asset
        {
            Kind = "text",
            File = "x/a",
            Name = "a",
            Load = () => new NotePreview { Summary = "s" },
        };
        var state = AssetBrowserState.Open(new FakeAssets(new ListedKind("text", asset)), selectFirst: false);
        state.SelectKind("text");

        Assert.AreEqual("x/a", state.CrashSubject);
    }

    [TestMethod]
    public void WithNoSelectedAssetTheCrashSubjectIsTheSelectedKind()
    {
        var state = AssetBrowserState.Open(new FakeAssets(new ListedKind("empty")), selectFirst: false);
        state.SelectKind("empty");

        Assert.AreEqual("empty", state.CrashSubject);
    }

    [TestMethod]
    public void WithNeitherSelectedTheCrashSubjectIsNull()
    {
        var state = AssetBrowserState.Open(new FakeAssets(new ListedKind("empty")), selectFirst: false);

        Assert.IsNull(state.CrashSubject);
    }
}
