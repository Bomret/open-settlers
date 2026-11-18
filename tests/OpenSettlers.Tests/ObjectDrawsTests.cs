using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The flag geometry against the History Edition source, which the map view cannot reach; the rest of this class is in
// src/MapView.Tests.
[TestClass]
public sealed class ObjectDrawsTests
{
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFlagGeometryIsTheBoxOfTheHistoryEditionsFlagAtRest()
    {
        var image = HistoryEditionSource
            .Load(TestProgram.OpenInstall())
            .Draw(
                new MapObjectDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Object = MapObject.Flag,
                    Step = 0,
                }
            )!
            .Image!;

        Assert.AreEqual(
            (
                ObjectDraws.FlagGeometry.Width,
                ObjectDraws.FlagGeometry.Height,
                ObjectDraws.FlagGeometry.OffsetX,
                ObjectDraws.FlagGeometry.OffsetY
            ),
            (image.Width, image.Height, image.OffsetX, image.OffsetY)
        );
    }
}
