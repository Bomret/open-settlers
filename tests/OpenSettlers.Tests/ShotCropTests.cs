using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ShotCropTests
{
    private const double ViewWidth = 800;
    private const double ViewHeight = 600;
    private const double OriginX = 0;
    private const double OriginY = 40;

    private static readonly Map Map = MapViewState.Open(TestProgram.Tables(), "1234567812345678", 1).Map!;

    private static string? Box(
        (double X, double Y) camera,
        double scale,
        int column,
        int row,
        int radius,
        int headroom,
        out CropBox box
    ) =>
        ShotCrop.Box(
            Map,
            camera.X,
            camera.Y,
            scale,
            ViewWidth,
            ViewHeight,
            OriginX,
            OriginY,
            column,
            row,
            radius,
            headroom,
            out box
        );

    private static (double X, double Y) Aimed((double X, double Y) camera, double scale, int column, int row)
    {
        var problem = MapPicking.Aim(
            Map,
            camera.X,
            camera.Y,
            scale,
            ViewWidth,
            ViewHeight,
            OriginY,
            column,
            row,
            out var x,
            out var y
        );
        Assert.IsNull(problem);

        return (x, y);
    }

    private static int Height(int column, int row)
    {
        var width = Map.Size.Width;
        var rows = Map.Size.Height;
        var c = ((column % width) + width) % width;
        var r = ((row % rows) + rows) % rows;

        return (Map.Layer(MapLayer.Height)[r * width + c] & Map.HeightMask) * TerrainTriangles.HeightStep;
    }

    [TestMethod]
    public void ARadiusOfZeroFramesTheAimedTileAlone()
    {
        var camera = (300.0, 200.0);
        var problem = Box(camera, 2, 10, 10, 0, 0, out var box);

        Assert.IsNull(problem);
        var (x, y) = Aimed(camera, 2, 10, 10);
        Assert.AreEqual(
            new CropBox(
                (int)Math.Floor(x - 16 * 2 + OriginX),
                (int)Math.Floor(y - 10 * 2 + OriginY),
                (int)Math.Ceiling(x + 16 * 2 + OriginX) - (int)Math.Floor(x - 16 * 2 + OriginX),
                (int)Math.Ceiling(y + 10 * 2 + OriginY) - (int)Math.Floor(y - 10 * 2 + OriginY)
            ),
            box
        );
    }

    [TestMethod]
    public void TheBoxCoversEveryTileWithinTheRadiusAndTheHeadroomAbove()
    {
        var camera = (300.0, 200.0);
        const double scale = 1.5;
        const int headroom = 50;
        var problem = Box(camera, scale, 10, 10, 1, headroom, out var box);

        Assert.IsNull(problem);
        var (x, y) = Aimed(camera, scale, 10, 10);
        // The aimed tile's drawn point less its own lift and position: the screen point of the copy's origin.
        var aimHeight = Height(10, 10);
        var (nx, ny) = Camera.NodePosition(10, 10);
        (int, int)[] ring = [(0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1)];
        foreach (var (dc, dr) in ring)
        {
            var (px, py) = Camera.NodePosition(10 + dc, 10 + dr);
            var sx = x + (px - nx) * scale + OriginX;
            var sy = y + (py - Height(10 + dc, 10 + dr) - (ny - aimHeight)) * scale + OriginY;
            Assert.IsTrue(sx >= box.X && sx <= box.X + box.Width, $"x of {dc},{dr}");
            Assert.IsTrue(sy >= box.Y && sy <= box.Y + box.Height, $"y of {dc},{dr}");
        }

        // Two steps away is not in a radius of 1: (2, 0) lies beyond the box east.
        var (fx, _) = Camera.NodePosition(12, 10);
        Assert.IsTrue(x + (fx - nx) * scale + OriginX > box.X + box.Width);
        Assert.IsTrue(box.Y <= y + OriginY - (10 + headroom) * scale);
    }

    [TestMethod]
    public void ATileAtTheSeamTakesItsRingFromTheSameCopy()
    {
        // Camera on the map's north-west node: column 0 is drawn near the middle, column -1 is the last column.
        var (cx, cy) = Camera.NodePosition(0, 5);
        var problem = Box((cx, cy), 1, 0, 5, 1, 0, out var box);

        Assert.IsNull(problem);
        Assert.IsTrue(box.Width <= 3 * Camera.NodeSpacing + 2, $"width {box.Width}");
    }

    [TestMethod]
    public void ABoxReachingPastTheCanvasIsClippedToIt()
    {
        // A tile drawn near the top-left corner of the canvas, with a wide ring.
        var (nx, ny) = Camera.NodePosition(5, 2);
        var (column, row, cam) = (5, 2, (nx + 380.0, ny - Height(5, 2) + 280.0));
        var problem = Box(cam, 1, column, row, 3, 100, out var box);

        Assert.IsNull(problem);
        Assert.AreEqual((int)OriginX, box.X);
        Assert.AreEqual((int)OriginY, box.Y);
        Assert.IsTrue(box.Width > 0 && box.Height > 0);
        Assert.IsTrue(box.X + box.Width <= OriginX + ViewWidth);
    }

    [TestMethod]
    public void AnAimedTileThatCannotBeClickedIsTheClickProblem()
    {
        var problem = Box((300.0, 200.0), 1, Map.Size.Width, 0, 1, 0, out _);

        Assert.AreEqual(
            $"tile {Map.Size.Width},0 is outside the map of {Map.Size.Width} by {Map.Size.Height} tiles",
            problem
        );
    }

    [TestMethod]
    public void ACropTooLargeOnceScaledIsAProblemNamingBothSizes()
    {
        Assert.IsNull(ShotCrop.ScaleProblem(new CropBox(0, 0, 1000, 500), 16));
        Assert.AreEqual(
            "the crop of 1000x500 pixels at scale 17 is 17000x8500, more than 16384 on a side",
            ShotCrop.ScaleProblem(new CropBox(0, 0, 1000, 500), 17)
        );
    }
}
