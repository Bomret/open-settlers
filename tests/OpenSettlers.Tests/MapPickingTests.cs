using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class MapPickingTests
{
    private const double ViewWidth = 800;
    private const double ViewHeight = 600;

    private static readonly Map Map = MapViewState.Open("1234567812345678", 1).Map!;

    public static IEnumerable<object[]> Scales =>
        new[] { 0.25, 0.8, 1, 2.5, 4 }.Select(scale => new object[] { scale });

    // The screen position at which the view draws a node of the copy with this offset: the node's drawn point, lifted
    // by its height, moved by the copy's offset, relative to the camera, scaled, around the middle of the view.
    private static (double X, double Y) Screen(
        int column,
        int row,
        (int X, int Y) copy,
        (double X, double Y) camera,
        double scale
    )
    {
        var (x, y) = DrawnPoint(column, row);

        return ((x + copy.X - camera.X) * scale + ViewWidth / 2, (y + copy.Y - camera.Y) * scale + ViewHeight / 2);
    }

    private static (int X, int Y) DrawnPoint(int column, int row)
    {
        var (x, y) = Camera.NodePosition(column, row);
        var height = Map.Layer(MapLayer.Height)[row * Map.Size.Width + column] & 31;

        return (x, y - height * TerrainTriangles.HeightStep);
    }

    private static (int X, int Y) Pick((double X, double Y) camera, double scale, (double X, double Y) screen) =>
        MapPicking.Pick(Map, camera.X, camera.Y, scale, ViewWidth, ViewHeight, screen.X, screen.Y);

    private static void AssertPicksANodeDrawnAt(
        int column,
        int row,
        (int X, int Y) copy,
        (double X, double Y) camera,
        double scale
    )
    {
        var picked = Pick(camera, scale, Screen(column, row, copy, camera, scale));

        // Two nodes of different heights can be drawn on one point; either is a node under the click.
        Assert.AreEqual(
            DrawnPoint(column, row),
            DrawnPoint(picked.X, picked.Y),
            $"node {column},{row} at scale {scale}"
        );
    }

    [TestMethod]
    [DynamicData(nameof(Scales))]
    public void AClickOnTheDrawnPointOfAnyNodePicksThatNodeAtEveryZoom(double scale)
    {
        for (var row = 0; row < Map.Size.Height; row++)
        {
            for (var column = 0; column < Map.Size.Width; column++)
            {
                AssertPicksANodeDrawnAt(column, row, (0, 0), (300, 200), scale);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(Scales))]
    public void AClickOnAWrappedCopyPicksTheNodeOfTheMap(double scale)
    {
        var camera = new Camera(Map.Size);
        camera.CenterOn(20, 10);
        foreach (var copy in camera.Copies())
        {
            foreach (var (column, row) in new[] { (0, 0), (31, 0), (0, 31), (31, 31), (17, 3), (5, 24) })
            {
                AssertPicksANodeDrawnAt(column, row, copy, (camera.X, camera.Y), scale);
            }
        }
    }

    [TestMethod]
    public void ANodeAtTheEdgeOfTheMapIsPickedWhereverTheCameraStands()
    {
        var camera = new Camera(Map.Size);
        foreach (var (column, row) in new[] { (0, 0), (31, 0), (0, 31), (31, 31) })
        {
            camera.CenterOn(column, row);
            AssertPicksANodeDrawnAt(column, row, (0, 0), (camera.X, camera.Y), 1);
            AssertPicksANodeDrawnAt(column, row, (0, 0), (camera.X, camera.Y), 3);
        }
    }

    [TestMethod]
    public void ARaisedNodeIsPickedWhereItIsDrawnNotWhereItsRowWouldPutIt()
    {
        var heights = Map.Layer(MapLayer.Height).ToArray();
        var raised = Enumerable
            .Range(0, heights.Length)
            .First(node =>
                (heights[node] & 31) >= 8
                && node % Map.Size.Width is > 3 and < 28
                && node / Map.Size.Width is > 8 and < 24
            );
        var (column, row) = (raised % Map.Size.Width, raised / Map.Size.Width);
        var flat = Camera.NodePosition(column, row);
        var drawn = DrawnPoint(column, row);
        var camera = ((double)flat.X, (double)flat.Y);

        var atDrawn = Pick(camera, 1, Screen(column, row, (0, 0), camera, 1));
        var atFlat = MapPicking.Pick(
            Map,
            camera.Item1,
            camera.Item2,
            1,
            ViewWidth,
            ViewHeight,
            ViewWidth / 2,
            ViewHeight / 2
        );

        Assert.AreEqual(drawn, DrawnPoint(atDrawn.X, atDrawn.Y));
        Assert.AreNotEqual((column, row), (atFlat.X, atFlat.Y));
    }

    [TestMethod]
    public void AClickNearAnodeButNotOnItPicksTheNearestNode()
    {
        var camera = (300.0, 200.0);
        var screen = Screen(12, 14, (0, 0), camera, 2);

        var picked = Pick(camera, 2, (screen.X + 3, screen.Y - 2));

        Assert.AreEqual(DrawnPoint(12, 14), DrawnPoint(picked.X, picked.Y));
    }
}
