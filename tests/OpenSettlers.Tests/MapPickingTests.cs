using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
[TestCategory(Needs.Install)]
public sealed class MapPickingTests
{
    private const double ViewWidth = 800;
    private const double ViewHeight = 600;

    private static readonly Map Map = MapViewState.Open(TestProgram.Tables(), "1234567812345678", 1).Map!;

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
        var height = Map.At(column, row).Height;

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
        var raised = Enumerable
            .Range(0, Map.Count)
            .First(node =>
                Map.At(node).Height >= 8
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

    private static string? Aim(
        (double X, double Y) camera,
        double scale,
        int column,
        int row,
        out (double X, double Y) at
    ) => Aim(camera, scale, ViewWidth, ViewHeight, column, row, out at);

    private static string? Aim(
        (double X, double Y) camera,
        double scale,
        double width,
        double height,
        int column,
        int row,
        out (double X, double Y) at
    )
    {
        var problem = MapPicking.Aim(
            Map,
            camera.X,
            camera.Y,
            scale,
            width,
            height,
            40,
            column,
            row,
            out var x,
            out var y
        );
        at = (x, y);

        return problem;
    }

    public static IEnumerable<object[]> Views =>
        from scale in new[] { 0.25, 1, 2.5 }
        from camera in new[] { (0.0, 0.0), (300.0, 200.0), (1000.0, 630.0) }
        from size in new[] { (800.0, 600.0), (1280.0, 680.0), (320.0, 200.0) }
        select new object[] { scale, camera.Item1, camera.Item2, size.Item1, size.Item2 };

    [TestMethod]
    [DynamicData(nameof(Views))]
    public void ThePointATileIsAimedAtPicksThatTile(
        double scale,
        double cameraX,
        double cameraY,
        double width,
        double height
    )
    {
        var aimed = 0;
        for (var row = 0; row < Map.Size.Height; row++)
        {
            for (var column = 0; column < Map.Size.Width; column++)
            {
                if (Aim((cameraX, cameraY), scale, width, height, column, row, out var at) is { } problem)
                {
                    StringAssert.Contains(problem, $"{column},{row}");
                    continue;
                }

                aimed++;
                Assert.IsTrue(at.X >= 0 && at.X < width && at.Y >= 0 && at.Y < height, $"{at} is off the view");
                var picked = MapPicking.Pick(Map, cameraX, cameraY, scale, width, height, at.X, at.Y);
                Assert.AreEqual((column, row), picked, $"tile {column},{row} at scale {scale}");
            }
        }

        Assert.IsGreaterThan(0, aimed);
    }

    [TestMethod]
    [DynamicData(nameof(Views))]
    public void TheAimedPointIsTheSinglePrecisionPointAClickSends(
        double scale,
        double cameraX,
        double cameraY,
        double width,
        double height
    )
    {
        for (var row = 0; row < Map.Size.Height; row++)
        {
            for (var column = 0; column < Map.Size.Width; column++)
            {
                if (Aim((cameraX + 0.1, cameraY + 0.3), scale, width, height, column, row, out var at) is not null)
                    continue;

                Assert.AreEqual(((double)(float)at.X, (double)(float)at.Y), at, $"tile {column},{row}");
            }
        }
    }

    [TestMethod]
    public void EveryTileOfAMapThatFillsTheViewIsAimedAtExceptOneDrawnOnTheSamePointAsAnother()
    {
        for (var row = 0; row < Map.Size.Height; row++)
        {
            for (var column = 0; column < Map.Size.Width; column++)
            {
                if (Aim((512, 320), 0.5, 1280, 680, column, row, out _) is { } problem)
                    StringAssert.Contains(problem, "same point");
            }
        }
    }

    [TestMethod]
    public void ARaisedTileIsAimedAtWhereItIsDrawn()
    {
        var raised = Enumerable.Range(0, Map.Count).First(node => Map.At(node).Height >= 8);
        var (column, row) = (raised % Map.Size.Width, raised / Map.Size.Width);
        var camera = Camera.NodePosition(column, row);

        Assert.IsNull(Aim((camera.X, camera.Y), 1, column, row, out var at));
        Assert.AreEqual(Screen(column, row, (0, 0), (camera.X, camera.Y), 1), at);
    }

    [TestMethod]
    public void TheSameViewAndTileAlwaysGiveTheSamePoint()
    {
        Assert.IsNull(Aim((300, 200), 0.25, 5, 7, out var first));
        Assert.IsNull(Aim((300, 200), 0.25, 5, 7, out var second));

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void OfTheCopiesOnScreenTheOneNearestTheMiddleIsAimedAt()
    {
        // At scale 0.25 the 800 by 600 view shows the map's 1024 by 640 pixels three times across.
        Assert.IsNull(Aim((300, 200), 0.25, 5, 7, out var at));

        var nearest = new Camera(Map.Size)
            .Copies()
            .Select(copy => Screen(5, 7, copy, (300, 200), 0.25))
            .Where(p => p.X >= 0 && p.X < ViewWidth && p.Y >= 0 && p.Y < ViewHeight)
            .MinBy(p =>
                (p.X - ViewWidth / 2) * (p.X - ViewWidth / 2) + (p.Y - ViewHeight / 2) * (p.Y - ViewHeight / 2)
            );
        Assert.AreEqual(nearest, at);
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(0, -1)]
    [DataRow(32, 0)]
    [DataRow(0, 32)]
    public void ATileOutsideTheMapIsAProblem(int column, int row)
    {
        var problem = Aim((300, 200), 1, column, row, out _);

        StringAssert.Contains(problem, "outside the map");
    }

    [TestMethod]
    public void ATileWithNoCopyOnScreenIsAProblem()
    {
        var problem = Aim((300, 200), 4, 20, 20, out _);

        StringAssert.Contains(problem, "not on screen");
    }

    [TestMethod]
    public void ATileJustAboveTheViewIsUnderTheButtonBar()
    {
        // The node lies 10 screen pixels above the view's top edge, inside the 40 pixels of the bar.
        var (x, y) = DrawnPoint(10, 10);
        var camera = (x, y + ViewHeight / 2 / 4 + 10 / 4.0);

        var problem = Aim(camera, 4, 10, 10, out _);

        StringAssert.Contains(problem, "under the button bar");
    }

    // A flag's sprite of 16 by 19 pixels stands on its node: its left edge 2 west of the node, its top 19 above it.
    private static readonly ObjectImage FlagImage = new(16, 19, -2, -19, new byte[16 * 19], new byte[16 * 19]);

    [TestMethod]
    [DynamicData(nameof(Scales))]
    public void AClickOnAFlagAsItIsDrawnPicksTheFlagsNode(double scale)
    {
        var map = MapViewState.Open(TestProgram.Tables(), "1234567812345678", 1).Map!;
        var (column, row) = (12, 9);
        map.At(column, row).Object = MapObject.Flag;
        var (x, y) = DrawnPoint(column, row);
        var camera = ((double)x, (double)y);

        // The top of the cloth, 17 above the node and 10 east of it: nearer the point of another node than its own.
        var (screenX, screenY) = (ViewWidth / 2 + 10 * scale, ViewHeight / 2 - 17 * scale);
        var picked = MapPicking.Pick(
            map,
            camera.Item1,
            camera.Item2,
            scale,
            ViewWidth,
            ViewHeight,
            screenX,
            screenY,
            FlagImage
        );

        Assert.AreNotEqual(
            (column, row),
            MapPicking.Pick(map, camera.Item1, camera.Item2, scale, ViewWidth, ViewHeight, screenX, screenY)
        );
        Assert.AreEqual((column, row), picked);
    }
}
