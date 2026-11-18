using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ImageLayersTests
{
    // A fixed set of images of mixed sizes, met one at a time as a game view's frames meet them.
    static List<ObjectImage> Images() =>
        [
            .. Enumerable
                .Range(0, 1000)
                .Select(i =>
                {
                    var width = 1 + i % 97;
                    var height = 1 + i % 61;

                    return new ObjectImage
                    {
                        Width = width,
                        Height = height,
                        OffsetX = 0,
                        OffsetY = 0,
                        Rgba = new byte[width * height * 4],
                    };
                }),
        ];

    // The view builds the array texture again only where the shape changes: the shape grows in steps, so a thousand
    // images met one at a time change it a handful of times, not once per image.
    [TestMethod]
    public void TheShapeChangesAFewTimesWhileImagesAreMetOneAtATime()
    {
        var layers = new ImageLayers();
        var shapes = new HashSet<(int, int, int)>();
        foreach (var image in Images())
        {
            layers.Of(image);
            shapes.Add(layers.Shape);
        }

        Assert.IsLessThanOrEqualTo(20, shapes.Count);
    }

    // The doc of Shape: an empty set takes one layer, and the width and height are the 32 step's smallest.
    [TestMethod]
    public void AnEmptySetTakesOneLayerOfTheSmallestStep() => Assert.AreEqual((1, 32, 32), new ImageLayers().Shape);

    // The layers are by image identity: two images equal by value but not the same object are two layers.
    [TestMethod]
    public void TwoValueEqualButDistinctImagesGiveTwoLayers()
    {
        var layers = new ImageLayers();
        var first = new ObjectImage
        {
            Width = 0,
            Height = 0,
            OffsetX = 0,
            OffsetY = 0,
            Rgba = [],
        };
        var second = new ObjectImage
        {
            Width = 0,
            Height = 0,
            OffsetX = 0,
            OffsetY = 0,
            Rgba = [],
        };

        Assert.AreEqual(first, second);
        Assert.AreEqual(0, layers.Of(first));
        Assert.AreEqual(1, layers.Of(second));
        Assert.AreEqual(0, layers.Of(first));
    }

    // Every image fits the shape: a layer each, and each image's width and height.
    [TestMethod]
    public void TheShapeHoldsEveryImage()
    {
        var layers = new ImageLayers();
        foreach (var image in Images())
        {
            layers.Of(image);
            var (count, width, height) = layers.Shape;
            Assert.IsTrue(
                count >= layers.Images.Count && width >= image.Width && height >= image.Height,
                $"{layers.Shape} for {layers.Images.Count} images, the last {image.Width} x {image.Height}"
            );
        }
    }
}
