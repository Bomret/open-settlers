using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ZoomTests
{
    static Camera Smallest => new(MapSize.Offered[0]);

    [TestMethod]
    public void AStepInMultipliesTheScale()
    {
        var zoom = new Zoom();
        zoom.By(1, Smallest, 100, 100);

        Assert.AreEqual(Zoom.Step, zoom.Scale, 1e-6);
    }

    [TestMethod]
    public void TheScaleStopsAtTheLargest()
    {
        var zoom = new Zoom();
        zoom.By(100, Smallest, 100, 100);

        Assert.AreEqual(Zoom.Max, zoom.Scale);
    }

    [TestMethod]
    public void TheScaleStopsAtTheSmallestInASmallView()
    {
        var zoom = new Zoom();
        zoom.By(-100, Smallest, 100, 100);

        Assert.AreEqual(Zoom.Min, zoom.Scale);
    }

    [TestMethod]
    public void AWideViewCoversAtMostTwoMaps()
    {
        // The smallest map is 1,024 pixels wide: a 4,096-pixel view needs a scale of 2 to cover 2,048.
        var zoom = new Zoom();
        zoom.By(-100, Smallest, 4096, 100);

        Assert.AreEqual(2f, zoom.Scale, 1e-6);
    }
}
