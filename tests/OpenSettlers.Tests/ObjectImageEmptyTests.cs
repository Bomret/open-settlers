using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectImageEmptyTests
{
    // The doc of Empty: an image with no pixel, for an entry the archive leaves out.
    [TestMethod]
    public void TheEmptyImageHasNoSizeOffsetOrPixel()
    {
        var image = ObjectImage.Empty;

        Assert.AreEqual((0, 0, 0, 0), (image.Width, image.Height, image.OffsetX, image.OffsetY));
        Assert.IsEmpty(image.Rgba);
    }

    [TestMethod]
    public void TheEmptyImageDrawnOverAnImageLeavesItsPixels()
    {
        var image = new ObjectImage
        {
            Width = 1,
            Height = 1,
            OffsetX = 0,
            OffsetY = 0,
            Rgba = [1, 2, 3, 255],
        };

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 255 }, image.Over(ObjectImage.Empty).Rgba);
    }
}
