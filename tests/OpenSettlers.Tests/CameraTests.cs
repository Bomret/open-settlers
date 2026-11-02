using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class CameraTests
{
    static MapSize Largest => MapSize.Offered[^1];

    [TestMethod]
    public void ThePeriodIsTheMapInPixels()
    {
        var camera = new Camera(Largest);

        Assert.AreEqual(64 * 32, camera.PeriodX);
        Assert.AreEqual(64 * 20, camera.PeriodY);
    }

    [TestMethod]
    public void MovingPastTheEastEdgeWrapsToTheWest()
    {
        var camera = new Camera(Largest);
        camera.Move(camera.PeriodX - 1, 0);
        camera.Move(3, 0);

        Assert.AreEqual(2, camera.X);
        Assert.AreEqual(0, camera.Y);
    }

    [TestMethod]
    public void MovingNorthOfTheTopWrapsToTheBottomAndShiftsBySixteenPerRow()
    {
        var size = MapSize.Offered[1];
        var camera = new Camera(size);
        camera.Move(0, -1);

        Assert.AreEqual(camera.PeriodY - 1, camera.Y);
        // The row north of the top row is the bottom row. The rows lean west by 16 pixels each, so the bottom
        // row lies 16 pixels times the rows west of the top row.
        Assert.AreEqual(camera.PeriodX - 16 * size.Height, camera.X);
    }

    [TestMethod]
    public void MovingAlongTheMapsOwnPeriodReturnsToTheStartWhateverItsShape()
    {
        var size = MapSize.Offered[1];
        var camera = new Camera(size);
        camera.Move(100, 70);
        var (x, y) = (camera.X, camera.Y);

        camera.Move(-16 * size.Height, 20 * size.Height);

        Assert.AreEqual((x, y), (camera.X, camera.Y));
    }

    [TestMethod]
    public void TheCameraReachesEveryTileOfTheLargestMap()
    {
        var size = Largest;
        var seen = new HashSet<(int, int)>();
        for (var row = 0; row < size.Height; row++)
        {
            for (var column = 0; column < size.Width; column++)
            {
                var camera = new Camera(size);
                camera.CenterOn(column, row);
                var (x, y) = Camera.NodePosition(column, row);
                camera.Move(0, 0);

                Assert.IsTrue(camera.X >= 0 && camera.X < camera.PeriodX && camera.Y >= 0 && camera.Y < camera.PeriodY);
                // Each tile has its own place: the camera, moved by the distance to the tile, lands on it.
                var other = new Camera(size);
                other.Move(x, y);
                Assert.AreEqual((other.X, other.Y), (camera.X, camera.Y));
                seen.Add((camera.X, camera.Y));
            }
        }

        Assert.HasCount(size.Width * size.Height, seen);
    }

    [TestMethod]
    public void TheVisibleCopiesCoverTheViewAroundTheCamera()
    {
        var camera = new Camera(Largest);
        camera.Move(5, 7);

        var copies = camera.Copies().ToList();

        Assert.HasCount(35, copies);
        Assert.Contains((0, 0), copies);
        Assert.Contains((camera.PeriodX, 0), copies);
        Assert.Contains((-16 * 64, 20 * 64), copies);
    }

    [TestMethod]
    public void TheCopiesCoverAViewOfTwoMapsAtEveryCameraCorner()
    {
        foreach (var size in MapSize.Offered)
        {
            foreach (var (cx, cy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                var camera = new Camera(size);
                camera.Move(cx * (camera.PeriodX - 1), cy * (camera.PeriodY - 1));
                var copies = camera.Copies().ToList();
                for (var py = camera.Y - camera.PeriodY; py <= camera.Y + camera.PeriodY; py += 10)
                {
                    for (var px = camera.X - camera.PeriodX; px <= camera.X + camera.PeriodX; px += 16)
                    {
                        Assert.IsTrue(
                            copies.Any(copy => Covers(size, copy, px, py)),
                            $"size {size.Value}, camera ({camera.X}, {camera.Y}): ({px}, {py}) is not covered"
                        );
                    }
                }
            }
        }
    }

    // Whether the copy drawn at this offset holds the pixel: its row from y, then its column from x and the lean.
    static bool Covers(MapSize size, (int X, int Y) copy, int px, int py)
    {
        var row = System.Math.Floor((py - copy.Y) / 20.0);
        var column = System.Math.Floor((px - copy.X + 16 * row) / 32.0);

        return row >= 0 && row < size.Height && column >= 0 && column < size.Width;
    }
}
