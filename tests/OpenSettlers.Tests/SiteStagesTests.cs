using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The drawing of a building site by its progress word (docs/rules/construction.md, the draw routine from 0x44fa79): the layers a progress
// word draws and how much of the growing one shows. The images are made up here; the entries of the History Edition are in
// ObjectSpritesTests and the play runs.
[TestClass]
public sealed class SiteStagesTests
{
    [TestMethod]
    public void TheScaffoldIsLargeForTheSawmillSmallForTheHutsAndNoneForOtherTypes()
    {
        Assert.AreEqual(true, SiteStages.ScaffoldIsLarge(BuildingType.Sawmill));
        Assert.AreEqual(false, SiteStages.ScaffoldIsLarge(BuildingType.Woodcutter));
        Assert.AreEqual(false, SiteStages.ScaffoldIsLarge(BuildingType.Stonecutter));
        Assert.AreEqual(false, SiteStages.ScaffoldIsLarge(BuildingType.Forester));
        Assert.IsNull(SiteStages.ScaffoldIsLarge(BuildingType.Castle));
    }

    [TestMethod]
    public void AProgressOf0DrawsOnlyTheGroundOfALargeSite()
    {
        CollectionAssert.AreEqual(new[] { new SitePart(SiteLayer.Ground, null) }, SiteStages.Parts(0).ToArray());
    }

    [TestMethod]
    public void AProgressOf1DrawsTheBaseAndNoStageYet()
    {
        // Twice the word is 2, below the 3 the routine tests (0x44fc6d).
        CollectionAssert.AreEqual(new[] { new SitePart(SiteLayer.Base, null) }, SiteStages.Parts(1).ToArray());
    }

    [TestMethod]
    public void AProgressBelowTheTopBitDrawsTheBaseAndAStageGrownByTwiceTheWord()
    {
        CollectionAssert.AreEqual(
            new[] { new SitePart(SiteLayer.Base, null), new SitePart(SiteLayer.Stage, 0x2002) },
            SiteStages.Parts(0x1001).ToArray()
        );
    }

    [TestMethod]
    public void AProgressFromTheTopBitDrawsTheWholeStageAndTheFinishedBuildingGrownByTwiceTheRest()
    {
        CollectionAssert.AreEqual(
            new[] { new SitePart(SiteLayer.Stage, null), new SitePart(SiteLayer.Finished, 0x0002) },
            SiteStages.Parts(0x8001).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { new SitePart(SiteLayer.Stage, null) },
            SiteStages.Parts(0x8000).ToArray(),
            "a fraction of 0 draws nothing of the building"
        );
    }

    [TestMethod]
    public void AGrowingImageShowsItsBottomRowsByTheFractionOfItsHeight()
    {
        var image = Image(width: 2, height: 4, offsetY: -10);

        var grown = image.Grown(0x8000);

        // (0x8000 * 4 >> 16) + 1 = 3 rows from the bottom (0x406350).
        Assert.AreEqual(3, grown.Height);
        Assert.AreEqual(-9, grown.OffsetY);
        CollectionAssert.AreEqual(image.Rgba[(1 * 2 * 4)..], grown.Rgba);
        Assert.AreEqual(4, image.Grown(0xffff).Height, "the whole image at the top of the fraction");
        Assert.AreEqual(1, image.Grown(1).Height, "one row at the bottom");
    }

    [TestMethod]
    public void AnImageOverAnotherCoversTheUnionAndTheTopWinsWhereItIsCovered()
    {
        var below = Image(width: 2, height: 2, offsetX: 0, offsetY: 0, value: 1);
        var above = Image(width: 2, height: 2, offsetX: 1, offsetY: 1, value: 2);

        var both = below.Over(above);

        Assert.AreEqual((3, 3, 0, 0), (both.Width, both.Height, both.OffsetX, both.OffsetY));
        Assert.AreEqual(1, both.Rgba[(0 * 3 + 0) * 4], "only the lower image covers the corner");
        Assert.AreEqual(2, both.Rgba[(1 * 3 + 1) * 4], "the upper image covers where both do");
        Assert.AreEqual(3, both.Rgba[(2 * 3 + 2) * 4], "the upper image's second row, which carries its row index");
        Assert.AreEqual(0, both.Rgba[(0 * 3 + 2) * 4 + 3], "a pixel neither covers stays transparent");
    }

    [TestMethod]
    public void TheHistoryEditionDrawsTheScaffoldAndTheFinishedBuildingFromTheTablesObjects()
    {
        // The table at 0x44fcfe holds the object 186 (0xba) for the types 2, 4, and 9 and 181 (0xb5) for the type 0x11; the table at 0x44fd16
        // holds the finished buildings' objects 168, 169, 170, and 160. A sprite is the object plus the 1250.
        foreach (var small in new[] { BuildingType.Woodcutter, BuildingType.Stonecutter, BuildingType.Forester })
        {
            Assert.AreEqual(1250 + 186, ObjectSprites.SiteLayerEntry(SiteLayer.Stage, small));
        }

        Assert.AreEqual(1250 + 181, ObjectSprites.SiteLayerEntry(SiteLayer.Stage, BuildingType.Sawmill));
        Assert.AreEqual(1250 + 160, ObjectSprites.SiteLayerEntry(SiteLayer.Finished, BuildingType.Sawmill));
        Assert.AreEqual(1250 + 144, ObjectSprites.SiteLayerEntry(SiteLayer.Ground, BuildingType.Forester));
        Assert.AreEqual(1250 + 145, ObjectSprites.SiteLayerEntry(SiteLayer.Base, BuildingType.Forester));
        Assert.IsNull(ObjectSprites.SiteLayerEntry(SiteLayer.Stage, BuildingType.Storehouse));
    }

    static ObjectImage Image(int width, int height, int offsetX = 0, int offsetY = 0, byte value = 7)
    {
        var rgba = new byte[width * height * 4];
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            // The row index in the colour, so that a cut shows which rows were kept; every pixel is covered.
            rgba[pixel * 4] = pixel / width == 0 ? value : (byte)(value + pixel / width);
            rgba[pixel * 4 + 3] = 255;
        }

        return new ObjectImage(width, height, offsetX, offsetY, rgba);
    }
}
