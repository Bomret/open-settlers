using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Flag picking where flag boxes overlap, on a map built in code (no install). The boxes are the map view's
// ObjectDraws.FlagGeometry, 16 by 19 pixels. Two nodes one row apart are 16 pixels apart across, and two nodes in one
// row 32, so with that geometry only nodes an even number of rows apart share a column of pixels. They overlap when
// the southern one is raised enough (4 pixels a height unit, up to 124) to close the 20 pixels a row gap.
[TestClass]
public sealed class MapPickingFlagTests
{
    const double ViewWidth = 800;
    const double ViewHeight = 600;

    static Map FlagsAt(params (int Column, int Row, int Height)[] flags)
    {
        var map = new Map(MapSize.Offered[0]);
        foreach (var (column, row, height) in flags)
        {
            map.At(column, row).Object = MapObject.Flag;
            map.At(column, row).Height = height;
        }

        return map;
    }

    static (int X, int Y) PickAt(Map map, (double X, double Y) point, MapPicking.PickBox box) =>
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
    public void AClickInTwoOverlappingFlagBoxesAcrossTheVerticalSeamPicksTheFlagOnRowZeroOfTheCopyBelow()
    {
        // (10,30) stands at (-160,600): box y 582 to 600. (11,0) is drawn in the copy below the last row at
        // (-160,640) lifted 32 to 608: box y 590 to 608. A click at (-155,595) lies in both.
        var map = FlagsAt((10, 30, 0), (11, 0, 8));

        var picked = PickAt(map, (-155, 595), ObjectDraws.FlagGeometry);

        Assert.AreEqual((11, 0), picked);
    }

    [TestMethod]
    public void AClickOutsideEveryFlagBoxPicksTheNearestNode()
    {
        // The flag at (10,10) stands at (160,200): its box (FlagGeometry: x node to node + 15, y node - 18 to node)
        // covers x 160 to 175, y 182 to 200. A click at (180,200) lies 5 pixels east of the box. The nearest node is (11,10) at (192,200), 12 pixels away; the flag is 20 away.
        var map = FlagsAt((10, 10, 0));

        var picked = PickAt(map, (180, 200), ObjectDraws.FlagGeometry);

        Assert.AreEqual((11, 10), picked);
    }

    [TestMethod]
    public void AClickInTheBoxOfAFlagInTheCopyToTheWestPicksTheFlag()
    {
        // (31,10) stands at (832,200) and in the copy one map width (1024) to the west at (-192,200): box x -192 to
        // -177, y 182 to 200. A click at (-182,183), the top of the cloth, lies in it; the node nearest to it is (31,9)
        // at (-176,180), 6.7 pixels away, and the flag's node is about 19.7 away (sqrt(10^2 + 17^2)).
        var map = FlagsAt((31, 10, 0));

        var picked = PickAt(map, (-182, 183), ObjectDraws.FlagGeometry);

        Assert.AreEqual((31, 10), picked);
    }

    [TestMethod]
    public void AClickInTwoFlagBoxesOfOneRowPicksTheEasternFlag()
    {
        // The map view's boxes never overlap within a row (the nodes are 32 pixels apart and the boxes 16 wide), so
        // the rule is pinned with a box of 40 pixels centred on the node: the flags at (160,200) and (192,200) hold
        // x 140 to 180 and x 172 to 212, and both hold (174,198), whose nearest node is the western flag.
        var map = FlagsAt((10, 10, 0), (11, 10, 0));

        var picked = PickAt(map, (174, 198), new MapPicking.PickBox(40, 19, -20, -18));

        Assert.AreEqual((11, 10), picked);
    }
}
