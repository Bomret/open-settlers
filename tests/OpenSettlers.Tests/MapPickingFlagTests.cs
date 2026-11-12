using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Flag picking where flag boxes overlap, on a map built in code (no install). The boxes are the map view's
// ObjectDraws.FlagGeometry, 16 by 19 pixels. Two nodes one row apart are 16 pixels apart across, and two nodes in one
// row 32, so with that geometry only nodes two rows apart share a column of pixels, and only when the southern one
// is raised enough (4 pixels a height unit) to close the 20 pixel row gap.
[TestClass]
public sealed class MapPickingFlagTests
{
    private const double ViewWidth = 800;
    private const double ViewHeight = 600;

    private static Map FlagsAt(params (int Column, int Row, int Height)[] flags)
    {
        var map = new Map(MapSize.Offered[0]);
        foreach (var (column, row, height) in flags)
        {
            map.At(column, row).Object = MapObject.Flag;
            map.At(column, row).Height = height;
        }

        return map;
    }

    private static (int X, int Y) PickAt(Map map, (double X, double Y) point, MapPicking.PickBox box) =>
        MapPicking.Pick(map, point.X, point.Y, 1, ViewWidth, ViewHeight, ViewWidth / 2, ViewHeight / 2, box);

    [TestMethod]
    public void AClickInTwoOverlappingFlagBoxesPicksTheFlagOfTheSouthernRow()
    {
        // (10,10) stands at (160,200), flat: its box covers y 182 to 200. (11,12) stands at (160,240) lifted 32 to
        // 208: its box covers y 190 to 208. A click at (165,195) lies in both.
        var map = FlagsAt((10, 10, 0), (11, 12, 8));

        var picked = PickAt(map, (165, 195), ObjectDraws.FlagGeometry);

        Assert.AreEqual((11, 12), picked);
    }

    [TestMethod]
    public void TheSouthernFlagWinsWhicheverOrderTheFlagsWereLaidIn()
    {
        var map = FlagsAt((11, 12, 8), (10, 10, 0));

        var picked = PickAt(map, (165, 195), ObjectDraws.FlagGeometry);

        Assert.AreEqual((11, 12), picked);
    }

    [TestMethod]
    public void AClickInTwoOverlappingFlagBoxesAcrossTheVerticalSeamPicksTheFlagOnRowZeroOfTheCopyBelow()
    {
        // (10,30) stands at (-160,600): box y 582 to 600. (11,0) is drawn in the copy below the last row at
        // (-160,640) lifted 32 to 608: box y 590 to 608. A click at (-155,595) lies in both.
        var map = FlagsAt((10, 30, 0), (11, 0, 8));

        var picked = PickAt(map, (-155, 595), ObjectDraws.FlagGeometry);

        Assert.AreEqual((11, 0), picked);
    }

    [TestMethod]
    public void AClickInTwoFlagBoxesOfOneRowPicksTheEasternFlag()
    {
        // The map view's boxes never overlap within a row (the nodes are 32 pixels apart and the boxes 16 wide), so
        // the rule is pinned with a box of 40 pixels: the flags at (160,200) and (192,200) both hold (195,195).
        var map = FlagsAt((10, 10, 0), (11, 10, 0));

        var picked = PickAt(map, (195, 195), new MapPicking.PickBox(40, 19, 0, -18));

        Assert.AreEqual((11, 10), picked);
    }
}
