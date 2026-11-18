using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The asset browser state on the launch's MissingInstall (internal to the launch library, so it needs the OpenSettlers.Tests
// grant and belongs to no pair); the rest of this class is in src/Assets.Tests.
[TestClass]
public sealed class AssetBrowserStateTests
{
    [TestMethod]
    public void APortWithNoUsableDataGivesOneMessageAndNoCategories()
    {
        var state = AssetBrowserState.Open(
            new MissingInstall("no install given: pass --game <dir> or set SIEDLER_DIR")
        );

        Assert.AreEqual("no install given: pass --game <dir> or set SIEDLER_DIR", state.Message);
        Assert.IsEmpty(state.Categories);
    }
}
